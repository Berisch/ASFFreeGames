using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Collections;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ASFFreeGames.ASFExtensions.Bot;
using ASFFreeGames.Commands;
using ASFFreeGames.Configurations;
using JetBrains.Annotations;
using Maxisoft.ASF.ASFExtensions;
using Maxisoft.ASF.Configurations;
using Maxisoft.ASF.Github;
using Maxisoft.ASF.Utils;
using Maxisoft.ASF.Utils.Workarounds;
using SteamKit2;
using static ArchiSteamFarm.Core.ASF;

namespace Maxisoft.ASF;

internal interface IASFFreeGamesPlugin {
	internal Version Version { get; }
	internal ASFFreeGamesOptions Options { get; }

	internal void CollectGamesOnClock(object? source);
}

#pragma warning disable CA1812 // ASF uses this class during runtime
[SuppressMessage("Design", "CA1001:Disposable fields")]
internal sealed class ASFFreeGamesPlugin : IASF, IBot, IBotConnection, IBotCommand2, IUpdateAware, IASFFreeGamesPlugin, IGitHubPluginUpdates {
	internal const string StaticName = nameof(ASFFreeGamesPlugin);
	private const int CollectGamesTimeout = 3 * 60 * 1000;

	internal static PluginContext Context {
		get => _context.Value ?? new PluginContext(Array.Empty<Bot>(), new ContextRegistry(), new ASFFreeGamesOptions(), new LoggerFilter());
		private set => _context.Value = value;
	}

	// ReSharper disable once InconsistentNaming
	private static readonly Utils.Workarounds.AsyncLocal<PluginContext> _context = new();
	private static CancellationToken CancellationToken => Context.CancellationToken;

	public string Name => StaticName;
	public Version Version => GetVersion();

