using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Web.GitHub;
using ArchiSteamFarm.Web.GitHub.Data;

namespace Maxisoft.ASF.Github;

public class GithubPluginUpdater(Lazy<Version> version) {
	public const string RepositoryName = "Berisch/ASFFreeGames";
#if ASF_V7
	private const int BuildAsfMajor = 7;
#else
	private const int BuildAsfMajor = 6;
#endif
	private static readonly Version LastLegacyVersion = new(1, 10, 2, 0);
	private const int MinimumArchiveSize = 1 << 16;

	public bool CanUpdate { get; internal set; } = true;

	public Task<Uri?> GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced) =>
		GetTargetReleaseURL(asfVersion, asfVariant, asfUpdate, stable, forced, CancellationToken.None);

	public async Task<Uri?> GetTargetReleaseURL(Version asfVersion, string asfVariant, bool asfUpdate, bool stable, bool forced, CancellationToken cancellationToken) {
		ArgumentNullException.ThrowIfNull(asfVersion);
		ArgumentException.ThrowIfNullOrEmpty(asfVariant);

		if (!CanUpdate) {
			return null;
		}

		// GitHub's "latest" release stays on the single-archive bridge for older plugin installations.
		var releases = await GitHubService.GetReleases(RepositoryName, 100, cancellationToken).ConfigureAwait(false);
		if (releases is null) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericError($"{nameof(GithubPluginUpdater)}: GetReleases returned null");
			return null;
		}

		ReleaseAsset? asset = SelectReleaseAsset(releases, asfVersion.Major, asfUpdate, stable, forced, DateTime.UtcNow);
		if (asset is not null) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericDebug($"{nameof(GithubPluginUpdater)}: Selected {asset.DownloadURL} for ASF {asfVersion}");
		}

		return asset?.DownloadURL;
	}

	internal ReleaseAsset? SelectReleaseAsset(IEnumerable<ReleaseResponse> releases, int targetMajor, bool asfUpdate, bool stable, bool forced, DateTime utcNow) {
		Version currentVersion = NormalizeVersion(version.Value);
		bool migrating = asfUpdate && (targetMajor != BuildAsfMajor);
		string targetName = $"ASFFreeGames-V{targetMajor}.zip";
		Version? bestVersion = null;
		ReleaseAsset? bestAsset = null;

		foreach (ReleaseResponse release in releases) {
			// ASF can replace its own files even if no plugin update was found. A major upgrade needs its matching plugin immediately.
			if (release.IsPreRelease || (stable && !migrating && (utcNow - release.PublishedAt < TimeSpan.FromHours(3)))) {
				continue;
			}

			ReadOnlySpan<char> tag = release.Tag.AsSpan();
			if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
				tag = tag[1..];
			}
			if (!Version.TryParse(tag, out Version? parsed) || (parsed.Build < 0)) {
				continue;
			}

			Version candidateVersion = NormalizeVersion(parsed);
			if ((candidateVersion <= bestVersion) || (!forced && ((candidateVersion < currentVersion) || ((candidateVersion == currentVersion) && !migrating)))) {
				continue;
			}

			ReleaseAsset? asset = release.Assets.FirstOrDefault(a => a.Name.Equals(targetName, StringComparison.Ordinal) && (a.Size > MinimumArchiveSize));
			// Only the old V6 releases used the generic name.
			if ((asset is null) && (targetMajor == 6) && (candidateVersion <= LastLegacyVersion)) {
				asset = release.Assets.FirstOrDefault(static a => a.Name.Equals("ASFFreeGames-generic.zip", StringComparison.Ordinal) && (a.Size > MinimumArchiveSize));
			}
			if (asset is not null) {
				bestVersion = candidateVersion;
				bestAsset = asset;
			}
		}

		return bestAsset;
	}

	private static Version NormalizeVersion(Version value) => value.Revision < 0 ? new Version(value.Major, value.Minor, Math.Max(value.Build, 0), 0) : value;
}
