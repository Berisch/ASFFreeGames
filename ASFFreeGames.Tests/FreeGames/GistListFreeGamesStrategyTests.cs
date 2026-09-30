using System.Linq;
using Maxisoft.ASF.FreeGames.Strategies;
using Maxisoft.ASF.Reddit;
using Xunit;

namespace Maxisoft.ASF.Tests.FreeGames;

public sealed class GistListFreeGamesStrategyTests {
	// same shape as the ASFinfo gist: append-only, one identifier per line, newest last
	private const string Gist = "a/1\ns/2\r\n  a/3  \nnot an identifier\n\nsub/5\na/1\ns/4";

	[Fact]
	public void TestNewestFirstAndDeduped() {
		string[] identifiers = GistListFreeGamesStrategy.ParseGist(Gist).Select(static entry => entry.Identifier).ToArray();

		Assert.Equal(["s/4", "a/1", "a/3", "s/2"], identifiers);
	}

	[Fact]
	public void TestMaxEntries() {
		string[] identifiers = GistListFreeGamesStrategy.ParseGist(Gist, 3).Select(static entry => entry.Identifier).ToArray();

		Assert.Equal(["s/4", "a/1", "a/3"], identifiers);
	}

	[Fact]
	public void TestEntriesHaveNoDateNorKind() {
		RedditGameEntry entry = GistListFreeGamesStrategy.ParseGist(Gist).First();

		Assert.Equal(0, entry.Date);
		Assert.Equal(ERedditGameEntryKind.None, entry.Kind);
	}

	[Fact]
	public void TestEmpty() => Assert.Empty(GistListFreeGamesStrategy.ParseGist(""));
}
