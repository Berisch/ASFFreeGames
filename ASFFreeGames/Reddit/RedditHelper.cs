using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using Maxisoft.ASF.HttpClientSimple;

namespace Maxisoft.ASF.Reddit;

internal static class RedditHelper {
	private const int MaxGameEntry = 1024;
	internal const string User = "ASFinfo";

	/// <summary>
	///     Gets a collection of Reddit game entries from the user's Atom feed.
	/// </summary>
	/// <returns>A collection of Reddit game entries.</returns>
	/// <remarks>The JSON API answers 403 "blocked by network security" to unauthenticated clients since mid 2026, the RSS/Atom feed still works.</remarks>
	public static async ValueTask<IReadOnlyCollection<RedditGameEntry>> GetGames(SimpleHttpClient httpClient, uint retry = 5, CancellationToken cancellationToken = default) {
		string feed = await GetFeed(httpClient, cancellationToken, retry).ConfigureAwait(false);

		return LoadMessagesFromAtom(feed);
	}

	internal static IReadOnlyCollection<RedditGameEntry> LoadMessages(JsonNode children) {
		Maxisoft.Utils.Collections.Dictionaries.OrderedDictionary<RedditGameEntry, EmptyStruct> games = new(new GameEntryIdentifierEqualityComparer());

		// ReSharper disable once LoopCanBePartlyConvertedToQuery
		foreach (JsonNode? comment in (JsonArray) children) {
			JsonNode? commentData = comment?["data"];

			if (commentData is null) {
				continue;
			}

			long date;
			string text;

			try {
				text = commentData["body"]?.GetValue<string>() ?? string.Empty;

				try {
					date = checked((long) (commentData["created_utc"]?.GetValue<double>() ?? 0));
				}
				catch (Exception e) when (e is FormatException or InvalidOperationException) {
					date = 0;
				}

				if (!double.IsNormal(date) || (date <= 0)) {
					date = checked((long) (commentData["created"]?.GetValue<double>() ?? 0));
				}
			}
			catch (Exception e) when (e is FormatException or InvalidOperationException) {
				continue;
			}

			if (!double.IsNormal(date) || (date <= 0)) {
				continue;
			}

			if (!AddGamesFromText(games, text, date)) {
				break;
			}
		}

		return TrimToMaxGameEntry(games);
	}

