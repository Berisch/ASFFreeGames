using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.ASF.Reddit;
using Maxisoft.ASF.Redlib;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

/// <summary>
///     Tries every source one after another, from the richest one (dates, free to play and DLC flags) to the last resort, and returns the first non empty result.
///     Sources are not raced on purpose: this runs in the background, and hammering blocked or rate limited endpoints in parallel only gets us blocked harder.
/// </summary>
[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public class ListFreeGamesMainStrategy : IListFreeGamesStrategy {
	private static readonly TimeSpan RedditTimeout = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan GistTimeout = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan RedlibTimeout = TimeSpan.FromSeconds(45);

	private readonly RedditListFreeGamesStrategy RedditStrategy = new();
	private readonly GistListFreeGamesStrategy GistStrategy = new();
	private readonly RedlibListFreeGamesStrategy RedlibStrategy = new();

	private SemaphoreSlim StrategySemaphore { get; } = new(1, 1); // prevents concurrent run and access to internal state

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public async Task<IReadOnlyCollection<RedditGameEntry>> GetGames([NotNull] ListFreeGamesContext context, CancellationToken cancellationToken) {
		await StrategySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try {
			return await DoGetGames(context, cancellationToken).ConfigureAwait(false);
		}
		finally {
			StrategySemaphore.Release();
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			RedditStrategy.Dispose();
			GistStrategy.Dispose();
			RedlibStrategy.Dispose();
			StrategySemaphore.Dispose();
		}
	}

	private async Task<IReadOnlyCollection<RedditGameEntry>> DoGetGames([NotNull] ListFreeGamesContext context, CancellationToken cancellationToken) {
		(EListFreeGamesStrategy Kind, IListFreeGamesStrategy Strategy, ListFreeGamesContext Context, TimeSpan Timeout)[] sources = [
			(EListFreeGamesStrategy.Reddit, RedditStrategy, context with {
				Retry = 2,
				HttpClient = new Lazy<SimpleHttpClient>(() => context.HttpClientFactory.CreateForReddit())
			}, RedditTimeout),
			(EListFreeGamesStrategy.Gist, GistStrategy, context with { HttpClient = new Lazy<SimpleHttpClient>(() => context.HttpClientFactory.CreateForGithub()) }, GistTimeout),
			(EListFreeGamesStrategy.Redlib, RedlibStrategy, context with { HttpClient = new Lazy<SimpleHttpClient>(() => context.HttpClientFactory.CreateForRedlib()) }, RedlibTimeout)
		];

		List<Exception> exceptions = new(sources.Length);

		foreach ((EListFreeGamesStrategy kind, IListFreeGamesStrategy strategy, ListFreeGamesContext strategyContext, TimeSpan timeout) in sources) {
			cancellationToken.ThrowIfCancellationRequested();

			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(timeout);

			try {
				IReadOnlyCollection<RedditGameEntry> result = await strategy.GetGames(strategyContext, cts.Token).ConfigureAwait(false);

				if (result.Count > 0) {
					context.PreviousSucessfulStrategy = kind;

					return result;
				}
			}
			catch (RedlibDisabledException) {
				// disabled by the user's configuration, not an error
			}
			catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
				exceptions.Add(e is OperationCanceledException ? new TimeoutException($"{kind} source timed out after {timeout.TotalSeconds}s", e) : e);
			}
		}

		context.PreviousSucessfulStrategy = EListFreeGamesStrategy.None;

		switch (exceptions.Count) {
			case 1:
				throw exceptions[0];
			case > 0:
				throw new AggregateException(exceptions);
			default:
				return [];
		}
	}
}
