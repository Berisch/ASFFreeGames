using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Maxisoft.ASF.Reddit;
using Xunit;

namespace Maxisoft.ASF.Tests.Reddit;

public sealed class RedditHelperAtomTests {
	[Fact]
	public async Task TestEntriesAreDedupedInFeedOrder() {
		RedditGameEntry[] entries = await LoadAsfinfoFeedEntries().ConfigureAwait(true);

		// the FAQ post has no addlicense command, a/3594170 and a/4319430 are posted in two subreddits each
		string[] expected = ["a/3594170", "a/4319430", "a/5255050", "s/1843288", "s/1827145", "s/1830927", "a/3008200", "a/4091610", "s/1789456"];
		Assert.Equal(expected, entries.Select(static entry => entry.Identifier));
	}

	[Fact]
	public async Task TestDateIsUnixSecondsOfNewestPost() {
		RedditGameEntry[] entries = await LoadAsfinfoFeedEntries().ConfigureAwait(true);
		RedditGameEntry entry = Array.Find(entries, static entry => entry.Identifier == "a/3594170");

		Assert.Equal(new DateTimeOffset(2026, 9, 30, 12, 31, 15, TimeSpan.Zero).ToUnixTimeSeconds(), entry.Date);
	}

	[Theory]
	[InlineData("a/3594170", true, false)] // "This game is currently free to play."
	[InlineData("a/3008200", true, false)] // multiple ids in the same command
	[InlineData("a/4091610", true, false)]
	[InlineData("a/5255050", false, true)] // "There is a chance this is free DLC for a non-free game."
	[InlineData("s/1843288", false, true)]
	[InlineData("s/1789456", false, false)]
	public async Task TestKindParsing(string identifier, bool freeToPlay, bool dlc) {
		RedditGameEntry[] entries = await LoadAsfinfoFeedEntries().ConfigureAwait(true);
		RedditGameEntry entry = Array.Find(entries, entry => entry.Identifier == identifier);

		Assert.Equal(identifier, entry.Identifier);
		Assert.Equal(freeToPlay, entry.IsFreeToPlay);
		Assert.Equal(dlc, entry.IsForDlc);
	}

	[Theory]
	[InlineData("")]
	[InlineData("<html><body>You've been blocked by network security.</body></html>")]
	[InlineData("<feed><entry><content type=\"html\">!addlicense asf a/1</content></entry></feed>")] // no <updated>
	public void TestInvalidFeedsYieldNothing(string feed) => Assert.Empty(RedditHelper.LoadMessagesFromAtom(feed));

	[Theory]
	[InlineData("&lt;code&gt;!addlicense asf a/1 &lt;/code&gt;", "<code>!addlicense asf a/1 </code>")]
	[InlineData("I&amp;#39;m&amp;nbsp;a bot", "I&#39;m&nbsp;a bot")] // a single pass only decodes the xml level
	[InlineData("I&#39;m&nbsp;a&#x200B;bot &#32;", "I'm a​bot  ")]
	[InlineData("&unknown; &#0; &#xD800; &", "&unknown; &#0; &#xD800; &")]
	public void TestDecodeHtmlEntities(string input, string expected) => Assert.Equal(expected, RedditHelper.DecodeHtmlEntities(input));

	private static async Task<RedditGameEntry[]> LoadAsfinfoFeedEntries() {
		Assembly assembly = Assembly.GetExecutingAssembly();

#pragma warning disable CA2007
		await using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.ASFinfo.rss")!;
#pragma warning restore CA2007
		using StreamReader reader = new(stream);
		string feed = await reader.ReadToEndAsync().ConfigureAwait(false);

		return RedditHelper.LoadMessagesFromAtom(feed).ToArray();
	}
}