	/// <summary>
	///     Gets a collection of Reddit game entries from an Atom feed (<c>/user/{User}.rss</c>).
	/// </summary>
	/// <param name="feed">The raw Atom xml.</param>
	/// <returns>A collection of Reddit game entries, in feed order (newest first).</returns>
	internal static IReadOnlyCollection<RedditGameEntry> LoadMessagesFromAtom(string feed) {
		Maxisoft.Utils.Collections.Dictionaries.OrderedDictionary<RedditGameEntry, EmptyStruct> games = new(new GameEntryIdentifierEqualityComparer());

		foreach (Match entry in RedditHelperRegexes.AtomEntry().Matches(feed)) {
			string body = entry.Groups["body"].Value;
			Match content = RedditHelperRegexes.AtomContent().Match(body);
			Match updated = RedditHelperRegexes.AtomUpdated().Match(body);

			if (!content.Success || !updated.Success) {
				continue;
			}

			if (!DateTimeOffset.TryParse(updated.Groups["date"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date)) {
				continue;
			}

			// the content is html escaped inside the xml, and the html itself contains entities such as &nbsp;
			string text = DecodeHtmlEntities(DecodeHtmlEntities(content.Groups["content"].Value));

			if (!AddGamesFromText(games, text, date.ToUnixTimeSeconds())) {
				break;
			}
		}

		return TrimToMaxGameEntry(games);
	}

	/// <summary>
	///     Extracts the addlicense commands of a single message and adds the resulting entries to <paramref name="games" />.
	/// </summary>
	/// <returns>false once <see cref="MaxGameEntry" /> is reached, true otherwise.</returns>
	private static bool AddGamesFromText(Maxisoft.Utils.Collections.Dictionaries.OrderedDictionary<RedditGameEntry, EmptyStruct> games, string text, long date) {
		MatchCollection matches = RedditHelperRegexes.Command().Matches(text);

		foreach (Match match in matches) {
			ERedditGameEntryKind kind = ERedditGameEntryKind.None;

			if (RedditHelperRegexes.IsFreeToPlay().IsMatch(text)) {
				kind |= ERedditGameEntryKind.FreeToPlay;
			}

			if (RedditHelperRegexes.IsDlc().IsMatch(text)) {
				kind = ERedditGameEntryKind.Dlc;
			}

			// Use separate matches to extract all app IDs (avoids Group.Captures compatibility issues)
			MatchCollection appIdMatches = RedditHelperRegexes.AppId().Matches(match.Value);

			foreach (Match appIdMatch in appIdMatches) {
				string appIdValue = appIdMatch.Groups["appid"].Value;
				RedditGameEntry gameEntry = new(appIdValue, kind, date);

				try {
					games.Add(gameEntry, default(EmptyStruct));
				}
				catch (ArgumentException) { }

				if (games.Count >= MaxGameEntry) {
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	///     Decodes numeric entities and the few named ones found in the feed.
	/// </summary>
	/// <remarks>System.Net.WebUtility.HtmlDecode and Regex.Replace with a MatchEvaluator would do, but both are trimmed out of ASF builds.</remarks>
	internal static string DecodeHtmlEntities(string text) {
		MatchCollection matches = RedditHelperRegexes.HtmlEntity().Matches(text);

		if (matches.Count == 0) {
			return text;
		}

		StringBuilder builder = new(text.Length);
		int last = 0;

		foreach (Match match in matches) {
			builder.Append(text, last, match.Index - last);
			builder.Append(DecodeHtmlEntity(match));
			last = match.Index + match.Length;
		}

		builder.Append(text, last, text.Length - last);

		return builder.ToString();
	}

	private static string DecodeHtmlEntity(Match match) {
		if (match.Groups["dec"].Success) {
			return int.TryParse(match.Groups["dec"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int codePoint) ? FromCodePoint(codePoint, match.Value) : match.Value;
		}

		if (match.Groups["hex"].Success) {
			return int.TryParse(match.Groups["hex"].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int codePoint) ? FromCodePoint(codePoint, match.Value) : match.Value;
		}

		return match.Groups["name"].Value switch {
			"amp" => "&",
			"lt" => "<",
			"gt" => ">",
			"quot" => "\"",
			"apos" => "'",
			"nbsp" => " ",
			_ => match.Value
		};
	}

	private static string FromCodePoint(int codePoint, string fallback) => codePoint is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(codePoint) : fallback;

	private static IReadOnlyCollection<RedditGameEntry> TrimToMaxGameEntry(Maxisoft.Utils.Collections.Dictionaries.OrderedDictionary<RedditGameEntry, EmptyStruct> games) {
		while (games.Count is > 0 and > MaxGameEntry) {
			games.RemoveAt(games.Count - 1);
		}

		return (IReadOnlyCollection<RedditGameEntry>) games.Keys;
	}

	/// <summary>
	///     Tries to get the Atom feed from Reddit.
	/// </summary>
	/// <param name="httpClient">The http client instance to use.</param>
	/// <param name="cancellationToken"></param>
	/// <param name="retry"></param>
	/// <returns>The raw Atom xml.</returns>
	/// <exception cref="RedditServerException">Thrown when Reddit returns a server error.</exception>
	/// <remarks>This method is based on this GitHub issue: https://github.com/maxisoft/ASFFreeGames/issues/28</remarks>
	private static async ValueTask<string> GetFeed(SimpleHttpClient httpClient, CancellationToken cancellationToken, uint retry = 5) {
		HttpStreamResponse? response = null;

		Dictionary<string, string> headers = new() {
			{ "Pragma", "no-cache" },
			{ "Cache-Control", "no-cache" },
			{ "Accept", "application/atom+xml, application/xml;q=0.9, */*;q=0.8" },
			{ "Sec-Fetch-Site", "none" },
			{ "Sec-Fetch-Mode", "no-cors" },
			{ "Sec-Fetch-Dest", "empty" },
			{ "x-sec-fetch-dest", "empty" },
			{ "x-sec-fetch-mode", "no-cors" },
			{ "x-sec-fetch-site", "none" }
		};

		for (int t = 0; t < retry; t++) {
			try {
#pragma warning disable CA2000
				response = await httpClient.GetStreamAsync(GetUrl(), headers, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000

				if (await HandleTooManyRequest(response, cancellationToken: cancellationToken).ConfigureAwait(false)) {
					continue;
				}

				if (!response.StatusCode.IsSuccessCode()) {
					throw new RedditServerException($"reddit http error code is {response.StatusCode}", response.StatusCode);
				}

				string feed = await response.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

				if (string.IsNullOrWhiteSpace(feed)) {
					throw new RedditServerException("empty response", response.StatusCode);
				}

				if (!feed.Contains("<feed", StringComparison.Ordinal)) {
					throw new RedditServerException("invalid response", response.StatusCode);
				}

				return feed;
			}
			catch (RedditServerException e) when (e.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) {
				// retrying a blocked or rate limited request right away only makes things worse, let the next strategy handle it
				throw;
			}
			catch (Exception e) when (e is IOException or RedditServerException or HttpRequestException) {
				// If it's the last retry, re-throw the original Exception
				if (t + 1 == retry) {
					throw;
				}

				cancellationToken.ThrowIfCancellationRequested();
			}
			finally {
				if (response is not null) {
					await response.DisposeAsync().ConfigureAwait(false);
				}

				response = null;
			}

			await Task.Delay((2 << (t + 1)) * 100, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
		}

		throw new RedditServerException("reddit rate limit reached", HttpStatusCode.TooManyRequests);
	}

	private static Uri GetUrl() => new($"https://www.reddit.com/user/{User}.rss?sort=new&limit=100", UriKind.Absolute);

	/// <summary>
	///     Handles too many requests by checking the status code and headers of the response.
	///     If the status code is Forbidden or TooManyRequests, it checks the remaining rate limit
	///     and the reset time. If the remaining rate limit is less than or equal to 0, it delays
	///     the execution until the reset time using the cancellation token.
	/// </summary>
	/// <param name="response">The HTTP stream response to handle.</param>
	/// <param name="maxTimeToWait"></param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the request was handled & awaited, false otherwise.</returns>
	private static async ValueTask<bool> HandleTooManyRequest(HttpStreamResponse response, int maxTimeToWait = 45, CancellationToken cancellationToken = default) {
		if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) {
			if (response.Response.Headers.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? rateLimitRemaining)) {
				if (int.TryParse(rateLimitRemaining.FirstOrDefault(), out int remaining) && (remaining <= 0)) {
					if (response.Response.Headers.TryGetValues("x-ratelimit-reset", out IEnumerable<string>? rateLimitReset)
						&& float.TryParse(rateLimitReset.FirstOrDefault(), out float reset) && double.IsNormal(reset) && (0 < reset) && (reset < maxTimeToWait)) {
						try {
							await Task.Delay(TimeSpan.FromSeconds(reset), cancellationToken).ConfigureAwait(false);
						}
						catch (TaskCanceledException) {
							return false;
						}
						catch (TimeoutException) {
							return false;
						}
						catch (OperationCanceledException) {
							return false;
						}
					}

					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	///     Parses a JSON object from a stream response. Using not straightforward for ASF trimmed compatibility reasons
	/// </summary>
	/// <param name="stream">The stream response containing the JSON data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The parsed JSON object, or null if parsing fails.</returns>
	internal static async Task<JsonNode?> ParseJsonNode(HttpStreamResponse stream, CancellationToken cancellationToken) {
		string data = await stream.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		return JsonNode.Parse(data);
	}
}
