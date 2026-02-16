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
            if (_rateLimitedUntil == null)
                return false;

            if (DateTime.UtcNow >= _rateLimitedUntil)
            {
                _rateLimitedUntil = null;
                return false;
            }

            return true;
        }

        public int GetRateLimitSecondsRemaining()
        {
            if (_rateLimitedUntil == null)
                return 0;

            var remaining = (_rateLimitedUntil.Value - DateTime.UtcNow).TotalSeconds;
            return Math.Max(0, (int)Math.Ceiling(remaining));
        }

        private void SetRateLimited()
        {
            _rateLimitedUntil = DateTime.UtcNow.AddSeconds(RATE_LIMIT_DURATION_SECONDS);
            System.Diagnostics.Debug.WriteLine($"⏱️ Rate limited until {_rateLimitedUntil}");
        }

        public async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            if (IsRateLimited())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Rate limited - request ignored");
                return null;
            }

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
                            title {{ 
                                romaji 
                                english 
                                native 
                            }}
                            coverImage {{ 
                                extraLarge 
                                large 
                            }}
                            bannerImage
                            averageScore
                            meanScore
                            genres
                            episodes
                            duration
                            description
                            season
                            seasonYear
                            status
                            format
                            source
                            startDate {{
                                year
                                month
                                day
                            }}
                            endDate {{
                                year
                                month
                                day
                            }}
                            popularity
                            favourites
                            hashtag
                            studios(isMain: true) {{
                                nodes {{
                                    name
                                }}
                            }}
                            tags {{
                                name
                                rank
                            }}
                            trailer {{
                                id
                                site
                            }}
                            nextAiringEpisode {{ 
                                episode 
                            }}
                            relations {{
                                edges {{
                                    relationType
                                    node {{
                                        id
                                        title {{
                                            romaji
                                            english
                                        }}
                                        coverImage {{
                                            large
                                        }}
                                        format
                                        type
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
                    System.Diagnostics.Debug.WriteLine("🚫 429 Too Many Requests - Rate limit activated");
                    SetRateLimited();
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ API Error: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(jsonResponse);

                var page = data["data"]?["Page"];
                if (page == null)
                    return null;

                var mediaArray = page["media"];
                if (mediaArray == null || !mediaArray.HasValues)
                    return null;

                var media = mediaArray[0];
                if (media == null)
                    return null;

                return ParseAnimeCard(media);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Unexpected error: {ex.Message}");
                return null;
            }
        }

        private AnimeCard ParseAnimeCard(JToken media)
        {
            var titleObj = media["title"];
            string displayTitle = (!string.IsNullOrEmpty(titleObj?["english"]?.ToString()))
                ? titleObj!["english"]!.ToString()
                : titleObj?["romaji"]?.ToString() ?? "Unknown";

            string titleEnglish = titleObj?["english"]?.ToString() ?? "";
            string titleNative = titleObj?["native"]?.ToString() ?? "";

            var coverObj = media["coverImage"];
            string imageUrl = coverObj?["extraLarge"]?.ToString() ?? coverObj?["large"]?.ToString() ?? "";

            string score = media["averageScore"] != null && media["averageScore"]!.Type != JTokenType.Null
                ? media["averageScore"]!.ToString()
                : "N/A";

            string meanScore = media["meanScore"] != null && media["meanScore"]!.Type != JTokenType.Null
                ? media["meanScore"]!.ToString()
                : "N/A";

            string description = media["description"]?.ToString() ?? "";
            description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);

            string season = media["season"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(season))
            {
                season = char.ToUpper(season[0]) + season.Substring(1).ToLower();
            }

            string yearStr = media["seasonYear"]?.ToString() ?? "";
            int? year = int.TryParse(yearStr, out int y) ? y : null;
            string status = media["status"]?.ToString() ?? "";
            string format = media["format"]?.ToString() ?? "";
            string source = media["source"]?.ToString() ?? "";

            // Format source text
            if (!string.IsNullOrEmpty(source))
            {
                source = source.Replace("_", " ");
                source = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(source.ToLower());
            }

            // Format dates
            string startDate = FormatDate(media["startDate"]);
            string endDate = FormatDate(media["endDate"]);

            int? popularity = media["popularity"] != null && media["popularity"]!.Type != JTokenType.Null
                ? (int?)media["popularity"]
                : null;

            int? favourites = media["favourites"] != null && media["favourites"]!.Type != JTokenType.Null
                ? (int?)media["favourites"]
                : null;

            int? duration = media["duration"] != null && media["duration"]!.Type != JTokenType.Null
                ? (int?)media["duration"]
                : null;

            int? averageScore = media["averageScore"] != null && media["averageScore"]!.Type != JTokenType.Null
                ? (int?)media["averageScore"]
                : null;

            string hashtag = media["hashtag"]?.ToString() ?? "";

            // Episodes display
            string epDisplay = "N/A";
            if (status == "RELEASING")
            {
                int? totalEpisodes = media["episodes"] != null && media["episodes"]!.Type != JTokenType.Null ? (int?)media["episodes"] : null;
                int? nextAiring = media["nextAiringEpisode"]?["episode"] != null && media["nextAiringEpisode"]!["episode"]!.Type != JTokenType.Null
                    ? (int?)media["nextAiringEpisode"]!["episode"]
                    : null;

                if (nextAiring.HasValue)
                {
                    int releasedEpisodes = nextAiring.Value - 1;
                    if (totalEpisodes.HasValue)
                        epDisplay = $"{releasedEpisodes}/{totalEpisodes.Value}";
                    else
                        epDisplay = $"{releasedEpisodes}+";
                }
                else if (totalEpisodes.HasValue)
                    epDisplay = totalEpisodes.Value.ToString();
            }
            else if (status == "FINISHED" || status == "NOT_YET_RELEASED")
            {
                if (media["episodes"] != null && media["episodes"]!.Type != JTokenType.Null)
                    epDisplay = media["episodes"]!.ToString();
            }

            // Genres
            var genres = new List<string>();
            var genresArray = media["genres"];
            if (genresArray != null && genresArray.HasValues)
            {
                for (int i = 0; i < Math.Min(3, genresArray.Count()); i++)
                    genres.Add(genresArray[i]!.ToString());
            }

            // Studios
            var studios = new List<string>();
            var studiosObj = media["studios"]?["nodes"];
            if (studiosObj != null && studiosObj.HasValues)
            {
                foreach (var studio in studiosObj.Take(3))
                {
                    string studioName = studio["name"]?.ToString();
                    if (!string.IsNullOrEmpty(studioName))
                        studios.Add(studioName);
                }
            }

            // Tags
            var tags = new List<string>();
            var tagsArray = media["tags"];
            if (tagsArray != null && tagsArray.HasValues)
            {
                foreach (var tag in tagsArray.OrderByDescending(t => t["rank"]).Take(15))
                {
                    string tagName = tag["name"]?.ToString();
                    if (!string.IsNullOrEmpty(tagName))
                        tags.Add(tagName);
                }
            }

            // Trailer
            string trailerUrl = "";
            var trailer = media["trailer"];
            if (trailer != null)
            {
                string site = trailer["site"]?.ToString() ?? "";
                string id = trailer["id"]?.ToString() ?? "";

                if (site == "youtube" && !string.IsNullOrEmpty(id))
                {
                    trailerUrl = $"https://www.youtube.com/embed/{id}";
                }
            }

            // Relations
            var relations = new List<AnimeCard.AnimeRelation>();
            var relationsEdges = media["relations"]?["edges"];
            if (relationsEdges != null && relationsEdges.HasValues)
            {
                foreach (var edge in relationsEdges.Take(10))
                {
                    var node = edge["node"];
                    var nodeType = node?["type"]?.ToString();

                    // Ne garder que les animes (pas les mangas)
                    if (nodeType == "ANIME")
                    {
                        var relationType = edge["relationType"]?.ToString() ?? "";
                        var relationTitle = node?["title"]?["english"]?.ToString()
                            ?? node?["title"]?["romaji"]?.ToString()
                            ?? "";
                        var relationImage = node?["coverImage"]?["large"]?.ToString() ?? "";
                        var relationFormat = node?["format"]?.ToString() ?? "";
                        var relationId = node?["id"]?.Value<int>() ?? 0;

                        if (!string.IsNullOrEmpty(relationTitle))
                        {
                            relations.Add(new AnimeCard.AnimeRelation
                            {
                                Id = relationId,
                                Title = relationTitle,
                                ImageUrl = relationImage,
                                RelationType = FormatRelationType(relationType),
                                Format = relationFormat
                            });
                        }
                    }
                }
            }

            return new AnimeCard
            {
                Id = media["id"]?.Value<int>() ?? 0,
                Title = displayTitle,
                TitleEnglish = titleEnglish,
                TitleNative = titleNative,
                ImageUrl = imageUrl,
                BannerUrl = media["bannerImage"]?.ToString() ?? "",
                Score = score,
                MeanScore = meanScore,
                Description = description,
                Season = season,
                Year = year,
                Status = status,
                Episodes = epDisplay,
                Duration = duration,
                Format = format,
                Source = source,
                StartDate = startDate,
                EndDate = endDate,
                Popularity = popularity,
                Favourites = favourites,
                Genres = genres,
                Studios = studios,
                Tags = tags,
                Hashtag = hashtag,
                TrailerUrl = trailerUrl,
                AverageScore = averageScore,
                Relations = relations
            };
        }

        private string FormatRelationType(string relationType)
        {
            return relationType switch
            {
                "SEQUEL" => "Sequel",
                "PREQUEL" => "Prequel",
                "ALTERNATIVE" => "Alternative",
                "SIDE_STORY" => "Side Story",
                "PARENT" => "Parent Story",
                "SUMMARY" => "Summary",
                "ADAPTATION" => "Adaptation",
                "SPIN_OFF" => "Spin-off",
                "OTHER" => "Related",
                _ => relationType
            };
        }

        private string FormatDate(JToken? dateToken)
        {
            if (dateToken == null)
                return "";

            int? year = dateToken["year"] != null && dateToken["year"]!.Type != JTokenType.Null
                ? (int?)dateToken["year"]
                : null;

            int? month = dateToken["month"] != null && dateToken["month"]!.Type != JTokenType.Null
                ? (int?)dateToken["month"]
                : null;

            int? day = dateToken["day"] != null && dateToken["day"]!.Type != JTokenType.Null
                ? (int?)dateToken["day"]
                : null;

            if (!year.HasValue)
                return "";

            if (!month.HasValue)
                return year.Value.ToString();

            string[] monthNames = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            string monthStr = month.Value >= 1 && month.Value <= 12 ? monthNames[month.Value] : "";

            if (!day.HasValue)
                return $"{monthStr} {year.Value}";

            return $"{monthStr} {day.Value}, {year.Value}";
        }

        public async Task<AnimeLoadResult> GetMultipleAnimesAsync(int count)
        {
            if (IsRateLimited())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Rate limited - loading cancelled");
                return new AnimeLoadResult
                {
                    Animes = new List<AnimeCard>(),
                    IsRateLimited = true
                };
            }

            var results = new List<AnimeCard>();

            for (int i = 0; i < count; i++)
            {
                if (IsRateLimited())
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Rate limited after {i} requests");
                    break;
                }

                var anime = await GetRandomAnimeAsync();
                if (anime != null)
                {
                    results.Add(anime);
                }
                else if (IsRateLimited())
                {
                    break;
                }

                await Task.Delay(100);
            }

            return new AnimeLoadResult
            {
                Animes = results,
                IsRateLimited = IsRateLimited()
            };
        }
    }

    public class AnimeLoadResult
    {
        public List<AnimeCard> Animes { get; set; } = new List<AnimeCard>();
        public bool IsRateLimited { get; set; }
    }
}