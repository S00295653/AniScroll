using AniScroll.Shared.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AniScroll.Shared.Services;

/// <summary>
/// Handles AniList OAuth (implicit grant) and user-data import.
///
/// SETUP — register your app at https://anilist.co/settings/developer
///   Name  : anything (e.g. "AniScroll")
///   Redirect URI : the exact origin of your app, e.g.
///                  https://localhost:5001/   (dev)
///                  https://yourdomain.com/   (prod)
/// Then paste your numeric Client ID into CLIENT_ID below.
/// </summary>
public class AniListAuthService
{
    // ── ⚙️  CONFIGURE THIS ──────────────────────────────────────────────────
    private const string CLIENT_ID = "36730"; // ← replace with your app's ID
    // ────────────────────────────────────────────────────────────────────────

    private readonly HttpClient _httpClient;
    private const string ENDPOINT = "https://graphql.anilist.co";

    private string? _accessToken;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public AniListUserProfile? CurrentUser { get; private set; }

    public AniListAuthService(HttpClient httpClient) => _httpClient = httpClient;

    // ── Token management ──────────────────────────────────────────────────

    public string GetAuthUrl() =>
        $"https://anilist.co/api/v2/oauth/authorize" +
        $"?client_id={CLIENT_ID}" +
        $"&response_type=token";

    public void SetToken(string token) { _accessToken = token; }
    public void ClearToken() { _accessToken = null; CurrentUser = null; }

    // ── Viewer (current user) ─────────────────────────────────────────────

    public async Task<AniListUserProfile?> FetchCurrentUserAsync()
    {
        if (!IsAuthenticated) return null;

        const string q = @"query {
            Viewer {
                id
                name
                avatar { large medium }
            }
        }";

        var resp = await PostGraphQL(q);
        if (resp == null) return null;

        var data = JObject.Parse(resp)?["data"]?["Viewer"];
        if (data == null || data.Type == JTokenType.Null) return null;