	private static Version GetVersion() => typeof(ASFFreeGamesPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	private readonly ConcurrentHashSet<Bot> Bots = new(new BotEqualityComparer());
	private readonly Lazy<CancellationTokenSource> CancellationTokenSourceLazy = new(static () => new CancellationTokenSource());
	private readonly CommandDispatcher CommandDispatcher;

	private readonly LoggerFilter LoggerFilter = new();

	private bool VerboseLog => Options.VerboseLog ?? true;
	private readonly ContextRegistry BotContextRegistry = new();

	public ASFFreeGamesOptions Options => OptionsField;
	private ASFFreeGamesOptions OptionsField = new();

	private readonly CollectIntervalManager CollectIntervalManager;

	private static readonly TimeSpan AllBotsLoggedOnDelay = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan LateBotCatchUpDelay = TimeSpan.FromSeconds(30);

	// set once the first scheduled collect operation started / was moved earlier because every enabled bot is logged on
	private int FirstRunStarted;
	private int FirstRunScheduled;

	// bots included in a collect operation since the plugin started, so that a bot logging on late is caught up exactly once
	private readonly ConcurrentHashSet<string> ProcessedBotNames = new(StringComparer.OrdinalIgnoreCase);

	public ASFFreeGamesPlugin() {
		CommandDispatcher = new CommandDispatcher(Options);
		CollectIntervalManager = new CollectIntervalManager(this);
		_context.Value = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter) { CancellationTokenLazy = new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token) };
	}

	public async Task<string?> OnBotCommand(Bot? bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		if (!Context.Valid) {
			CreateContext();
		}

		return await CommandDispatcher.Execute(bot, message, args, steamID).ConfigureAwait(false);
	}

	public async Task OnBotDestroy(Bot bot) => await RemoveBot(bot).ConfigureAwait(false);

	public async Task OnBotDisconnected(Bot bot, EResult reason) => await RemoveBot(bot).ConfigureAwait(false);

	public Task OnBotInit(Bot bot) => Task.CompletedTask;

	public async Task OnBotLoggedOn(Bot bot) => await RegisterBot(bot).ConfigureAwait(false);

	public Task OnLoaded() {
		if (VerboseLog) {
			ArchiLogger.LogGenericInfo($"Loaded {Name}");
		}

		return Task.CompletedTask;
	}

	public async Task OnASFInit(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
		ASFFreeGamesOptionsLoader.Bind(ref OptionsField);
		JsonElement? jsonElement = GlobalDatabase?.LoadFromJsonStorage($"{Name}.Verbose");

		if (jsonElement?.ValueKind is JsonValueKind.True) {
			Options.VerboseLog = true;
		}

		await SaveOptions(CancellationToken).ConfigureAwait(false);
	}

	public async Task OnUpdateFinished(Version currentVersion, Version newVersion) => await SaveOptions(Context.CancellationToken).ConfigureAwait(false);

	public Task OnUpdateProceeding(Version currentVersion, Version newVersion) => Task.CompletedTask;

	public async void CollectGamesOnClock(object? source) {
		CollectIntervalManager.RandomlyChangeCollectInterval(source);

		if (!Context.Valid || ((Bots.Count > 0) && (Context.Bots.Count != Bots.Count))) {
			CreateContext();
		}

		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
		cts.CancelAfter(TimeSpan.FromMilliseconds(CollectGamesTimeout));

		if (cts.IsCancellationRequested || !Context.Valid) {
			return;
		}

		// ReSharper disable once AccessToDisposedClosure
		using (Context.TemporaryChangeCancellationToken(() => cts.Token)) {
			Bot[] reorderedBots;
			IContextRegistry botContexts = Context.BotContexts;

			lock (botContexts) {
				long orderByRunKeySelector(Bot bot) => botContexts.GetBotContext(bot)?.RunElapsedMilli ?? long.MaxValue;
				int comparison(Bot x, Bot y) => orderByRunKeySelector(y).CompareTo(orderByRunKeySelector(x)); // sort in descending order
				reorderedBots = Bots.ToArray();
				Array.Sort(reorderedBots, comparison);
			}

			if (reorderedBots.Length == 0) {
				ArchiLogger.LogGenericDebug("no viable bot found for freegame scheduled operation");

				return;
			}

			if (Interlocked.Exchange(ref FirstRunStarted, 1) == 0) {
				string[] missingBots = GetEnabledBotsNotLoggedOn();

				if (missingBots.Length > 0) {
					ArchiLogger.LogGenericWarning($"[FreeGames] starting the first collection without {missingBots.Length} enabled bot(s) that are not logged on: {string.Join(", ", missingBots)}");
				}
			}

			foreach (Bot bot in reorderedBots) {
				ProcessedBotNames.Add(bot.BotName);
			}

			if (!cts.IsCancellationRequested) {
				string cmd = $"FREEGAMES {FreeGamesCommand.CollectInternalCommandString} " + string.Join(' ', reorderedBots.Select(static bot => bot.BotName));

				try {
					await OnBotCommand(null, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);
				}
				catch (Exception ex) {
					ArchiLogger.LogGenericWarning($"Failed to execute scheduled free games collection: {ex.Message}");
				}
			}
		}
	}

	/// <summary>
	/// Creates a new PluginContext instance and assigns it to the Context property.
	/// </summary>
	private void CreateContext() => Context = new PluginContext(Bots, BotContextRegistry, Options, LoggerFilter, true) { CancellationTokenLazy = new Lazy<CancellationToken>(() => CancellationTokenSourceLazy.Value.Token) };

	private async Task RegisterBot(Bot bot) {
		Bots.Add(bot);

		StartTimerIfNeeded();

		await BotContextRegistry.SaveBotContext(bot, new BotContext(bot), CancellationToken).ConfigureAwait(false);
		BotContext? ctx = BotContextRegistry.GetBotContext(bot);

		if (ctx is not null) {
			await ctx.LoadFromFileSystem(CancellationToken).ConfigureAwait(false);
		}

		OnBotReady(bot);
	}

	private void OnBotReady(Bot bot) {
		if (Volatile.Read(ref FirstRunStarted) == 0) {
			// right after ASF (re)starts: wait for every enabled bot instead of collecting for whichever logged on first
			if ((GetEnabledBotsNotLoggedOn().Length == 0) && (Interlocked.Exchange(ref FirstRunScheduled, 1) == 0)) {
				ArchiLogger.LogGenericInfo($"[FreeGames] all {Bots.Count} enabled bot(s) logged on, starting collection in {AllBotsLoggedOnDelay.TotalSeconds}s");
				CollectIntervalManager.ScheduleNextRun(AllBotsLoggedOnDelay);
			}

			return;
		}

		if (!ProcessedBotNames.Contains(bot.BotName)) {
			// the plugin hook must not block ASF, so the catch up runs in the background
			ArchiSteamFarm.Core.Utilities.InBackground(() => CatchUpLateBot(bot));
		}
	}

	// runs unobserved in the background, so it must never throw
	private async Task CatchUpLateBot(Bot bot) {
		try {
			await Task.Delay(LateBotCatchUpDelay, CancellationToken).ConfigureAwait(false);

			// the bot may have disconnected meanwhile, or been included in a scheduled run
			if (!bot.IsConnectedAndLoggedOn || !Bots.Contains(bot) || !ProcessedBotNames.Add(bot.BotName)) {
				return;
			}

			if (!Context.Valid || (Context.Bots.Count != Bots.Count)) {
				CreateContext();
			}

			// no TemporaryChangeCancellationToken here: it is not safe to use concurrently with a scheduled run
			string cmd = $"FREEGAMES {FreeGamesCommand.CollectCachedInternalCommandString} {bot.BotName}";
			await OnBotCommand(null, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);
		}
		catch (OperationCanceledException) {
			// plugin is shutting down
		}
		catch (Exception ex) {
			ArchiLogger.LogGenericWarning($"Failed to catch up free games collection for {bot.BotName}: {ex.Message}");
		}
	}

	private string[] GetEnabledBotsNotLoggedOn() {
		IReadOnlyDictionary<string, Bot>? allBots = Bot.BotsReadOnly;

		if (allBots is null) {
			return [];
		}

		return allBots.Values.Where(b => b.BotConfig.Enabled && !Bots.Contains(b)).Select(static b => b.BotName).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private async Task RemoveBot(Bot bot) {
		Bots.Remove(bot);

		BotContext? botContext = BotContextRegistry.GetBotContext(bot);

		if (botContext is not null) {
			try {
				await botContext.SaveToFileSystem(CancellationToken).ConfigureAwait(false);
			}
			finally {
				await BotContextRegistry.RemoveBotContext(bot).ConfigureAwait(false);
				botContext.Dispose();
			}
		}

		if (Bots.Count == 0) {
			CollectIntervalManager.StopTimer();
		}

		LoggerFilter.RemoveFilters(bot);
		BotPackageChecker.RemoveBotCache(bot);
	}

	// ReSharper disable once UnusedMethodReturnValue.Local
	private async Task<string?> SaveOptions(CancellationToken cancellationToken) {
		if (!cancellationToken.IsCancellationRequested) {
			const string cmd = $"FREEGAMES {FreeGamesCommand.SaveOptionsInternalCommandString}";
			async Task<string?> continuation() => await OnBotCommand(Bots.FirstOrDefault()!, EAccess.None, cmd, cmd.Split()).ConfigureAwait(false);

			string? result;

			if (Context.Valid) {
				using (Context.TemporaryChangeCancellationToken(() => cancellationToken)) {
					result = await continuation().ConfigureAwait(false);
				}
			}
			else {
				result = await continuation().ConfigureAwait(false);
			}

			return result;
		}

		return null;
	}

	private void StartTimerIfNeeded() => CollectIntervalManager.StartTimerIfNeeded();

	~ASFFreeGamesPlugin() => CollectIntervalManager.Dispose();

	#region IGitHubPluginUpdates implementation
	private readonly GithubPluginUpdater Updater = new(new Lazy<Version>(GetVersion));
	string IGitHubPluginUpdates.RepositoryName => GithubPluginUpdater.RepositoryName;

	bool IGitHubPluginUpdates.CanUpdate => Updater.CanUpdate;

	Task<Uri?> IGitHubPluginUpdates.GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) => Updater.GetTargetReleaseURL(asfVersion, asfVariant, asfUpdate, stable, forced);
	#endregion
}

#pragma warning restore CA1812 // ASF uses this class during runtime
