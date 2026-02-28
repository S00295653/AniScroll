using AniScroll.Shared.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AniScroll.Shared.Services
{
    // ─── Rate-limit snapshot for one API ─────────────────────────────────────────
    public class RateLimitInfo
    {
        /// <summary>Requests remaining in the current window (from X-RateLimit-Remaining).</summary>
        public int Remaining { get; set; } = -1;          // -1 = not yet known

        /// <summary>Total requests allowed per window (from X-RateLimit-Limit).</summary>
        public int Limit { get; set; } = -1;

        /// <summary>UTC time at which the window resets (from X-RateLimit-Reset or Retry-After).</summary>
        public DateTime? ResetAt { get; set; }

        /// <summary>True while we are in a rate-limited state (429 received or Remaining == 0).</summary>
        public bool IsLimited { get; set; }

        /// <summary>Seconds remaining until reset (0 when unknown or already passed).</summary>
        public int SecondsUntilReset =>
            ResetAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((ResetAt.Value - DateTime.UtcNow).TotalSeconds))
                : 0;

        /// <summary>0–100 fill percentage for a progress bar (used = Limit - Remaining).</summary>
        public double UsedPercent =>
            Limit > 0 && Remaining >= 0
                ? Math.Min(100.0, (Limit - Remaining) / (double)Limit * 100.0)
                : 0;
    }

    public class AniListService
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;

        // ── Exposed for the debug panel ─────────────────────────────────────────
        public string LastError { get; private set; } = string.Empty;

        /// <summary>Live rate-limit snapshot for the AniList GraphQL endpoint.</summary>
        public RateLimitInfo AniListRateLimit { get; } = new();

        /// <summary>Live rate-limit snapshot for the Jikan REST endpoint.</summary>
        public RateLimitInfo JikanRateLimit { get; } = new();

        // Legacy properties kept for backward compat with existing debug panel code
        public int RequestCount => AniListRateLimit.Limit > 0 && AniListRateLimit.Remaining >= 0
            ? AniListRateLimit.Limit - AniListRateLimit.Remaining
            : _legacyRequestCount;
        public const int RequestLimit = 90;   // AniList default window size

        private int _legacyRequestCount = 0;

        private const string ANILIST_ENDPOINT  = "https://graphql.anilist.co";
        private const int    RATE_LIMIT_FALLBACK_SECONDS = 60;

        public AniListService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _random     = new Random();
        }

        // ── Public helpers ───────────────────────────────────────────────────────

        public bool IsRateLimited() => AniListRateLimit.IsLimited;

        public int GetRateLimitSecondsRemaining() => AniListRateLimit.SecondsUntilReset;

        // ─── Jikan search ────────────────────────────────────────────────────────

        public async Task<List<JikanSearchResult>> SearchJikanAsync(string query)
        {
            try
            {
                var url = "https://api.jikan.moe/v4/anime?q="
                        + Uri.EscapeDataString(query)
                        + "&limit=20&sfw=true";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                // ── Parse Jikan rate-limit headers ──────────────────────────────
                ParseRateLimitHeaders(response, JikanRateLimit);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    JikanRateLimit.IsLimited = true;
                    return new List<JikanSearchResult>();
                }

                JikanRateLimit.IsLimited = false;

                if (!response.IsSuccessStatusCode) return new List<JikanSearchResult>();

                var json  = await response.Content.ReadAsStringAsync();
                var data  = JObject.Parse(json);
                var items = data["data"];
                if (items == null || !items.HasValues) return new List<JikanSearchResult>();

                var queryNorm = query.Trim().ToLowerInvariant();
                var raw = new List<JikanSearchResult>();

                foreach (var item in items)
                {
                    if (item == null || item.Type == JTokenType.Null) continue;

                    var scoreToken   = item["score"];
                    double numericScore = (scoreToken != null && scoreToken.Type != JTokenType.Null)
                        ? scoreToken.Value<double>() : 0;
                    string scoreStr = numericScore > 0 ? numericScore.ToString() : "N/A";

                    var titleRaw     = item["title"]?.ToString() ?? "";
                    var titleEnglish = item["title_english"]?.ToString() ?? "";

                    raw.Add(new JikanSearchResult
                    {
                        MalId          = item["mal_id"]?.Value<int>() ?? 0,
                        Title          = titleRaw,
                        ImageUrl       = item["images"]?["jpg"]?["image_url"]?.ToString() ?? "",
                        Score          = scoreStr,
                        Type           = item["type"]?.ToString() ?? "",
                        Episodes       = item["episodes"]?.Value<int?>(),
                        RelevanceScore = ComputeRelevance(queryNorm, titleRaw, titleEnglish, numericScore)
                    });
                }

                raw.Sort((a, b) => b.RelevanceScore.CompareTo(a.RelevanceScore));
                int displayCount = DetermineDisplayCount(raw, queryNorm);
                return raw.Take(displayCount).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Jikan search error: " + ex.Message);
                return new List<JikanSearchResult>();
            }
        }

        // ─── Single anime lookups (WITH relations) ────────────────────────────────

        public async Task<AnimeCard?> GetAnimeByMalIdAsync(int malId)
        {
            if (IsRateLimited()) return null;
            try
            {
                var fields = GetAnimeFieldsWithRelations();
                var query  = "query { Media(idMal: " + malId + ", type: ANIME) { " + fields + " } }";
                System.Diagnostics.Debug.WriteLine("AniList lookup MAL ID: " + malId);
                var resp = await PostGraphQL(query);
                if (resp == null) return null;

                var data   = JObject.Parse(resp);
                var errors = data["errors"];
                if (errors != null && errors.HasValues)
                    System.Diagnostics.Debug.WriteLine("AniList errors for MAL ID " + malId + ": " + errors);

                var media = data["data"]?["Media"];
                if (media == null || media.Type == JTokenType.Null) return null;

                var card = ParseAnimeCard(media, includeRelations: true);
                if (card == null && string.IsNullOrEmpty(LastError))
                    LastError = "ParseAnimeCard returned null — silent parsing exception";
                return card;
            }
            catch (Exception ex)
            {
                LastError = "GetAnimeByMalIdAsync exception: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(LastError);
                return null;
            }
        }

        public async Task<AnimeCard?> GetAnimeByTitleAsync(string title)
        {
            if (IsRateLimited()) return null;
            try
            {
                var safeTitle = title.Replace("\"", "\\\"");
                var fields    = GetAnimeFieldsWithRelations();
                var query     = "query { Media(search: \"" + safeTitle + "\", type: ANIME) { " + fields + " } }";
                System.Diagnostics.Debug.WriteLine("AniList title fallback: " + title);
                var resp = await PostGraphQL(query);
                if (resp == null) return null;
                var data  = JObject.Parse(resp);
                var media = data["data"]?["Media"];
                if (media == null || media.Type == JTokenType.Null) return null;
                var card = ParseAnimeCard(media, includeRelations: true);
                if (card == null && string.IsNullOrEmpty(LastError))
                    LastError = "ParseAnimeCard returned null — silent parsing exception";
                return card;
            }
            catch (Exception ex)
            {
                LastError = "GetAnimeByTitleAsync exception: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(LastError);
                return null;
            }
        }

        public async Task<AnimeCard?> GetAnimeByAniListIdAsync(int aniListId)
        {
            if (IsRateLimited()) return null;
            try
            {
                var fields = GetAnimeFieldsWithRelations();
                var query  = "query { Media(id: " + aniListId + ", type: ANIME) { " + fields + " } }";
                System.Diagnostics.Debug.WriteLine("AniList lookup by AniList ID: " + aniListId);
                var resp = await PostGraphQL(query);
                if (resp == null) return null;
                var data  = JObject.Parse(resp);
                var media = data["data"]?["Media"];
                if (media == null || media.Type == JTokenType.Null) return null;
                var card = ParseAnimeCard(media, includeRelations: true);
                if (card == null && string.IsNullOrEmpty(LastError))
                    LastError = "ParseAnimeCard returned null for AniList ID " + aniListId;
                return card;
            }
            catch (Exception ex)
            {
                LastError = "GetAnimeByAniListIdAsync exception: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(LastError);
                return null;
            }
        }

        // ─── Bulk / Random (WITHOUT relations) ───────────────────────────────────

        public async Task<AnimeLoadResult> GetBulkAnimesAsync()
        {
            if (IsRateLimited())
                return new AnimeLoadResult { Animes = new List<AnimeCard>(), IsRateLimited = true };
            try
            {
                var fields = GetAnimeFieldsNoRelations();
                var query  = @"query {
                    popular:    Page(page: 1, perPage: 50) { media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) { " + fields + @" } }
                    topScore:   Page(page: 1, perPage: 50) { media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC)      { " + fields + @" } }
                    hiddenGems: Page(page: 1, perPage: 50) { media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC) { " + fields + @" } }
                    ongoing:    Page(page: 1, perPage: 50) { media(type: ANIME, status: RELEASING,        isAdult: false, averageScore_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: TRENDING_DESC) { " + fields + @" } }
                    upcoming:   Page(page: 1, perPage: 10) { media(type: ANIME, status: NOT_YET_RELEASED, isAdult: false, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) { " + fields + @" } }
                }";

                var response = await PostGraphQL(query);
                if (response == null)
                    return new AnimeLoadResult { Animes = new List<AnimeCard>(), IsRateLimited = IsRateLimited() };

                var data          = JObject.Parse(response);
                var popularPool   = ExtractPool(data["data"]?["popular"]?["media"],    includeRelations: false);
                var topScorePool  = ExtractPool(data["data"]?["topScore"]?["media"],   includeRelations: false);
                var hiddenPool    = ExtractPool(data["data"]?["hiddenGems"]?["media"], includeRelations: false);
                var ongoingPool   = ExtractPool(data["data"]?["ongoing"]?["media"],    includeRelations: false);
                var upcomingPool  = ExtractPool(data["data"]?["upcoming"]?["media"],   includeRelations: false);

                var result = new List<AnimeCard>();
                result.AddRange(Pick(popularPool,  27));
                result.AddRange(Pick(topScorePool, 12));
                result.AddRange(Pick(hiddenPool,   12));
                result.AddRange(Pick(ongoingPool,   9));
                result.AddRange(Pick(upcomingPool,  1));
                result = result.OrderBy(_ => _random.Next()).ToList();

                System.Diagnostics.Debug.WriteLine("Bulk loaded " + result.Count + " anime");
                return new AnimeLoadResult { Animes = result, IsRateLimited = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetBulkAnimesAsync error: " + ex.Message);
                return new AnimeLoadResult { Animes = new List<AnimeCard>(), IsRateLimited = false };
            }
        }

        // ─── GraphQL core ─────────────────────────────────────────────────────────

        private async Task<string?> PostGraphQL(string query)
        {
            LastError = string.Empty;
            try
            {
                var payload = new { query };
                var json    = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine("PostGraphQL: sending, query length=" + query.Length);

                _legacyRequestCount++;
                var response = await _httpClient.PostAsync(ANILIST_ENDPOINT, content);

                System.Diagnostics.Debug.WriteLine("PostGraphQL: HTTP " + (int)response.StatusCode);

                // ── Parse AniList rate-limit headers ────────────────────────────
                ParseRateLimitHeaders(response, AniListRateLimit);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    AniListRateLimit.IsLimited   = true;
                    AniListRateLimit.Remaining   = 0;
                    LastError = "429 Rate Limited";
                    return null;
                }

                AniListRateLimit.IsLimited = false;

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    LastError = "HTTP " + (int)response.StatusCode + ": "
                              + body.Substring(0, Math.Min(200, body.Length));
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException;
                LastError = "EXCEPTION " + ex.GetType().Name + ": " + ex.Message
                    + (inner != null ? " | Inner: " + inner.GetType().Name + ": " + inner.Message : "");
                System.Diagnostics.Debug.WriteLine("PostGraphQL error: " + LastError);
                return null;
            }
        }

        // ─── Header parser (shared for AniList + Jikan) ──────────────────────────

        /// <summary>
        /// Reads X-RateLimit-Remaining, X-RateLimit-Limit, X-RateLimit-Reset and
        /// Retry-After from <paramref name="response"/> and updates <paramref name="info"/>.
        /// Safe to call even when headers are absent.
        /// </summary>
        private static void ParseRateLimitHeaders(HttpResponseMessage response, RateLimitInfo info)
        {
            // ── Remaining ──────────────────────────────────────────────────────
            if (TryGetHeader(response, "X-RateLimit-Remaining", out var remainingStr)
                && int.TryParse(remainingStr, out var remaining))
            {
                info.Remaining = remaining;
            }

            // ── Limit ──────────────────────────────────────────────────────────
            if (TryGetHeader(response, "X-RateLimit-Limit", out var limitStr)
                && int.TryParse(limitStr, out var limit))
            {
                info.Limit = limit;
            }

            // ── Reset timestamp ────────────────────────────────────────────────
            // AniList / most APIs: X-RateLimit-Reset  = Unix timestamp (seconds)
            if (TryGetHeader(response, "X-RateLimit-Reset", out var resetStr)
                && long.TryParse(resetStr, out var resetUnix))
            {
                info.ResetAt = DateTimeOffset.FromUnixTimeSeconds(resetUnix).UtcDateTime;
            }
            // Fallback: Retry-After = seconds-delta  OR  HTTP-date
            else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                     && TryGetHeader(response, "Retry-After", out var retryAfter))
            {
                if (int.TryParse(retryAfter, out var delta))
                    info.ResetAt = DateTime.UtcNow.AddSeconds(delta);
                else if (DateTimeOffset.TryParse(retryAfter, out var dto))
                    info.ResetAt = dto.UtcDateTime;
                else
                    info.ResetAt = DateTime.UtcNow.AddSeconds(RATE_LIMIT_FALLBACK_SECONDS);
            }

            // If we got a 429 but have no reset info yet, default to 60 s from now
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                && info.ResetAt == null)
            {
                info.ResetAt = DateTime.UtcNow.AddSeconds(RATE_LIMIT_FALLBACK_SECONDS);
            }
        }

        private static bool TryGetHeader(HttpResponseMessage response, string name, out string value)
        {
            value = string.Empty;
            if (response.Headers.TryGetValues(name, out var vals))
            {
                value = vals.FirstOrDefault() ?? string.Empty;
                return !string.IsNullOrEmpty(value);
            }
            // Some APIs put rate-limit headers in the content headers
            if (response.Content?.Headers.TryGetValues(name, out var cVals) == true)
            {
                value = cVals.FirstOrDefault() ?? string.Empty;
                return !string.IsNullOrEmpty(value);
            }
            return false;
        }

        // ─── Field sets ──────────────────────────────────────────────────────────

        private string GetAnimeFieldsWithRelations() => @"
            id
            title { romaji english native }
            coverImage { extraLarge large }
            bannerImage
            format
            averageScore
            genres
            episodes
            duration
            source
            description
            season
            seasonYear
            status
            startDate { year month day }
            endDate   { year month day }
            popularity
            favourites
            nextAiringEpisode { episode timeUntilAiring }
            studios(isMain: true) { nodes { id name } }
            relations {
                edges {
                    relationType
                    node {
                        id type
                        title { romaji english }
                        coverImage { large }
                        format status
                    }
                }
            }
            trailer { site id }
            tags { name rank isMediaSpoiler }
            externalLinks { url site type color icon }
            rankings { rank type context allTime season year }
        ";

        private string GetAnimeFieldsNoRelations() => @"
            id
            title { romaji english native }
            coverImage { extraLarge large }
            bannerImage
            format
            averageScore
            genres
            episodes
            duration
            source
            description
            season
            seasonYear
            status
            startDate { year month day }
            endDate   { year month day }
            popularity
            favourites
            nextAiringEpisode { episode timeUntilAiring }
            studios(isMain: true) { nodes { id name } }
            trailer { site id }
            tags { name rank isMediaSpoiler }
            externalLinks { url site type color icon }
            rankings { rank type context allTime season year }
        ";

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private List<AnimeCard> ExtractPool(JToken? arr, bool includeRelations)
        {
            var pool = new List<AnimeCard>();
            if (arr == null || !arr.HasValues) return pool;
            foreach (var m in arr)
            {
                if (m == null || m.Type == JTokenType.Null) continue;
                try
                {
                    var a = ParseAnimeCard(m, includeRelations);
                    if (a != null) pool.Add(a);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Parse error: " + ex.Message); }
            }
            return pool;
        }

        private List<AnimeCard> Pick(List<AnimeCard> pool, int count)
        {
            if (pool.Count == 0) return new List<AnimeCard>();
            if (count >= pool.Count) return new List<AnimeCard>(pool);
            var copy = new List<AnimeCard>(pool);
            var out2 = new List<AnimeCard>(count);
            for (int i = 0; i < count; i++)
            {
                int idx = _random.Next(copy.Count);
                out2.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return out2;
        }

        public async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            if (IsRateLimited()) return null;
            try
            {
                int randomPage = _random.Next(1, 80);
                int rand       = _random.Next(100);
                string filter  = rand < 45
                    ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: POPULARITY_DESC"
                    : rand < 65
                    ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: SCORE_DESC"
                    : rand < 80
                    ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: SCORE_DESC"
                    : rand < 95
                    ? "type: ANIME, status: RELEASING, isAdult: false, averageScore_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: TRENDING_DESC"
                    : "type: ANIME, status: NOT_YET_RELEASED, isAdult: false, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: POPULARITY_DESC";

                var q    = "query { Page(page: " + randomPage + ", perPage: 1) { media(" + filter + ") { " + GetAnimeFieldsNoRelations() + " } } }";
                var resp = await PostGraphQL(q);
                if (resp == null) return null;
                var data = JObject.Parse(resp);
                var arr  = data["data"]?["Page"]?["media"];
                if (arr == null || !arr.HasValues) return null;
                return ParseAnimeCard(arr[0]!, includeRelations: false);
            }
            catch { return null; }
        }

        // ─── Parsing ─────────────────────────────────────────────────────────────

        private AnimeCard? ParseAnimeCard(JToken m, bool includeRelations)
        {
            try
            {
                var titleObj   = m["title"];
                string title   = !string.IsNullOrEmpty(titleObj?["english"]?.ToString())
                    ? titleObj!["english"]!.ToString()
                    : titleObj?["romaji"]?.ToString() ?? "Unknown";
                string nativeTitle = titleObj?["native"]?.ToString() ?? "";

                var cover     = m["coverImage"];
                string imageUrl = cover?["extraLarge"]?.ToString() ?? cover?["large"]?.ToString() ?? "";
                string score  = m["averageScore"] != null && m["averageScore"]!.Type != JTokenType.Null
                    ? m["averageScore"]!.ToString() : "N/A";

                string description = m["description"]?.ToString() ?? "";
                description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", "");

                string season = m["season"]?.ToString() ?? "";
                int? year     = m["seasonYear"] != null && m["seasonYear"]!.Type != JTokenType.Null
                    ? m["seasonYear"]!.Value<int>() : null;
                string status = m["status"]?.ToString() ?? "";

                int? nextEp = null, timeUntil = null;
                var nae = m["nextAiringEpisode"];
                if (nae != null && nae.Type != JTokenType.Null)
                {
                    nextEp    = nae["episode"]?.Value<int?>();
                    timeUntil = nae["timeUntilAiring"]?.Value<int?>();
                }

                string epDisplay = "N/A";
                if (status == "RELEASING")
                {
                    int? total = m["episodes"] != null && m["episodes"]!.Type != JTokenType.Null
                        ? m["episodes"]!.Value<int?>() : null;
                    if (nextEp.HasValue)
                    {
                        int done = nextEp.Value - 1;
                        epDisplay = total.HasValue ? done + "/" + total.Value : done + "+";
                    }
                    else if (total.HasValue) epDisplay = total.Value.ToString();
                }
                else if (status == "FINISHED" || status == "NOT_YET_RELEASED")
                {
                    if (m["episodes"] != null && m["episodes"]!.Type != JTokenType.Null)
                        epDisplay = m["episodes"]!.ToString();
                }

                var genres = new List<string>();
                var ga     = m["genres"];
                if (ga != null && ga.HasValues)
                    for (int i = 0; i < Math.Min(3, ga.Count()); i++)
                    {
                        if (ga[i] == null || ga[i]!.Type == JTokenType.Null) continue;
                        genres.Add(ga[i]!.ToString());
                    }

                var studios = new List<AnimeStudio>();
                var sn      = m["studios"]?["nodes"];
                if (sn != null && sn.HasValues)
                    foreach (var s in sn)
                    {
                        if (s == null || s.Type == JTokenType.Null) continue;
                        studios.Add(new AnimeStudio
                        {
                            Id   = s["id"]?.Value<int>() ?? 0,
                            Name = s["name"]?.ToString() ?? ""
                        });
                    }

                var relations = new List<AnimeRelation>();
                if (includeRelations)
                {
                    var re = m["relations"]?["edges"];
                    if (re != null && re.HasValues)
                        foreach (var edge in re)
                        {
                            if (edge == null || edge.Type == JTokenType.Null) continue;
                            var node = edge["node"];
                            if (node == null || node.Type == JTokenType.Null) continue;
                            var rt        = node["title"];
                            string relTitle = !string.IsNullOrEmpty(rt?["english"]?.ToString())
                                ? rt!["english"]!.ToString()
                                : rt?["romaji"]?.ToString() ?? "";
                            relations.Add(new AnimeRelation
                            {
                                Id           = node["id"]?.Value<int>() ?? 0,
                                Type         = node["type"]?.ToString() ?? "",
                                RelationType = FormatRelationType(edge["relationType"]?.ToString() ?? ""),
                                Title        = relTitle,
                                ImageUrl     = node["coverImage"]?["large"]?.ToString() ?? "",
                                Format       = FormatFormat(node["format"]?.ToString() ?? ""),
                                Status       = node["status"]?.ToString() ?? ""
                            });
                        }
                }

                string trailerUrl = "";
                var tr = m["trailer"];
                if (tr != null && tr.Type != JTokenType.Null)
                {
                    var site     = tr["site"]?.ToString() ?? "";
                    var trailerId = tr["id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(trailerId) &&
                        site.StartsWith("youtube", StringComparison.OrdinalIgnoreCase))
                        trailerUrl = "https://www.youtube.com/watch?v=" + trailerId;
                }

                var tags = new List<AnimeTag>();
                var ta   = m["tags"];
                if (ta != null && ta.HasValues)
                    foreach (var t in ta)
                    {
                        if (t == null || t.Type == JTokenType.Null) continue;
                        tags.Add(new AnimeTag
                        {
                            Name           = t["name"]?.ToString() ?? "",
                            Rank           = t["rank"]?.Value<int>() ?? 0,
                            IsMediaSpoiler = t["isMediaSpoiler"]?.Value<bool>() ?? false
                        });
                    }

                var extLinks = new List<AnimeExternalLink>();
                var el       = m["externalLinks"];
                if (el != null && el.HasValues)
                    foreach (var link in el)
                    {
                        if (link == null || link.Type == JTokenType.Null) continue;
                        extLinks.Add(new AnimeExternalLink
                        {
                            Url   = link["url"]?.ToString() ?? "",
                            Site  = link["site"]?.ToString() ?? "",
                            Type  = link["type"]?.ToString() ?? "",
                            Color = link["color"]?.ToString() ?? "",
                            Icon  = link["icon"]?.ToString() ?? ""
                        });
                    }

                var rankings = new List<AnimeRanking>();
                var rk       = m["rankings"];
                if (rk != null && rk.HasValues)
                    foreach (var r in rk)
                    {
                        if (r == null || r.Type == JTokenType.Null) continue;
                        rankings.Add(new AnimeRanking
                        {
                            Rank    = r["rank"]?.Value<int>() ?? 0,
                            Type    = r["type"]?.ToString() ?? "",
                            Context = r["context"]?.ToString() ?? "",
                            AllTime = r["allTime"]?.Value<bool>() ?? false,
                            Season  = r["season"]?.ToString() ?? "",
                            Year    = r["year"]?.Value<int?>()
                        });
                    }

                return new AnimeCard
                {
                    Id          = m["id"]?.Value<int>() ?? 0,
                    Title       = title,
                    NativeTitle = nativeTitle,
                    ImageUrl    = imageUrl,
                    BannerUrl   = m["bannerImage"]?.ToString() ?? "",
                    Score       = score,
                    Description = description,
                    Season      = season,
                    Year        = year,
                    Status      = status,
                    Episodes    = epDisplay,
                    Genres      = genres,
                    Format      = FormatFormat(m["format"]?.ToString() ?? ""),
                    Source      = FormatSource(m["source"]?.ToString() ?? ""),
                    Duration    = m["duration"]?.Value<int?>(),
                    StartDate   = FormatDate(m["startDate"]),
                    EndDate     = FormatDate(m["endDate"]),
                    Popularity  = m["popularity"]?.Value<int?>(),
                    Favourites  = m["favourites"]?.Value<int?>(),
                    Studios     = studios,
                    Relations   = relations,
                    TrailerUrl  = trailerUrl,
                    Tags        = tags,
                    ExternalLinks = extLinks,
                    Rankings    = rankings,
                    NextAiringEpisodeNum  = nextEp,
                    NextAiringTimeUntil   = timeUntil
                };
            }
            catch (Exception ex)
            {
                LastError = "ParseAnimeCard exception: " + ex.GetType().Name + ": " + ex.Message;
                System.Diagnostics.Debug.WriteLine(LastError);
                return null;
            }
        }

        // ─── Jikan relevance scoring ──────────────────────────────────────────────

        private static double ComputeRelevance(
            string queryNorm, string title, string titleEnglish, double animeScore)
        {
            double score = 0;
            var t1 = title.Trim().ToLowerInvariant();
            var t2 = titleEnglish.Trim().ToLowerInvariant();

            if (t1 == queryNorm || t2 == queryNorm)                                     score += 60;
            else if (t1.StartsWith(queryNorm) || t2.StartsWith(queryNorm))              score += 40;
            else if (ContainsWholeWord(t1, queryNorm) || ContainsWholeWord(t2, queryNorm)) score += 25;
            else if (t1.Contains(queryNorm) || t2.Contains(queryNorm))                  score += 12;

            double lenRatio = queryNorm.Length / Math.Max(1.0, t1.Length);
            score += lenRatio * 10;
            score += animeScore;
            return score;
        }

        private static bool ContainsWholeWord(string text, string word)
        {
            int idx = text.IndexOf(word, StringComparison.Ordinal);
            if (idx < 0) return false;
            bool beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            int  end      = idx + word.Length;
            bool afterOk  = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            return beforeOk && afterOk;
        }

        private static int DetermineDisplayCount(List<JikanSearchResult> sorted, string query)
        {
            if (sorted.Count == 0) return 0;
            double best      = sorted[0].RelevanceScore;
            double threshold = best * 0.45;
            int    qualified = sorted.Count(r => r.RelevanceScore >= threshold);
            int    max       = best >= 60 ? 4 : best >= 40 ? 6 : best >= 20 ? 8 : 10;
            return Math.Min(qualified, max);
        }

        // ─── Format helpers ───────────────────────────────────────────────────────

        private string FormatDate(JToken? d)
        {
            if (d == null) return "";
            int? y = d["year"]?.Value<int?>(), mo = d["month"]?.Value<int?>(), day = d["day"]?.Value<int?>();
            if (!y.HasValue) return "";
            string[] months = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                    "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            if (mo.HasValue && day.HasValue) return months[mo.Value] + " " + day.Value + ", " + y;
            if (mo.HasValue) return months[mo.Value] + " " + y;
            return y.Value.ToString();
        }

        private string FormatFormat(string f) => f switch
        {
            "TV"       => "TV Series", "TV_SHORT" => "TV Short",
            "MOVIE"    => "Movie",     "SPECIAL"  => "Special",
            "OVA"      => "OVA",       "ONA"      => "ONA",
            "MUSIC"    => "Music",     _ => f
        };

        private string FormatSource(string s) => s switch
        {
            "ORIGINAL"            => "Original",
            "MANGA"               => "Manga",
            "LIGHT_NOVEL"         => "Light Novel",
            "VISUAL_NOVEL"        => "Visual Novel",
            "VIDEO_GAME"          => "Video Game",
            "WEB_NOVEL"           => "Web Novel",
            "NOVEL"               => "Novel",
            "ANIME"               => "Anime",
            "GAME"                => "Game",
            "COMIC"               => "Comic",
            "PICTURE_BOOK"        => "Picture Book",
            "MULTIMEDIA_PROJECT"  => "Multimedia Project",
            "OTHER"               => "Other",
            _ => s
        };

        private string FormatRelationType(string rt) => rt switch
        {
            "ADAPTATION" => "Adaptation", "PREQUEL"     => "Prequel",
            "SEQUEL"     => "Sequel",     "PARENT"      => "Parent",
            "SIDE_STORY" => "Side Story", "CHARACTER"   => "Character",
            "SUMMARY"    => "Summary",    "ALTERNATIVE" => "Alternative",
            "SPIN_OFF"   => "Spin-off",   "SOURCE"      => "Source",
            "COMPILATION"=> "Compilation","CONTAINS"    => "Contains",
            "OTHER"      => "Other",      _ => rt
        };

        public async Task<AnimeLoadResult> GetMultipleAnimesAsync(int count)
        {
            if (IsRateLimited())
                return new AnimeLoadResult { Animes = new List<AnimeCard>(), IsRateLimited = true };
            if (count > 10)
            {
                var bulk = await GetBulkAnimesAsync();
                if (bulk.Animes.Count > count) bulk.Animes = bulk.Animes.Take(count).ToList();
                return bulk;
            }
            var results = new List<AnimeCard>();
            for (int i = 0; i < count; i++)
            {
                if (IsRateLimited()) break;
                var anime = await GetRandomAnimeAsync();
                if (anime != null) results.Add(anime);
                await Task.Delay(100);
            }
            return new AnimeLoadResult { Animes = results, IsRateLimited = IsRateLimited() };
        }
    }

    public class AnimeLoadResult
    {
        public List<AnimeCard> Animes { get; set; } = new List<AnimeCard>();
        public bool IsRateLimited { get; set; }
    }
}