        CurrentUser = new AniListUserProfile
        {
            Id = data["id"]?.Value<int>() ?? 0,
            Name = data["name"]?.ToString() ?? "",
            AvatarUrl = data["avatar"]?["large"]?.ToString()
                     ?? data["avatar"]?["medium"]?.ToString() ?? ""
        };
        return CurrentUser;
    }

    // ── Full anime list (all lists, including custom) ─────────────────────

    /// <summary>
    /// Returns all anime entries for <paramref name="userId"/>.
    /// Custom list membership is detected by which list object each entry belongs to.
    /// </summary>
    public async Task<List<AniListImportEntry>> FetchAnimeListAsync(int userId)
    {
        var q = $@"query {{
            MediaListCollection(userId: {userId}, type: ANIME) {{
                lists {{
                    name
                    isCustomList
                    entries {{
                        mediaId
                        status
                        score(format: POINT_10_DECIMAL)
                        progress
                        repeat
                        notes
                        private
                        hiddenFromStatusLists
                        startedAt   {{ year month day }}
                        completedAt {{ year month day }}
                        media {{
                            id
                            title {{ romaji english native }}
                            coverImage {{ extraLarge large color }}
                            bannerImage
                            format
                            averageScore
                            genres
                            episodes
                            status
                            season
                            seasonYear
                            description
                            duration
                            source
                            startDate {{ year month day }}
                            endDate   {{ year month day }}
                            popularity
                            favourites
                        }}
                    }}
                }}
            }}
        }}";

        var resp = await PostGraphQL(q);
        if (resp == null) return new();

        var lists = JObject.Parse(resp)?["data"]?["MediaListCollection"]?["lists"];
        if (lists == null || !lists.HasValues) return new();

        // Deduplicate by mediaId; accumulate custom-list membership across lists
        var seen = new Dictionary<int, AniListImportEntry>();

        foreach (var list in lists)
        {
            var listName = list["name"]?.ToString() ?? "";
            bool isCustom = list["isCustomList"]?.Value<bool>() ?? false;
            var entries = list["entries"];
            if (entries == null || !entries.HasValues) continue;

            foreach (var entry in entries)
            {
                int mediaId = entry["mediaId"]?.Value<int>() ?? 0;
                if (mediaId == 0) continue;

                if (!seen.ContainsKey(mediaId))
                    seen[mediaId] = ParseEntry(entry);

                if (isCustom && !string.IsNullOrWhiteSpace(listName)
                    && !seen[mediaId].CustomListNames.Contains(listName))
                    seen[mediaId].CustomListNames.Add(listName);
            }
        }

        return seen.Values.ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private AniListImportEntry ParseEntry(JToken e)
    {
        var media = e["media"];
        var titleObj = media?["title"];

        string title = !string.IsNullOrEmpty(titleObj?["english"]?.ToString())
            ? titleObj!["english"]!.ToString()
            : titleObj?["romaji"]?.ToString() ?? "";

        var cover = media?["coverImage"];
        string imgUrl = cover?["extraLarge"]?.ToString() ?? cover?["large"]?.ToString() ?? "";
        string clrHex = cover?["color"]?.ToString() ?? "";

        string desc = System.Text.RegularExpressions.Regex
            .Replace(media?["description"]?.ToString() ?? "", "<.*?>", "");

        string epDisplay = "N/A";
        string mStatus = media?["status"]?.ToString() ?? "";
        if (mStatus == "RELEASING")
        {
            var ep = media?["episodes"];
            epDisplay = (ep != null && ep.Type != JTokenType.Null) ? ep.ToString() : "?+";
        }
        else if (media?["episodes"] != null && media["episodes"]!.Type != JTokenType.Null)
        {
            epDisplay = media["episodes"]!.ToString();
        }

        return new AniListImportEntry
        {
            MediaId = e["mediaId"]?.Value<int>() ?? 0,
            AniListStatus = e["status"]?.ToString() ?? "",
            Score = e["score"]?.Value<double>() ?? 0,
            Progress = e["progress"]?.Value<int>() ?? 0,
            Repeat = e["repeat"]?.Value<int>() ?? 0,
            Notes = e["notes"]?.ToString() ?? "",
            IsPrivate = e["private"]?.Value<bool>() ?? false,
            HideFromStatusLists = e["hiddenFromStatusLists"]?.Value<bool>() ?? false,
            StartedAt = ParseFuzzyDate(e["startedAt"]),
            CompletedAt = ParseFuzzyDate(e["completedAt"]),
            AnimeCard = new AnimeCard
            {
                Id = media?["id"]?.Value<int>() ?? 0,
                Title = title,
                NativeTitle = titleObj?["native"]?.ToString() ?? "",
                ImageUrl = imgUrl,
                BannerUrl = media?["bannerImage"]?.ToString() ?? "",
                CoverColor = clrHex,
                Score = media?["averageScore"]?.ToString() ?? "N/A",
                Status = mStatus,
                Format = FormatFormat(media?["format"]?.ToString() ?? ""),
                Episodes = epDisplay,
                Description = desc,
                Season = media?["season"]?.ToString() ?? "",
                Year = media?["seasonYear"]?.Value<int?>(),
                Genres = media?["genres"]?.Select(g => g.ToString()).Take(3).ToList() ?? new(),
                Duration = media?["duration"]?.Value<int?>(),
                Popularity = media?["popularity"]?.Value<int?>(),
                Favourites = media?["favourites"]?.Value<int?>(),
            }
        };
    }

    private static DateTime? ParseFuzzyDate(JToken? d)
    {
        if (d == null || d.Type == JTokenType.Null) return null;
        int? y = d["year"]?.Value<int?>();
        int? mo = d["month"]?.Value<int?>();
        int? dy = d["day"]?.Value<int?>();
        if (!y.HasValue || !mo.HasValue || !dy.HasValue) return null;
        try { return new DateTime(y.Value, mo.Value, dy.Value); }
        catch { return null; }
    }

    private static string FormatFormat(string f) => f switch
    {
        "TV" => "TV Series",
        "TV_SHORT" => "TV Short",
        "MOVIE" => "Movie",
        "SPECIAL" => "Special",
        "OVA" => "OVA",
        "ONA" => "ONA",
        "MUSIC" => "Music",
        _ => f
    };

    private async Task<string?> PostGraphQL(string query)
    {
        try
        {
            var payload = new { query };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, ENDPOINT)
            { Content = content };

            if (!string.IsNullOrEmpty(_accessToken))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[AniListAuthService] " + ex.Message);
            return null;
        }
    }
}