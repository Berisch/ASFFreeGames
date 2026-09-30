using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using Maxisoft.ASF.HttpClientSimple;
using Maxisoft.ASF.Reddit;

// ReSharper disable once CheckNamespace
namespace Maxisoft.ASF.FreeGames.Strategies;

/// <summary>
///     Lists free games from the gist maintained by the ASFinfo bot itself (https://github.com/C4illin/ASFclaim).
///     The gist is append-only, one <c>a/&lt;id&gt;</c> or <c>s/&lt;id&gt;</c> per line, and is hosted on GitHub so it is not affected by Reddit blocks.
/// </summary>
/// <remarks>The gist has neither dates nor free to play / DLC flags, so entries are reported with an unknown date (0) and no kind.</remarks>
[SuppressMessage("ReSharper", "RedundantNullableFlowAttribute")]
public sealed partial class GistListFreeGamesStrategy : IListFreeGamesStrategy {
	internal const int MaxEntries = 100;
	internal static readonly Uri GistUri = new("https://gist.githubusercontent.com/C4illin/e8c5cf365d816f2640242bf01d8d3675/raw/Steam%20Codes", UriKind.Absolute);

	public void Dispose() { }

	public async Task<IReadOnlyCollection<RedditGameEntry>> GetGames([NotNull] ListFreeGamesContext context, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CAC001
#pragma warning disable CA2007
		await using HttpStreamResponse resp = await context.HttpClient.Value.GetStreamAsync(GistUri, cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2007
#pragma warning restore CAC001

		if (!resp.StatusCode.IsSuccessCode() || !resp.HasValidStream) {
			throw new HttpRequestException($"invalid status code {resp.StatusCode} for {GistUri}", null, resp.StatusCode);
		}

		string content = await resp.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		return ParseGist(content);
	}

	/// <summary>
	///     Parses the gist content.
	/// </summary>
	/// <returns>At most <paramref name="maxEntries" /> unique entries, newest (last lines) first.</returns>
	internal static IReadOnlyCollection<RedditGameEntry> ParseGist(string content, int maxEntries = MaxEntries) {
		MatchCollection matches = GameIdentifierLine().Matches(content);
		List<RedditGameEntry> res = new(Math.Min(matches.Count, maxEntries));
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		for (int i = matches.Count - 1; (i >= 0) && (res.Count < maxEntries); i--) {
			string identifier = matches[i].Groups["id"].Value;

			if (seen.Add(identifier)) {
				res.Add(new RedditGameEntry(identifier, ERedditGameEntryKind.None, 0));
			}
		}

		return res;
	}

	[GeneratedRegex(@"^\s*(?<id>[as]/\d+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex GameIdentifierLine();
}
