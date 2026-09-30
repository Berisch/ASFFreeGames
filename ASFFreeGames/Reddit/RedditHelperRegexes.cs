using System.Text.RegularExpressions;

namespace Maxisoft.ASF.Reddit;

internal static partial class RedditHelperRegexes {
	[GeneratedRegex(@"(.addlicense)\s+(asf)?\s*((?<appid>(s/|a/)\d+)\s*,?\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex Command();

	[GeneratedRegex(@"(?<appid>(s/|a/)\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex AppId();

	[GeneratedRegex(@"free\s+DLC\s+for\s+a", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsDlc();

	// "permanently free" is the bot's old wording, "free to play" the current one
	[GeneratedRegex(@"permanently\s+free|free\s+to\s+play", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	internal static partial Regex IsFreeToPlay();

	// Atom feed parsing is regex based on purpose: ASF OS-specific builds are trimmed, so System.Xml.Linq may be missing at runtime
	[GeneratedRegex(@"<entry>(?<body>.*?)</entry>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
	internal static partial Regex AtomEntry();

	[GeneratedRegex(@"<content[^>]*>(?<content>.*?)</content>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
	internal static partial Regex AtomContent();

	[GeneratedRegex(@"<updated>(?<date>[^<]+)</updated>", RegexOptions.CultureInvariant)]
	internal static partial Regex AtomUpdated();
}
