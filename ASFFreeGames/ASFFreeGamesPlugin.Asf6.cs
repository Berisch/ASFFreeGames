#if !NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using SteamKit2;

namespace Maxisoft.ASF;

internal sealed partial class ASFFreeGamesPlugin {
	Task<string?> IBotCommand2.OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID) => OnBotCommand(bot, access, message, args, steamID);

	Task IBot.OnBotDestroy(Bot bot) => OnBotDestroy(bot, CancellationToken);
	Task IBot.OnBotInit(Bot bot) => OnBotInit(bot);
	Task IBotConnection.OnBotDisconnected(Bot bot, EResult reason) => OnBotDisconnected(bot, reason, CancellationToken);
	Task IBotConnection.OnBotLoggedOn(Bot bot) => OnBotLoggedOn(bot, CancellationToken);
	Task IPlugin.OnLoaded() => OnLoaded();
	Task IASF.OnASFInit(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties) => OnASFInit(additionalConfigProperties, CancellationToken);
	Task IUpdateAware.OnUpdateFinished(Version currentVersion, Version newVersion) => OnUpdateFinished(currentVersion, newVersion, CancellationToken);
	Task IUpdateAware.OnUpdateProceeding(Version currentVersion, Version newVersion) => OnUpdateProceeding(currentVersion, newVersion);

	Task<Uri?> IGitHubPluginUpdates.GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) => Updater.GetTargetReleaseURL(asfVersion, asfVariant, asfUpdate, stable, forced);
}
#endif
