using AniScroll.Shared.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AniScroll.Shared.Services
{
    public class RateLimitInfo
    {
        public int Remaining { get; set; } = -1;
        public int Limit { get; set; } = -1;
        public DateTime? ResetAt { get; set; }
        public bool IsLimited { get; set; }
        public int RequestsSent { get; set; } = 0;
        public int SecondsUntilReset => ResetAt.HasValue ? Math.Max(0, (int)Math.Ceiling((ResetAt.Value - DateTime.UtcNow).TotalSeconds)) : 0;
        public double UsedPercent { get { if (Limit > 0 && Remaining >= 0) return Math.Min(100.0, (Limit - Remaining) / (double)Limit * 100.0); if (Limit > 0 && RequestsSent > 0) return Math.Min(100.0, RequestsSent / (double)Limit * 100.0); return 0; } }
        public string Label { get { if (Limit > 0 && Remaining >= 0) return $"{Remaining} / {Limit} remaining"; if (RequestsSent > 0) return $"{RequestsSent} sent (no header data)"; return "No data yet"; } }
    }

    public class AniListService
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;
        public string LastError { get; private set; } = string.Empty;
        public RateLimitInfo AniListRateLimit { get; } = new() { Limit = 30 };
        public RateLimitInfo JikanRateLimit { get; } = new() { Limit = 60 };
        public int RequestCount => AniListRateLimit.RequestsSent;
        public const int RequestLimit = 30;
        private const string ANILIST_ENDPOINT = "https://graphql.anilist.co";
        private const int RATE_LIMIT_FALLBACK_SECONDS = 60;

        private static List<string>? _cachedGenres;
        private static List<string>? _cachedTags;
        private static List<string>? _cachedStudios;
        private static bool _studiosLoading;

        private static readonly HashSet<int> _seenIds = new();
        private static int _batchPage = 1;

        public void ClearSeenIds() { _seenIds.Clear(); _batchPage = 1; }
        private string BuildIdNotInFilter() { if (_seenIds.Count == 0) return ""; return $", id_not_in: [{string.Join(",", _seenIds)}]"; }
        private void RegisterSeen(IEnumerable<AnimeCard> cards) { foreach (var a in cards) if (a.Id > 0) _seenIds.Add(a.Id); }

        public List<string>? GetCachedGenres() => _cachedGenres;
        public List<string>? GetCachedTags() => _cachedTags;
        public List<string>? GetCachedStudios() => _cachedStudios;
        public bool IsStudiosLoading => _studiosLoading;

        private static readonly List<string> _genresFallback = new() { "Action", "Adventure", "Comedy", "Drama", "Ecchi", "Fantasy", "Horror", "Hentai", "Mahou Shoujo", "Mecha", "Music", "Mystery", "Psychological", "Romance", "Sci-Fi", "Slice of Life", "Sports", "Supernatural", "Thriller" };

        public AniListService(HttpClient httpClient) { _httpClient = httpClient; _random = new Random(); }

        public bool IsRateLimited()
        {
            if (!AniListRateLimit.IsLimited) return false;
            if (AniListRateLimit.ResetAt.HasValue && DateTime.UtcNow >= AniListRateLimit.ResetAt.Value)
            { AniListRateLimit.IsLimited = false; AniListRateLimit.ResetAt = null; AniListRateLimit.Remaining = -1; }
            return AniListRateLimit.IsLimited;
        }

        public int GetRateLimitSecondsRemaining() => AniListRateLimit.SecondsUntilReset;

        public async Task FetchGenresAndTagsAsync()
        {
            if (_cachedGenres == null) { var resp = await PostGraphQL("query { GenreCollection }"); if (resp != null) { var arr = JObject.Parse(resp)?["data"]?["GenreCollection"]; if (arr != null) _cachedGenres = arr.Select(g => g.ToString()).Where(g => !string.IsNullOrEmpty(g)).OrderBy(g => g).ToList(); } _cachedGenres ??= _genresFallback; }
            if (_cachedTags == null) { var resp = await PostGraphQL("query { MediaTagCollection { name } }"); if (resp != null) { var tags = JObject.Parse(resp)?["data"]?["MediaTagCollection"]; if (tags != null) _cachedTags = tags.Select(t => t["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); } _cachedTags ??= new List<string>(); }
        }

        public async Task FetchAllStudiosAsync(Func<Task>? onBatch = null)
        {
            if (_cachedStudios != null || _studiosLoading) return;
            _studiosLoading = true;
            const int BATCH_SIZE = 8; const int BATCH_DELAY = 4000;
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase); int batchStart = 1;
            try
            {
                while (true)
                {
                    while (IsRateLimited()) await Task.Delay(2000);
                    var sb = new System.Text.StringBuilder("query {");
                    for (int i = 0; i < BATCH_SIZE; i++) { int p = batchStart + i; sb.Append($" p{p}: Page(page: {p}, perPage: 50) {{ pageInfo {{ hasNextPage }} studios {{ name }} }}"); }
                    sb.Append(" }");
                    string? resp = null; try { resp = await PostGraphQL(sb.ToString()); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Studios batch {batchStart}: {ex.Message}"); }
                    if (resp == null) break;
                    JObject? data = null; try { data = JObject.Parse(resp)?["data"] as JObject; } catch { break; }
                    if (data == null) break;
                    bool anyHasNext = false;
                    for (int i = 0; i < BATCH_SIZE; i++) { int p = batchStart + i; var pd = data[$"p{p}"]; if (pd == null) continue; var stds = pd["studios"]; if (stds != null) foreach (var s in stds) { var n = s["name"]?.ToString(); if (!string.IsNullOrWhiteSpace(n)) all.Add(n); } if (pd["pageInfo"]?["hasNextPage"]?.Value<bool>() == true) anyHasNext = true; }
                    _cachedStudios = all.OrderBy(x => x).ToList(); if (onBatch != null) await onBatch(); if (!anyHasNext) break;
                    batchStart += BATCH_SIZE; await Task.Delay(BATCH_DELAY);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Studios fatal: {ex.Message}"); }
            finally { _cachedStudios = all.OrderBy(x => x).ToList(); _studiosLoading = false; if (onBatch != null) await onBatch(); }
        }

        public async Task<List<JikanSearchResult>> SearchJikanAsync(string query)
        {
            const int maxRetries = 3; const int retryDelayMs = 800;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var url = "https://api.jikan.moe/v4/anime?q=" + Uri.EscapeDataString(query) + "&limit=20&sfw=true";
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    JikanRateLimit.RequestsSent++;
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    ParseRateLimitHeaders(response, JikanRateLimit, new[] { "X-RateLimit-Limit", "X-RateLimit-Limit-EachMinute", "X-Ratelimit-Limit-60" }, new[] { "X-RateLimit-Remaining", "X-RateLimit-Remaining-EachMinute", "X-Ratelimit-Remaining-60" }, new[] { "X-RateLimit-Reset", "Retry-After" });
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) { JikanRateLimit.IsLimited = true; return await SearchAniListAsync(query); }
                    if ((int)response.StatusCode >= 500) { if (attempt < maxRetries) await Task.Delay(retryDelayMs * attempt); continue; }
                    JikanRateLimit.IsLimited = false;
                    if (!response.IsSuccessStatusCode) return new List<JikanSearchResult>();
                    var json = await response.Content.ReadAsStringAsync(); var data = JObject.Parse(json); var items = data["data"];
                    if (items == null || !items.HasValues) return new List<JikanSearchResult>();
                    var queryNorm = query.Trim().ToLowerInvariant(); var raw = new List<JikanSearchResult>();
                    foreach (var item in items)
                    {
                        if (item == null || item.Type == JTokenType.Null) continue;
                        var scoreToken = item["score"]; double numScore = scoreToken != null && scoreToken.Type != JTokenType.Null ? scoreToken.Value<double>() : 0;
                        var titleRaw = item["title"]?.ToString() ?? ""; var titleEnglish = item["title_english"]?.ToString() ?? "";
                        raw.Add(new JikanSearchResult { MalId = item["mal_id"]?.Value<int>() ?? 0, Title = titleRaw, ImageUrl = item["images"]?["jpg"]?["large_image_url"]?.ToString() ?? item["images"]?["jpg"]?["image_url"]?.ToString() ?? "", Score = numScore > 0 ? numScore.ToString() : "N/A", Type = item["type"]?.ToString() ?? "", Episodes = item["episodes"]?.Value<int?>(), RelevanceScore = ComputeRelevance(queryNorm, titleRaw, titleEnglish, numScore) });
                    }
                    raw.Sort((a, b) => b.RelevanceScore.CompareTo(a.RelevanceScore));
                    return raw.Take(DetermineDisplayCount(raw, queryNorm)).ToList();
                }
                catch (OperationCanceledException) { if (attempt < maxRetries) await Task.Delay(retryDelayMs * attempt); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Jikan: " + ex.Message); if (attempt < maxRetries) await Task.Delay(retryDelayMs * attempt); }
            }
            return await SearchAniListAsync(query);
        }

        private async Task<List<JikanSearchResult>> SearchAniListAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new();
            try
            {
                var safe = query.Replace("\"", "\\\"");
                var gql = $"query {{ Page(perPage: 12) {{ media(search: \"{safe}\", type: ANIME, isAdult: false) {{ id idMal title {{ romaji english }} coverImage {{ large }} format averageScore episodes }} }} }}";
                var resp = await PostGraphQL(gql); if (resp == null) return new();
                var items = JObject.Parse(resp)?["data"]?["Page"]?["media"]; if (items == null || !items.HasValues) return new();
                var results = new List<JikanSearchResult>();
                foreach (var item in items)
                {
                    if (item == null || item.Type == JTokenType.Null) continue;
                    var titleObj = item["title"]; string title = !string.IsNullOrEmpty(titleObj?["english"]?.ToString()) ? titleObj!["english"]!.ToString() : titleObj?["romaji"]?.ToString() ?? "";
                    var scoreRaw = item["averageScore"]; double score = scoreRaw != null && scoreRaw.Type != JTokenType.Null ? scoreRaw.Value<double>() / 10.0 : 0;
                    results.Add(new JikanSearchResult { MalId = item["idMal"]?.Value<int>() ?? 0, Title = title, ImageUrl = item["coverImage"]?["large"]?.ToString() ?? "", Score = score > 0 ? score.ToString("F1") : "N/A", Type = FormatFormat(item["format"]?.ToString() ?? ""), Episodes = item["episodes"]?.Value<int?>() });
                }
                return results;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("AniList search fallback: " + ex.Message); return new(); }
        }

        public async Task<AnimeCard?> GetAnimeByMalIdAsync(int malId) { if (IsRateLimited()) return null; try { var query = "query { Media(idMal: " + malId + ", type: ANIME) { " + GetAnimeFieldsWithRelations() + " } }"; var resp = await PostGraphQL(query); if (resp == null) return null; var data = JObject.Parse(resp); var errors = data["errors"]; if (errors != null && errors.HasValues) System.Diagnostics.Debug.WriteLine("AniList errors MAL " + malId + ": " + errors); var media = data["data"]?["Media"]; if (media == null || media.Type == JTokenType.Null) return null; var card = ParseAnimeCard(media, true); if (card == null && string.IsNullOrEmpty(LastError)) LastError = "ParseAnimeCard returned null"; return card; } catch (Exception ex) { LastError = "GetAnimeByMalIdAsync: " + ex.Message; return null; } }
        public async Task<AnimeCard?> GetAnimeByTitleAsync(string title) { if (IsRateLimited()) return null; try { var safe = title.Replace("\"", "\\\""); var query = "query { Media(search: \"" + safe + "\", type: ANIME) { " + GetAnimeFieldsWithRelations() + " } }"; var resp = await PostGraphQL(query); if (resp == null) return null; var media = JObject.Parse(resp)?["data"]?["Media"]; if (media == null || media.Type == JTokenType.Null) return null; return ParseAnimeCard(media, true); } catch (Exception ex) { LastError = "GetAnimeByTitleAsync: " + ex.Message; return null; } }
        public async Task<AnimeCard?> GetAnimeByAniListIdAsync(int id) { if (IsRateLimited()) return null; try { var query = "query { Media(id: " + id + ", type: ANIME) { " + GetAnimeFieldsWithRelations() + " } }"; var resp = await PostGraphQL(query); if (resp == null) return null; var media = JObject.Parse(resp)?["data"]?["Media"]; if (media == null || media.Type == JTokenType.Null) return null; return ParseAnimeCard(media, true); } catch (Exception ex) { LastError = "GetAnimeByAniListIdAsync: " + ex.Message; return null; } }

        // ═══════════════════════════════════════════════════════════════════════
        //  BULK LOAD — Instagram-style recommendation engine
        // ═══════════════════════════════════════════════════════════════════════
        //
        //  Single GraphQL request, 6 pools, adapts to user preferences.
        //
        //  COLD START (new user, HasData=false):
        //    popular × 27 · topScore × 12 · hiddenGems × 12 · ongoing × 8 · upcoming × 1
        //
        //  PERSONALISED (HasData=true):
        //    Pool   Genre filter          Sort               Pick  Purpose
        //    ─────────────────────────────────────────────────────────────────────
        //    1 prefPop    genre_in[fav2]  POPULARITY_DESC    20    Known taste, popular
        //    2 prefRated  genre_in[fav2]  SCORE_DESC         10    Known taste, acclaimed
        //    3 prefNiche  genre_in[fav+sec] niche<30k        8     Hidden gems, your taste
        //    4 discovery  genre_in[disc]  POPULARITY_DESC    10    "You might also like"
        //    5 ongoing    (none)          TRENDING_DESC      10    Time-relevant airing
        //    6 upcoming   (none)          POPULARITY_DESC    1     Teaser
        //    ─────────────────────────────────────────────────────────────────────
        //    Fill to 60 from leftovers if pools overlap.
        //
        //  Both paths also apply:
        //    • Page rotation  — different slice of AniList's sorted catalogue per call
        //    • id_not_in      — server-side seen-ID filter (no cap, grows with usage)
        //    • Client-side _seenIds check inside ExtractPool — final guard
        //    • Cross-pool usedIds — intra-batch deduplication
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<AnimeLoadResult> GetBulkAnimesAsync(UserPreferences? prefs = null)
        {
            if (IsRateLimited()) return new AnimeLoadResult { Animes = new(), IsRateLimited = true };
            try
            {
                var fields = GetAnimeFieldsWithRelations();
                var idNotIn = BuildIdNotInFilter();
                int mainPage = _batchPage;
                int ongoingPage = ((_batchPage - 1) % 20) + 1;
                // Discovery pool uses a page offset so it draws from a different
                // part of AniList's catalogue than the preference pools.
                int discPage = Math.Min(_batchPage + 40, 200);

                string query;

                if (prefs?.HasData == true)
                {
                    // ── Personalised query ────────────────────────────────────────
                    var excl = prefs.GenreNotIn();           // genre_not_in (hentai + disliked)
                    var g2 = prefs.GenreInFav(2);          // genre_in top-2 fav
                    var g45 = prefs.GenreInFavAndSecondary(4); // genre_in fav+secondary
                    var gDisc = prefs.GenreInDiscovery();     // genre_in discovery genres
                    int minSc = prefs.MinScoreThreshold;

                    // When there are no discovery genres we fall back to no genre filter
                    // (global popular) so the discovery pool always returns results.
                    string discFilter = string.IsNullOrEmpty(gDisc) ? "" : gDisc;

                    query = $@"query {{
                        prefPop:   Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: {minSc}, episodes_greater: 0{g2}{excl}{idNotIn}, sort: POPULARITY_DESC) {{ {fields} }} }}
                        prefRated: Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: {Math.Max(minSc, 70)}, episodes_greater: 0{g2}{excl}{idNotIn}, sort: SCORE_DESC) {{ {fields} }} }}
                        prefNiche: Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 65, episodes_greater: 0, popularity_lesser: 30000{g45}{excl}{idNotIn}, sort: SCORE_DESC) {{ {fields} }} }}
                        discovery: Page(page: {discPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0,  episodes_greater: 0{discFilter}{excl}{idNotIn}, sort: POPULARITY_DESC) {{ {fields} }} }}
                        ongoing:   Page(page: {ongoingPage}, perPage: 50) {{ media(type: ANIME, status: RELEASING, isAdult: false{excl}{idNotIn}, sort: TRENDING_DESC) {{ {fields} }} }}
                        upcoming:  Page(page: 1,             perPage: 10) {{ media(type: ANIME, status: NOT_YET_RELEASED, isAdult: false{excl}{idNotIn}, sort: POPULARITY_DESC) {{ {fields} }} }}
                    }}";
                }
                else
                {
                    // ── Cold-start / default query (unchanged weights) ────────────
                    const string excl = @", genre_not_in: [""Hentai"",""Ecchi""]";
                    query = $@"query {{
                        popular:    Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED,         isAdult: false, averageScore_greater: 0,  episodes_greater: 0{excl}{idNotIn}, sort: POPULARITY_DESC) {{ {fields} }} }}
                        topScore:   Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED,         isAdult: false, averageScore_greater: 0,  episodes_greater: 0{excl}{idNotIn}, sort: SCORE_DESC)      {{ {fields} }} }}
                        hiddenGems: Page(page: {mainPage},    perPage: 50) {{ media(type: ANIME, status: FINISHED,         isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000{excl}{idNotIn}, sort: SCORE_DESC) {{ {fields} }} }}
                        ongoing:    Page(page: {ongoingPage}, perPage: 50) {{ media(type: ANIME, status: RELEASING,        isAdult: false, averageScore_greater: 0{excl}{idNotIn}, sort: TRENDING_DESC) {{ {fields} }} }}
                        upcoming:   Page(page: 1,             perPage: 10) {{ media(type: ANIME, status: NOT_YET_RELEASED, isAdult: false{excl}{idNotIn}, sort: POPULARITY_DESC) {{ {fields} }} }}
                    }}";
                }

                var response = await PostGraphQL(query);
                if (response == null)
                    return new AnimeLoadResult { Animes = new(), IsRateLimited = IsRateLimited() };

                var data = JObject.Parse(response);

                List<AnimeCard> p1, p2, p3, p4, p5, p6;
                if (prefs?.HasData == true)
                {
                    p1 = ExtractPool(data["data"]?["prefPop"]?["media"], true);
                    p2 = ExtractPool(data["data"]?["prefRated"]?["media"], true);
                    p3 = ExtractPool(data["data"]?["prefNiche"]?["media"], true);
                    p4 = ExtractPool(data["data"]?["discovery"]?["media"], true);
                    p5 = ExtractPool(data["data"]?["ongoing"]?["media"], true);
                    p6 = ExtractPool(data["data"]?["upcoming"]?["media"], true);
                }
                else
                {
                    p1 = ExtractPool(data["data"]?["popular"]?["media"], true);
                    p2 = ExtractPool(data["data"]?["topScore"]?["media"], true);
                    p3 = ExtractPool(data["data"]?["hiddenGems"]?["media"], true);
                    p4 = ExtractPool(data["data"]?["ongoing"]?["media"], true);
                    p5 = ExtractPool(data["data"]?["upcoming"]?["media"], true);
                    p6 = new();
                }

                // Page exhaustion: both main pools empty → reset and retry once
                if (p1.Count == 0 && p2.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[Bulk] Page {_batchPage} exhausted — resetting");
                    _batchPage = 1; _seenIds.Clear();
                    return await GetBulkAnimesAsync(prefs);
                }

                _batchPage++;

                var usedIds = new HashSet<int>();
                var result = new List<AnimeCard>(60);

                void PickInto(List<AnimeCard> pool, int count)
                {
                    foreach (var a in Pick(pool.Where(a => !usedIds.Contains(a.Id)).ToList(), count))
                    { usedIds.Add(a.Id); result.Add(a); }
                }

                if (prefs?.HasData == true)
                {
                    PickInto(p1, 20);  // prefPop     — dominant: user's proven taste, popular
                    PickInto(p2, 10);  // prefRated   — acclaimed in taste zone
                    PickInto(p3, 8);   // prefNiche   — hidden gems matching taste
                    PickInto(p4, 10);  // discovery   — intentional serendipity / new genres
                    PickInto(p5, 10);  // ongoing     — time-relevant
                    PickInto(p6, 1);   // upcoming    — small teaser
                }
                else
                {
                    PickInto(p1, 27); PickInto(p2, 12); PickInto(p3, 12);
                    PickInto(p4, 8); PickInto(p5, 1);
                }

                // Fill any remaining gap (happens when pools overlap heavily)
                int deficit = 60 - result.Count;
                if (deficit > 0)
                {
                    var fillPool = new[] { p1, p2, p3, p4, p5, p6 }
                        .SelectMany(p => p)
                        .Where(a => !usedIds.Contains(a.Id))
                        .ToList();
                    PickInto(fillPool, deficit);
                }

                result = result.OrderBy(_ => _random.Next()).ToList();
                RegisterSeen(result);

                System.Diagnostics.Debug.WriteLine(
                    $"[Bulk] page={mainPage} personalised={prefs?.HasData} " +
                    $"favGenres=[{string.Join(",", prefs?.FavoriteGenres ?? new())}] " +
                    $"→ {result.Count} anime (seen={_seenIds.Count})");

                return new AnimeLoadResult { Animes = result, IsRateLimited = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetBulkAnimesAsync error: " + ex.Message);
                return new AnimeLoadResult { Animes = new(), IsRateLimited = false };
            }
        }

        private async Task<string?> PostGraphQL(string query)
        {
            LastError = string.Empty;
            try
            {
                var payload = new { query }; var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                AniListRateLimit.RequestsSent++;
                var response = await _httpClient.PostAsync(ANILIST_ENDPOINT, content);
                System.Diagnostics.Debug.WriteLine($"AniList HTTP {(int)response.StatusCode}");
                ParseRateLimitHeaders(response, AniListRateLimit, new[] { "X-RateLimit-Limit" }, new[] { "X-RateLimit-Remaining" }, new[] { "X-RateLimit-Reset", "Retry-After" });
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests) { AniListRateLimit.IsLimited = true; AniListRateLimit.Remaining = 0; LastError = "429 Rate Limited"; return null; }
                AniListRateLimit.IsLimited = false;
                if (!response.IsSuccessStatusCode) { var body = await response.Content.ReadAsStringAsync(); LastError = $"HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}"; return null; }
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) { LastError = $"EXCEPTION {ex.GetType().Name}: {ex.Message}" + (ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : ""); System.Diagnostics.Debug.WriteLine("PostGraphQL error: " + LastError); return null; }
        }

        private static void ParseRateLimitHeaders(HttpResponseMessage response, RateLimitInfo info, string[] limitAliases, string[] remainingAliases, string[] resetAliases)
        {
            foreach (var name in limitAliases) if (TryGetHeader(response, name, out var v) && int.TryParse(v, out var lim)) { info.Limit = lim; break; }
            foreach (var name in remainingAliases) if (TryGetHeader(response, name, out var v) && int.TryParse(v, out var rem)) { info.Remaining = rem; break; }
            foreach (var name in resetAliases) { if (!TryGetHeader(response, name, out var v)) continue; if (long.TryParse(v, out var unix) && unix > 1_000_000_000L) { info.ResetAt = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime; break; } if (int.TryParse(v, out var delta)) { info.ResetAt = DateTime.UtcNow.AddSeconds(delta); break; } if (DateTimeOffset.TryParse(v, out var dto)) { info.ResetAt = dto.UtcDateTime; break; } }
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && info.ResetAt == null) info.ResetAt = DateTime.UtcNow.AddSeconds(RATE_LIMIT_FALLBACK_SECONDS);
        }

        private static bool TryGetHeader(HttpResponseMessage r, string name, out string value)
        {
            value = string.Empty;
            if (r.Headers.TryGetValues(name, out var v1)) { value = v1.FirstOrDefault() ?? string.Empty; if (!string.IsNullOrEmpty(value)) return true; }
            if (r.Content?.Headers.TryGetValues(name, out var v2) == true) { value = v2.FirstOrDefault() ?? string.Empty; if (!string.IsNullOrEmpty(value)) return true; }
            return false;
        }

        private string GetAnimeFieldsWithRelations() => @"
            id title { romaji english native } coverImage { extraLarge large color } bannerImage format
            averageScore genres episodes duration source description season seasonYear status
            countryOfOrigin isAdult startDate { year month day } endDate { year month day }
            popularity favourites nextAiringEpisode { episode timeUntilAiring }
            studios(isMain: true) { nodes { id name } }
            relations { edges { relationType node { id type title { romaji english } coverImage { extraLarge large color } format status } } }
            trailer { site id } tags { name rank isMediaSpoiler }
            externalLinks { url site type color icon } rankings { rank type context allTime season year }";

        private string GetAnimeFieldsNoRelations() => @"
            id title { romaji english native } coverImage { extraLarge large color } bannerImage format
            averageScore genres episodes duration source description season seasonYear status
            countryOfOrigin isAdult startDate { year month day } endDate { year month day }
            popularity favourites nextAiringEpisode { episode timeUntilAiring }
            studios(isMain: true) { nodes { id name } }
            trailer { site id } tags { name rank isMediaSpoiler }
            externalLinks { url site type color icon } rankings { rank type context allTime season year }";

        private List<AnimeCard> ExtractPool(JToken? arr, bool includeRelations)
        {
            var pool = new List<AnimeCard>(); var seenInPool = new HashSet<int>();
            if (arr == null || !arr.HasValues) return pool;
            foreach (var m in arr)
            {
                if (m == null || m.Type == JTokenType.Null) continue;
                try { var a = ParseAnimeCard(m, includeRelations); if (a == null || a.Id == 0) continue; if (_seenIds.Contains(a.Id)) continue; if (!seenInPool.Add(a.Id)) continue; pool.Add(a); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Parse error: " + ex.Message); }
            }
            return pool;
        }

        private List<AnimeCard> Pick(List<AnimeCard> pool, int count)
        {
            if (pool.Count == 0) return new();
            if (count >= pool.Count) return new(pool);
            var copy = new List<AnimeCard>(pool); var out2 = new List<AnimeCard>(count);
            for (int i = 0; i < count; i++) { int idx = _random.Next(copy.Count); out2.Add(copy[idx]); copy.RemoveAt(idx); }
            return out2;
        }

        public async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            if (IsRateLimited()) return null;
            try
            {
                int rand = _random.Next(100); int page = _random.Next(1, 80);
                string filter = rand < 45 ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\",\"Ecchi\"], sort: POPULARITY_DESC" : rand < 65 ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\",\"Ecchi\"], sort: SCORE_DESC" : rand < 80 ? "type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [\"Hentai\",\"Ecchi\"], sort: SCORE_DESC" : rand < 95 ? "type: ANIME, status: RELEASING, isAdult: false, genre_not_in: [\"Hentai\",\"Ecchi\"], sort: TRENDING_DESC" : "type: ANIME, status: NOT_YET_RELEASED, isAdult: false, genre_not_in: [\"Hentai\",\"Ecchi\"], sort: POPULARITY_DESC";
                var q = $"query {{ Page(page: {page}, perPage: 1) {{ media({filter}) {{ {GetAnimeFieldsWithRelations()} }} }} }}";
                var resp = await PostGraphQL(q); if (resp == null) return null;
                var arr = JObject.Parse(resp)?["data"]?["Page"]?["media"];
                if (arr == null || !arr.HasValues) return null;
                return ParseAnimeCard(arr[0]!, true);
            }
            catch { return null; }
        }

        private AnimeCard? ParseAnimeCard(JToken m, bool includeRelations)
        {
            try
            {
                var titleObj = m["title"];
                string title = !string.IsNullOrEmpty(titleObj?["english"]?.ToString()) ? titleObj!["english"]!.ToString() : titleObj?["romaji"]?.ToString() ?? "Unknown";
                string nativeTitle = titleObj?["native"]?.ToString() ?? "";
                var cover = m["coverImage"]; string imageUrl = cover?["extraLarge"]?.ToString() ?? cover?["large"]?.ToString() ?? ""; string coverColor = cover?["color"]?.ToString() ?? "";
                string score = m["averageScore"] != null && m["averageScore"]!.Type != JTokenType.Null ? m["averageScore"]!.ToString() : "N/A";
                string description = System.Text.RegularExpressions.Regex.Replace(m["description"]?.ToString() ?? "", "<.*?>", "");
                string season = m["season"]?.ToString() ?? "";
                int? year = m["seasonYear"] != null && m["seasonYear"]!.Type != JTokenType.Null ? m["seasonYear"]!.Value<int>() : null;
                string status = m["status"]?.ToString() ?? "";
                int? nextEp = null, timeUntil = null;
                var nae = m["nextAiringEpisode"];
                if (nae != null && nae.Type != JTokenType.Null) { nextEp = nae["episode"]?.Value<int?>(); timeUntil = nae["timeUntilAiring"]?.Value<int?>(); }
                string epDisplay = "N/A";
                if (status == "RELEASING") { int? total = m["episodes"] != null && m["episodes"]!.Type != JTokenType.Null ? m["episodes"]!.Value<int?>() : null; if (nextEp.HasValue) { int done = nextEp.Value - 1; epDisplay = total.HasValue ? $"{done}/{total.Value}" : $"{done}+"; } else if (total.HasValue) epDisplay = total.Value.ToString(); }
                else if (status == "FINISHED" || status == "NOT_YET_RELEASED") { if (m["episodes"] != null && m["episodes"]!.Type != JTokenType.Null) epDisplay = m["episodes"]!.ToString(); }
                var genres = new List<string>(); var ga = m["genres"]; if (ga != null && ga.HasValues) foreach (var g in ga) { if (g == null || g.Type == JTokenType.Null) continue; genres.Add(g.ToString()); }
                var studios = new List<AnimeStudio>(); var sn = m["studios"]?["nodes"]; if (sn != null && sn.HasValues) foreach (var s in sn) { if (s == null || s.Type == JTokenType.Null) continue; studios.Add(new AnimeStudio { Id = s["id"]?.Value<int>() ?? 0, Name = s["name"]?.ToString() ?? "" }); }
                var relations = new List<AnimeRelation>();
                if (includeRelations) { var re = m["relations"]?["edges"]; if (re != null && re.HasValues) foreach (var edge in re) { if (edge == null || edge.Type == JTokenType.Null) continue; var node = edge["node"]; if (node == null || node.Type == JTokenType.Null) continue; var rt = node["title"]; string relTitle = !string.IsNullOrEmpty(rt?["english"]?.ToString()) ? rt!["english"]!.ToString() : rt?["romaji"]?.ToString() ?? ""; var relCover = node["coverImage"]; string relImg = relCover?["extraLarge"]?.ToString() ?? relCover?["large"]?.ToString() ?? ""; relations.Add(new AnimeRelation { Id = node["id"]?.Value<int>() ?? 0, Type = node["type"]?.ToString() ?? "", RelationType = FormatRelationType(edge["relationType"]?.ToString() ?? ""), Title = relTitle, ImageUrl = relImg, Format = FormatFormat(node["format"]?.ToString() ?? ""), Status = node["status"]?.ToString() ?? "" }); } }
                string trailerUrl = ""; var tr = m["trailer"]; if (tr != null && tr.Type != JTokenType.Null) { var site = tr["site"]?.ToString() ?? ""; var tid = tr["id"]?.ToString() ?? ""; if (!string.IsNullOrEmpty(tid) && site.StartsWith("youtube", StringComparison.OrdinalIgnoreCase)) trailerUrl = "https://www.youtube.com/watch?v=" + tid; }
                var tags = new List<AnimeTag>(); var ta = m["tags"]; if (ta != null && ta.HasValues) foreach (var t in ta) { if (t == null || t.Type == JTokenType.Null) continue; tags.Add(new AnimeTag { Name = t["name"]?.ToString() ?? "", Rank = t["rank"]?.Value<int>() ?? 0, IsMediaSpoiler = t["isMediaSpoiler"]?.Value<bool>() ?? false }); }
                var extLinks = new List<AnimeExternalLink>(); var el = m["externalLinks"]; if (el != null && el.HasValues) foreach (var link in el) { if (link == null || link.Type == JTokenType.Null) continue; extLinks.Add(new AnimeExternalLink { Url = link["url"]?.ToString() ?? "", Site = link["site"]?.ToString() ?? "", Type = link["type"]?.ToString() ?? "", Color = link["color"]?.ToString() ?? "", Icon = link["icon"]?.ToString() ?? "" }); }
                var rankings = new List<AnimeRanking>(); var rk = m["rankings"]; if (rk != null && rk.HasValues) foreach (var r in rk) { if (r == null || r.Type == JTokenType.Null) continue; rankings.Add(new AnimeRanking { Rank = r["rank"]?.Value<int>() ?? 0, Type = r["type"]?.ToString() ?? "", Context = r["context"]?.ToString() ?? "", AllTime = r["allTime"]?.Value<bool>() ?? false, Season = r["season"]?.ToString() ?? "", Year = r["year"]?.Value<int?>() }); }
                return new AnimeCard { Id = m["id"]?.Value<int>() ?? 0, Title = title, NativeTitle = nativeTitle, ImageUrl = imageUrl, BannerUrl = m["bannerImage"]?.ToString() ?? "", CoverColor = coverColor, Score = score, Description = description, Season = season, Year = year, Status = status, Episodes = epDisplay, Genres = genres, Format = FormatFormat(m["format"]?.ToString() ?? ""), Source = FormatSource(m["source"]?.ToString() ?? ""), Duration = m["duration"]?.Value<int?>(), StartDate = FormatDate(m["startDate"]), EndDate = FormatDate(m["endDate"]), Popularity = m["popularity"]?.Value<int?>(), Favourites = m["favourites"]?.Value<int?>(), Studios = studios, Relations = relations, TrailerUrl = trailerUrl, Tags = tags, ExternalLinks = extLinks, Rankings = rankings, NextAiringEpisodeNum = nextEp, NextAiringTimeUntil = timeUntil, CountryOfOrigin = m["countryOfOrigin"]?.ToString() ?? "", IsAdult = m["isAdult"]?.Value<bool>() ?? false };
            }
            catch (Exception ex) { LastError = "ParseAnimeCard: " + ex.GetType().Name + ": " + ex.Message; System.Diagnostics.Debug.WriteLine(LastError); return null; }
        }

        private static double ComputeRelevance(string queryNorm, string title, string titleEnglish, double animeScore)
        {
            double score = 0; var t1 = title.Trim().ToLowerInvariant(); var t2 = titleEnglish.Trim().ToLowerInvariant();
            if (t1 == queryNorm || t2 == queryNorm) score += 60; else if (t1.StartsWith(queryNorm) || t2.StartsWith(queryNorm)) score += 40; else if (ContainsWholeWord(t1, queryNorm) || ContainsWholeWord(t2, queryNorm)) score += 25; else if (t1.Contains(queryNorm) || t2.Contains(queryNorm)) score += 12;
            score += (queryNorm.Length / Math.Max(1.0, t1.Length)) * 10; score += animeScore; return score;
        }
        private static bool ContainsWholeWord(string text, string word) { int idx = text.IndexOf(word, StringComparison.Ordinal); if (idx < 0) return false; bool before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]); int end = idx + word.Length; bool after = end >= text.Length || !char.IsLetterOrDigit(text[end]); return before && after; }
        private static int DetermineDisplayCount(List<JikanSearchResult> sorted, string query) { if (sorted.Count == 0) return 0; double best = sorted[0].RelevanceScore; int qualified = sorted.Count(r => r.RelevanceScore >= best * 0.45); int max = best >= 60 ? 4 : best >= 40 ? 6 : best >= 20 ? 8 : 10; return Math.Min(qualified, max); }

        private string FormatDate(JToken? d) { if (d == null) return ""; int? y = d["year"]?.Value<int?>(), mo = d["month"]?.Value<int?>(), day = d["day"]?.Value<int?>(); if (!y.HasValue) return ""; string[] months = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }; if (mo.HasValue && day.HasValue) return $"{months[mo.Value]} {day.Value}, {y}"; if (mo.HasValue) return $"{months[mo.Value]} {y}"; return y.Value.ToString(); }
        private string FormatFormat(string f) => f switch { "TV" => "TV Series", "TV_SHORT" => "TV Short", "MOVIE" => "Movie", "SPECIAL" => "Special", "OVA" => "OVA", "ONA" => "ONA", "MUSIC" => "Music", _ => f };
        private string FormatSource(string s) => s switch { "ORIGINAL" => "Original", "MANGA" => "Manga", "LIGHT_NOVEL" => "Light Novel", "VISUAL_NOVEL" => "Visual Novel", "VIDEO_GAME" => "Video Game", "WEB_NOVEL" => "Web Novel", "NOVEL" => "Novel", "ANIME" => "Anime", "GAME" => "Game", "COMIC" => "Comic", "PICTURE_BOOK" => "Picture Book", "MULTIMEDIA_PROJECT" => "Multimedia Project", "OTHER" => "Other", _ => s };
        private string FormatRelationType(string rt) => rt switch { "ADAPTATION" => "Adaptation", "PREQUEL" => "Prequel", "SEQUEL" => "Sequel", "PARENT" => "Parent", "SIDE_STORY" => "Side Story", "CHARACTER" => "Character", "SUMMARY" => "Summary", "ALTERNATIVE" => "Alternative", "SPIN_OFF" => "Spin-off", "SOURCE" => "Source", "COMPILATION" => "Compilation", "CONTAINS" => "Contains", "OTHER" => "Other", _ => rt };

        public async Task<AnimeLoadResult> GetMultipleAnimesAsync(int count, UserPreferences? prefs = null)
        {
            if (IsRateLimited()) return new AnimeLoadResult { Animes = new(), IsRateLimited = true };
            if (count > 10) { var bulk = await GetBulkAnimesAsync(prefs); if (bulk.Animes.Count > count) bulk.Animes = bulk.Animes.Take(count).ToList(); return bulk; }
            var results = new List<AnimeCard>();
            for (int i = 0; i < count; i++) { if (IsRateLimited()) break; var anime = await GetRandomAnimeAsync(); if (anime != null) results.Add(anime); await Task.Delay(100); }
            return new AnimeLoadResult { Animes = results, IsRateLimited = IsRateLimited() };
        }
    }

    public class AnimeLoadResult { public List<AnimeCard> Animes { get; set; } = new(); public bool IsRateLimited { get; set; } }
}