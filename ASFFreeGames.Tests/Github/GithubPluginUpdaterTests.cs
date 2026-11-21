using System;
using System.Linq;
using System.Text.Json;
using ArchiSteamFarm.Web.GitHub.Data;
using Maxisoft.ASF.Github;
using Xunit;

namespace Maxisoft.ASF.Tests.Github;

public sealed class GithubPluginUpdaterTests {
	private const string V6 = "ASFFreeGames-V6.zip";
	private const string V7 = "ASFFreeGames-V7.zip";
	private const string Legacy = "ASFFreeGames-generic.zip";
	private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

	[Theory]
	[InlineData(6, V6)]
	[InlineData(7, V7)]
	[InlineData(8, null)]
	public void SelectsOnlyTheExactTargetArchive(int targetMajor, string? expected) {
		ReleaseResponse release = Release("v1.11.0", [V7, "ASFFreeGames-V6-symbols.zip", Legacy, V6, "other.zip"]);
		Assert.Equal(expected, Select([release], targetMajor)?.Name);
	}

	[Fact]
	public void LegacyArchiveCanOnlyUpdateV6ThroughTheBridge() {
		ReleaseResponse bridge = Release("v1.10.2", [Legacy]);
		ReleaseResponse dualEra = Release("v1.11.0", [Legacy]);
		Assert.Equal(Legacy, Select([bridge], 6)?.Name);
		Assert.Null(Select([bridge], 7, forced: true));
		Assert.Null(Select([dualEra], 6));
		Assert.Equal(V6, Select([Release("v1.10.2", [Legacy, V6])], 6)?.Name);
	}

	[Theory]
	[InlineData("v1.11.0", "1.11.0.0")]
	[InlineData("V1.11.0.0", "1.11.0")]
	public void SameVersionMigratesOnlyWhenAsfChangesMajor(string tag, string installed) {
		int installedMajor = typeof(ReleaseResponse).Assembly.GetName().Version!.Major;
		int otherMajor = installedMajor == 6 ? 7 : 6;
		ReleaseResponse release = Release(tag, [V6, V7], ageInHours: 0);
		Assert.Equal(otherMajor == 7 ? V7 : V6, Select([release], otherMajor, installed, asfUpdate: true)?.Name);
		Assert.Null(Select([release], otherMajor, installed, stable: false));
		Assert.Null(Select([release], installedMajor, installed, stable: false, asfUpdate: true));
	}

	[Fact]
	public void ScansForHighestCompatiblePublishedVersion() {
		ReleaseResponse expected = Release("v1.11.0.1", [V7]);
		ReleaseResponse[] releases = [
			Release("v1.20.0", [V6]),
			Release("garbage", [V7]),
			Release("v1.14.0", [V7], ageInHours: 1),
			Release("v1.13.0", [V7], prerelease: true),
			Release("v1.12.0", [V7], assetSize: 65536),
			Release("v1.11.0.0", [V6, V7]),
			expected,
			Release("v1.10.2", [Legacy])
		];
		Assert.Same(expected.Assets.Single(), Select(releases, 7));
		Assert.Same(expected.Assets.Single(), Select(releases.Reverse().ToArray(), 7));
		Assert.Equal(releases[2].Assets.Single(), Select(releases, 7, stable: false));
	}

	[Fact]
	public void DowngradeRequiresForceAndUsesOnlyMatchingAsset() {
		ReleaseResponse release = Release("v1.11.0", [V6, V7]);
		Assert.Null(Select([release], 7, "1.11.0.1", asfUpdate: true));
		Assert.Equal(V7, Select([release], 7, "1.11.0.1", forced: true)?.Name);
		Assert.Null(Select([Release("v1.11.0", [V6])], 7, "1.11.0.1", forced: true));
	}

	private static ReleaseAsset? Select(ReleaseResponse[] releases, int targetMajor, string installed = "1.10.1.0", bool asfUpdate = false, bool stable = true, bool forced = false) {
		GithubPluginUpdater updater = new(new Lazy<Version>(() => new Version(installed)));
		return updater.SelectReleaseAsset(releases, targetMajor, asfUpdate, stable, forced, Now);
	}

	private static ReleaseResponse Release(string tag, string[] names, int ageInHours = 4, bool prerelease = false, uint assetSize = 65537) =>
		JsonSerializer.SerializeToElement(new {
			tag_name = tag,
			body = "",
			published_at = Now.AddHours(-ageInHours),
			prerelease,
			assets = names.Select(name => new { name, size = assetSize, browser_download_url = $"https://example.org/{tag}/{name}" })
		}).Deserialize<ReleaseResponse>()!;
}
