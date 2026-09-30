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

	// System.Net.WebUtility is trimmed out of ASF builds, so the few entities used by the feed are decoded by hand
	[GeneratedRegex(@"&(?:#(?<dec>[0-9]{1,7})|#[xX](?<hex>[0-9a-fA-F]{1,6})|(?<name>amp|lt|gt|quot|apos|nbsp));", RegexOptions.CultureInvariant)]
	internal static partial Regex HtmlEntity();
}
