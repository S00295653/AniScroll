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
            System.Diagnostics.Debug.WriteLine($"⏱️ Rate limited jusqu'à {_rateLimitedUntil}");
        }

        public async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            if (IsRateLimited())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Rate limited - requête ignorée");
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


                var query = GetDetailedAnimeQuery();

                var request = new { query = query, variables = new { page = randomPage } };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    System.Diagnostics.Debug.WriteLine("🚫 429 Too Many Requests - Rate limit activé");
                    SetRateLimited();
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Erreur API: {response.StatusCode}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Erreur réseau: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Erreur inattendue: {ex.Message}");
                return null;
            }
        }

        // Nouvelle méthode : récupère 60 animes en une seule requête
        public async Task<AnimeLoadResult> GetBulkAnimesAsync()
        {
            if (IsRateLimited())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Rate limited - chargement annulé");
                return new AnimeLoadResult
                {
                    Animes = new List<AnimeCard>(),
                    IsRateLimited = true
                };
            }

            try
            {
                // Répartition : 27 populaires, 12 top score, 12 hidden gems, 8 ongoing, 1 upcoming
                var query = @"
                query {
                    popular1: Page(page: 1, perPage: 9) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    popular2: Page(page: 2, perPage: 9) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    popular3: Page(page: 3, perPage: 9) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    topScore1: Page(page: 1, perPage: 6) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    topScore2: Page(page: 2, perPage: 6) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    hiddenGems1: Page(page: 1, perPage: 6) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    hiddenGems2: Page(page: 2, perPage: 6) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, genre_not_in: [""Hentai"", ""Ecchi""], sort: SCORE_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    ongoing: Page(page: 1, perPage: 8) {
                        media(type: ANIME, status: RELEASING, isAdult: false, averageScore_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: TRENDING_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                    upcoming: Page(page: 1, perPage: 1) {
                        media(type: ANIME, status: NOT_YET_RELEASED, isAdult: false, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                }";

                var request = new { query = query };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    System.Diagnostics.Debug.WriteLine("🚫 429 Too Many Requests - Rate limit activé");
                    SetRateLimited();
                    return new AnimeLoadResult
                    {
                        Animes = new List<AnimeCard>(),
                        IsRateLimited = true
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Erreur API: {response.StatusCode}");
                    return new AnimeLoadResult
                    {
                        Animes = new List<AnimeCard>(),
                        IsRateLimited = false
                    };
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(jsonResponse);

                var results = new List<AnimeCard>();

                // Extraire les animes de toutes les catégories
                var categories = new[] { "popular1", "popular2", "popular3", "topScore1", "topScore2", 
                                        "hiddenGems1", "hiddenGems2", "ongoing", "upcoming" };

                foreach (var category in categories)
                {
                    var categoryData = data["data"]?[category];
                    if (categoryData?["media"] != null && categoryData["media"]!.HasValues)
                    {
                        foreach (var media in categoryData["media"]!)
                        {
                            var anime = ParseAnimeCard(media);
                            if (anime != null)
                            {
                                results.Add(anime);
                            }
                        }
                    }
                }

                // Mélanger pour un ordre aléatoire
                results = results.OrderBy(x => _random.Next()).ToList();

                System.Diagnostics.Debug.WriteLine($"✅ {results.Count} animes récupérés en une requête");

                return new AnimeLoadResult
                {
                    Animes = results,
                    IsRateLimited = false
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Erreur réseau: {ex.Message}");
                return new AnimeLoadResult
                {
                    Animes = new List<AnimeCard>(),
                    IsRateLimited = false
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Erreur inattendue: {ex.Message}");
                return new AnimeLoadResult
                {
                    Animes = new List<AnimeCard>(),
                    IsRateLimited = false
                };
            }
        }

        private string GetAnimeFields()
        {
            return @"
                id
                title { romaji english }
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
                endDate { year month day }
                popularity
                favourites
                nextAiringEpisode { episode }
                studios(isMain: true) {
                    nodes {
                        id
                        name
                    }
                }
                relations {
                    edges {
                        relationType
                        node {
                            id
                            type
                            title { romaji english }
                            coverImage { large }
                            format
                            status
                        }
                    }
                }
                trailer {
                    site
                    id
                }
            ";
        }

        private string GetDetailedAnimeQuery()
        {
            return @"
                query ($page: Int) {
                    Page(page: $page, perPage: 1) {
                        media(type: ANIME, status: FINISHED, isAdult: false, averageScore_greater: 0, episodes_greater: 0, genre_not_in: [""Hentai"", ""Ecchi""], sort: POPULARITY_DESC) {
                            " + GetAnimeFields() + @"
                        }
                    }
                }";
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
                ? media["averageScore"]!.ToString()
                : "N/A";

            string description = media["description"]?.ToString() ?? "";
            description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);

            string season = media["season"]?.ToString() ?? "";
            string yearStr = media["seasonYear"]?.ToString() ?? "";
            int? year = int.TryParse(yearStr, out int y) ? y : null;
            string status = media["status"]?.ToString() ?? "";

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

            var genres = new List<string>();
            var genresArray = media["genres"];
            if (genresArray != null && genresArray.HasValues)
            {
                for (int i = 0; i < Math.Min(3, genresArray.Count()); i++)
                    genres.Add(genresArray[i]!.ToString());
            }

            // Parse extended details
            string format = FormatAnimeFormat(media["format"]?.ToString() ?? "");
            string source = FormatSource(media["source"]?.ToString() ?? "");
            int? duration = media["duration"] != null && media["duration"]!.Type != JTokenType.Null 
                ? (int?)media["duration"] : null;

            string startDate = FormatDate(media["startDate"]);
            string endDate = FormatDate(media["endDate"]);

            int? popularity = media["popularity"] != null && media["popularity"]!.Type != JTokenType.Null
                ? (int?)media["popularity"] : null;
            int? favourites = media["favourites"] != null && media["favourites"]!.Type != JTokenType.Null
                ? (int?)media["favourites"] : null;

            // Parse studios
            var studios = new List<AnimeStudio>();
            var studiosData = media["studios"]?["nodes"];
            if (studiosData != null && studiosData.HasValues)
            {
                foreach (var studio in studiosData)
                {
                    studios.Add(new AnimeStudio
                    {
                        Id = studio["id"]?.Value<int>() ?? 0,
                        Name = studio["name"]?.ToString() ?? ""
                    });
                }
            }

            // Parse relations
            var relations = new List<AnimeRelation>();
            var relationsData = media["relations"]?["edges"];
            if (relationsData != null && relationsData.HasValues)
            {
                foreach (var edge in relationsData.Take(8)) // Limiter à 8 relations
                {
                    var node = edge["node"];
                    if (node == null || node["type"]?.ToString() != "ANIME") continue;

                    var relTitle = node["title"];
                    string relDisplayTitle = (!string.IsNullOrEmpty(relTitle?["english"]?.ToString()))
                        ? relTitle!["english"]!.ToString()
                        : relTitle?["romaji"]?.ToString() ?? "";

                    relations.Add(new AnimeRelation
                    {
                        Id = node["id"]?.Value<int>() ?? 0,
                        Type = "ANIME",
                        RelationType = FormatRelationType(edge["relationType"]?.ToString() ?? ""),
                        Title = relDisplayTitle,
                        ImageUrl = node["coverImage"]?["large"]?.ToString() ?? "",
                        Format = FormatAnimeFormat(node["format"]?.ToString() ?? ""),
                        Status = node["status"]?.ToString() ?? ""
                    });
                }
            }

            // Parse trailer
            string trailerUrl = "";
            var trailer = media["trailer"];
            if (trailer != null)
            {
                string site = trailer["site"]?.ToString() ?? "";
                string id = trailer["id"]?.ToString() ?? "";
                if (site == "youtube" && !string.IsNullOrEmpty(id))
                {
                    trailerUrl = $"https://www.youtube.com/watch?v={id}";
                }
            }

            return new AnimeCard
            {
                Id = media["id"]?.Value<int>() ?? 0,
                Title = displayTitle,
                ImageUrl = imageUrl,
                BannerUrl = media["bannerImage"]?.ToString() ?? "",
                Score = score,
                Description = description,
                Season = season,
                Year = year,
                Status = status,
                Episodes = epDisplay,
                Genres = genres,
                Format = format,
                Source = source,
                Duration = duration,
                StartDate = startDate,
                EndDate = endDate,
                Popularity = popularity,
                Favourites = favourites,
                Studios = studios,
                Relations = relations,
                TrailerUrl = trailerUrl
            };
        }

        private string FormatDate(JToken? dateToken)
        {
            if (dateToken == null) return "";

            int? year = dateToken["year"]?.Value<int>();
            int? month = dateToken["month"]?.Value<int>();
            int? day = dateToken["day"]?.Value<int>();

            if (!year.HasValue) return "";

            string[] months = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            if (month.HasValue && day.HasValue)
                return $"{months[month.Value]} {day.Value}, {year.Value}";
            else if (month.HasValue)
                return $"{months[month.Value]} {year.Value}";
            else
                return year.Value.ToString();
        }

        private string FormatAnimeFormat(string format)
        {
            return format switch
            {
                "TV" => "TV Series",
                "TV_SHORT" => "TV Short",
                "MOVIE" => "Movie",
                "SPECIAL" => "Special",
                "OVA" => "OVA",
                "ONA" => "ONA",
                "MUSIC" => "Music",
                _ => format
            };
        }

        private string FormatSource(string source)
        {
            return source switch
            {
                "ORIGINAL" => "Original",
                "MANGA" => "Manga",
                "LIGHT_NOVEL" => "Light Novel",
                "VISUAL_NOVEL" => "Visual Novel",
                "VIDEO_GAME" => "Video Game",
                "OTHER" => "Other",
                "NOVEL" => "Novel",
                "DOUJINSHI" => "Doujinshi",
                "ANIME" => "Anime",
                "WEB_NOVEL" => "Web Novel",
                "LIVE_ACTION" => "Live Action",
                "GAME" => "Game",
                "COMIC" => "Comic",
                "MULTIMEDIA_PROJECT" => "Multimedia Project",
                "PICTURE_BOOK" => "Picture Book",
                _ => source
            };
        }

        private string FormatRelationType(string relationType)
        {
            return relationType switch
            {
                "ADAPTATION" => "Adaptation",
                "PREQUEL" => "Prequel",
                "SEQUEL" => "Sequel",
                "PARENT" => "Parent",
                "SIDE_STORY" => "Side Story",
                "CHARACTER" => "Character",
                "SUMMARY" => "Summary",
                "ALTERNATIVE" => "Alternative",
                "SPIN_OFF" => "Spin-off",
                "OTHER" => "Other",
                "SOURCE" => "Source",
                "COMPILATION" => "Compilation",
                "CONTAINS" => "Contains",
                _ => relationType
            };
        }

        public async Task<AnimeLoadResult> GetMultipleAnimesAsync(int count)
        {
            if (IsRateLimited())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Rate limited - chargement annulé");
                return new AnimeLoadResult
                {
                    Animes = new List<AnimeCard>(),
                    IsRateLimited = true
                };
            }

            // Si on demande 5 ou moins, utiliser l'ancienne méthode
            if (count <= 5)
            {
                var results = new List<AnimeCard>();

                for (int i = 0; i < count; i++)
                {
                    if (IsRateLimited())
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Rate limited après {i} requêtes");
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
            else
            {
                // Pour de grandes quantités, utiliser la méthode bulk
                return await GetBulkAnimesAsync();
            }
        }
    }

    public class AnimeLoadResult
    {
        public List<AnimeCard> Animes { get; set; } = new List<AnimeCard>();
        public bool IsRateLimited { get; set; }
    }
}