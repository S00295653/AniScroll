using AniScroll.Shared.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AniScroll.Shared.Services
{
    public class AniListService
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;

        private DateTime? _rateLimitedUntil = null;
        private const int RATE_LIMIT_DURATION_SECONDS = 60;

        public AniListService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _random = new Random();
        }

        public bool IsRateLimited()
        {
            if (_rateLimitedUntil == null) return false;
            if (DateTime.UtcNow >= _rateLimitedUntil) { _rateLimitedUntil = null; return false; }
            return true;
        }

        public int GetRateLimitSecondsRemaining()
        {
            if (_rateLimitedUntil == null) return 0;
            var remaining = (_rateLimitedUntil.Value - DateTime.UtcNow).TotalSeconds;
            return Math.Max(0, (int)Math.Ceiling(remaining));
        }

        private void SetRateLimited()
        {
            _rateLimitedUntil = DateTime.UtcNow.AddSeconds(RATE_LIMIT_DURATION_SECONDS);
        }

        public async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            if (IsRateLimited()) return null;

            try
            {
                int randomPage = _random.Next(1, 80);
                int rand = _random.Next(0, 100);
                string mediaFilter;

                if (rand < 45)
                    mediaFilter = "media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: POPULARITY_DESC)";
                else if (rand < 65)
                    mediaFilter = "media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: SCORE_DESC)";
                else if (rand < 80)
                    mediaFilter = "media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: SCORE_DESC)";
                else if (rand < 95)
                    mediaFilter = "media(type: ANIME, status: RELEASING, isAdult: false, averageScore_greater: 0, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: TRENDING_DESC)";
                else
                    mediaFilter = "media(type: ANIME, status: NOT_YET_RELEASED, isAdult: false, genre_not_in: [\"Hentai\", \"Ecchi\"], sort: POPULARITY_DESC)";

                var query = $@"
                query ($page: Int) {{
                    Page(page: $page, perPage: 1) {{
                        {mediaFilter} {{
                            id
                            title {{ romaji english native }}
                            coverImage {{ extraLarge large }}
                            bannerImage
                            averageScore
                            meanScore
                            genres
                            episodes
                            description
                            season
                            seasonYear
                            status
                            format
                            source
                            duration
                            popularity
                            favourites
                            siteUrl
                            nextAiringEpisode {{ episode airingAt }}
                            studios(isMain: true) {{ nodes {{ name }} }}
                            tags(sort: RANK_DESC) {{ name rank isMediaSpoiler }}
                            relations {{
                                edges {{
                                    relationType
                                    node {{
                                        id
                                        title {{ romaji english }}
                                        coverImage {{ large }}
                                        format
                                        status
                                    }}
                                }}
                            }}
                        }}
                    }}
                }}";

                var request = new { query = query, variables = new { page = randomPage } };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    SetRateLimited();
                    return null;
                }

                if (!response.IsSuccessStatusCode) return null;

                var jsonResponse = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(jsonResponse);

                var page = data["data"]?["Page"];
                if (page == null) return null;

                var mediaArray = page["media"];
                if (mediaArray == null || !mediaArray.HasValues) return null;

                var media = mediaArray[0];
                if (media == null) return null;

                return ParseAnimeCard(media);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        private AnimeCard ParseAnimeCard(JToken media)
        {
            var titleObj = media["title"];
            string displayTitle = (!string.IsNullOrEmpty(titleObj?["english"]?.ToString()))
                ? titleObj!["english"]!.ToString()
                : titleObj?["romaji"]?.ToString() ?? "Unknown";

            var coverObj = media["coverImage"];
            string imageUrl = coverObj?["extraLarge"]?.ToString() ?? coverObj?["large"]?.ToString() ?? "";

            string score = media["averageScore"] != null && media["averageScore"]!.Type != JTokenType.Null
                ? media["averageScore"]!.ToString() : "N/A";

            string description = media["description"]?.ToString() ?? "";
            description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);

            string season = media["season"]?.ToString() ?? "";
            string yearStr = media["seasonYear"]?.ToString() ?? "";
            int? year = int.TryParse(yearStr, out int y) ? y : null;
            string status = media["status"]?.ToString() ?? "";

            int? totalEpisodes = media["episodes"] != null && media["episodes"]!.Type != JTokenType.Null
                ? (int?)media["episodes"] : null;

            string epDisplay = "N/A";
            int? nextEp = null;
            long? nextAiringAt = null;

            if (status == "RELEASING")
            {
                int? nextAiring = media["nextAiringEpisode"]?["episode"] != null && media["nextAiringEpisode"]!["episode"]!.Type != JTokenType.Null
                    ? (int?)media["nextAiringEpisode"]!["episode"] : null;

                if (media["nextAiringEpisode"]?["airingAt"] != null && media["nextAiringEpisode"]!["airingAt"]!.Type != JTokenType.Null)
                    nextAiringAt = (long?)media["nextAiringEpisode"]!["airingAt"];

                nextEp = nextAiring;

                if (nextAiring.HasValue)
                {
                    int releasedEpisodes = nextAiring.Value - 1;
                    epDisplay = totalEpisodes.HasValue ? $"{releasedEpisodes}/{totalEpisodes.Value}" : $"{releasedEpisodes}+";
                }
                else if (totalEpisodes.HasValue)
                    epDisplay = totalEpisodes.Value.ToString();
            }
            else if (status == "FINISHED" || status == "NOT_YET_RELEASED")
            {
                if (totalEpisodes.HasValue)
                    epDisplay = totalEpisodes.Value.ToString();
            }

            var genres = new List<string>();
            var genresArray = media["genres"];
            if (genresArray != null && genresArray.HasValues)
            {
                foreach (var g in genresArray)
                    genres.Add(g!.ToString());
            }

            // Studios
            var studios = new List<string>();
            var studiosNodes = media["studios"]?["nodes"];
            if (studiosNodes != null && studiosNodes.HasValues)
            {
                foreach (var s in studiosNodes)
                {
                    var name = s["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name)) studios.Add(name);
                }
            }

            // Tags (non-spoiler, top 6)
            var tags = new List<string>();
            var tagsArray = media["tags"];
            if (tagsArray != null && tagsArray.HasValues)
            {
                foreach (var t in tagsArray)
                {
                    bool isSpoiler = t["isMediaSpoiler"]?.Value<bool>() ?? false;
                    if (!isSpoiler && tags.Count < 6)
                        tags.Add(t["name"]?.ToString() ?? "");
                }
            }

            // Relations
            var relations = new List<AnimeRelation>();
            var relEdges = media["relations"]?["edges"];
            if (relEdges != null && relEdges.HasValues)
            {
                foreach (var edge in relEdges)
                {
                    var relType = edge["relationType"]?.ToString() ?? "";
                    var node = edge["node"];
                    if (node == null) continue;

                    var relTitle = node["title"];
                    string relDisplayTitle = (!string.IsNullOrEmpty(relTitle?["english"]?.ToString()))
                        ? relTitle!["english"]!.ToString()
                        : relTitle?["romaji"]?.ToString() ?? "Unknown";

                    relations.Add(new AnimeRelation
                    {
                        Id = node["id"]?.Value<int>() ?? 0,
                        Title = relDisplayTitle,
                        ImageUrl = node["coverImage"]?["large"]?.ToString() ?? "",
                        RelationType = relType,
                        Format = node["format"]?.ToString() ?? "",
                        Status = node["status"]?.ToString() ?? ""
                    });
                }
            }

            int? meanScore = media["meanScore"] != null && media["meanScore"]!.Type != JTokenType.Null
                ? (int?)media["meanScore"] : null;

            int? popularity = media["popularity"] != null && media["popularity"]!.Type != JTokenType.Null
                ? (int?)media["popularity"] : null;

            int? favourites = media["favourites"] != null && media["favourites"]!.Type != JTokenType.Null
                ? (int?)media["favourites"] : null;

            int? duration = media["duration"] != null && media["duration"]!.Type != JTokenType.Null
                ? (int?)media["duration"] : null;

            return new AnimeCard
            {
                Id = media["id"]?.Value<int>() ?? 0,
                Title = displayTitle,
                TitleRomaji = titleObj?["romaji"]?.ToString() ?? "",
                TitleNative = titleObj?["native"]?.ToString() ?? "",
                ImageUrl = imageUrl,
                BannerUrl = media["bannerImage"]?.ToString() ?? "",
                Score = score,
                Description = description,
                Season = season,
                Year = year,
                Status = status,
                Episodes = epDisplay,
                TotalEpisodes = totalEpisodes,
                Genres = genres,
                Format = media["format"]?.ToString() ?? "",
                Source = media["source"]?.ToString() ?? "",
                Duration = duration,
                Popularity = popularity,
                Favourites = favourites,
                MeanScore = meanScore,
                Studios = studios,
                Tags = tags,
                SiteUrl = media["siteUrl"]?.ToString() ?? "",
                NextEpisode = nextEp,
                NextAiringAt = nextAiringAt,
                Relations = relations
            };
        }

        public async Task<AnimeLoadResult> GetMultipleAnimesAsync(int count)
        {
            if (IsRateLimited())
                return new AnimeLoadResult { Animes = new List<AnimeCard>(), IsRateLimited = true };

            var results = new List<AnimeCard>();

            for (int i = 0; i < count; i++)
            {
                if (IsRateLimited()) break;

                var anime = await GetRandomAnimeAsync();
                if (anime != null) results.Add(anime);
                else if (IsRateLimited()) break;

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