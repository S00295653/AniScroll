using AnimeScrollWasm.Models;
using AniScroll.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AnimeScrollWasm.Services
{
    public class AniListService
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;

        // ⚠️ DIFFÉRENCE WASM : HttpClient injecté
        public AniListService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _random = new Random();
        }

        public async Task<AnimeCard> GetRandomAnimeAsync()
        {
            try
            {
                int randomPage = _random.Next(1, 80);
                int rand = _random.Next(0, 100);
                string mediaFilter;

                if (rand < 45)
                    mediaFilter = "media(type: ANIME, status: FINISHED, averageScore_greater: 0, episodes_greater: 0, sort: POPULARITY_DESC)";
                else if (rand < 65)
                    mediaFilter = "media(type: ANIME, status: FINISHED, averageScore_greater: 0, episodes_greater: 0, sort: SCORE_DESC)";
                else if (rand < 80)
                    mediaFilter = "media(type: ANIME, status: FINISHED, averageScore_greater: 70, episodes_greater: 0, popularity_lesser: 20000, sort: SCORE_DESC)";
                else if (rand < 95)
                    mediaFilter = "media(type: ANIME, status: RELEASING, averageScore_greater: 0, sort: TRENDING_DESC)";
                else
                    mediaFilter = "media(type: ANIME, status: NOT_YET_RELEASED, sort: POPULARITY_DESC)";

                var query = $@"
                query ($page: Int) {{
                    Page(page: $page, perPage: 1) {{
                        {mediaFilter} {{
                            id
                            title {{ romaji english }}
                            coverImage {{ extraLarge large }}
                            bannerImage
                            averageScore
                            genres
                            episodes
                            description
                            season
                            seasonYear
                            status
                            nextAiringEpisode {{ episode }}
                        }}
                    }}
                }}";

                var request = new { query = query, variables = new { page = randomPage } };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);

                if (!response.IsSuccessStatusCode)
                    return null;

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
            catch (Exception)
            {
                return null;
            }
        }

        private AnimeCard ParseAnimeCard(JToken media)
        {
            var titleObj = media["title"];
            string displayTitle = (!string.IsNullOrEmpty(titleObj?["english"]?.ToString()))
                ? titleObj["english"].ToString()
                : titleObj?["romaji"]?.ToString();

            var coverObj = media["coverImage"];
            string imageUrl = coverObj?["extraLarge"]?.ToString() ?? coverObj?["large"]?.ToString();

            string score = media["averageScore"] != null && media["averageScore"].Type != JTokenType.Null
                ? media["averageScore"].ToString()
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
                int? totalEpisodes = media["episodes"]?.Type == JTokenType.Null ? null : (int?)media["episodes"];
                int? nextAiring = media["nextAiringEpisode"]?["episode"]?.Type == JTokenType.Null ? null : (int?)media["nextAiringEpisode"]["episode"];

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
                if (media["episodes"] != null && media["episodes"].Type != JTokenType.Null)
                    epDisplay = media["episodes"].ToString();
            }

            var genres = new List<string>();
            var genresArray = media["genres"];
            if (genresArray != null && genresArray.HasValues)
            {
                for (int i = 0; i < Math.Min(3, genresArray.Count()); i++)
                    genres.Add(genresArray[i].ToString());
            }

            return new AnimeCard
            {
                Id = media["id"]?.Value<int>() ?? 0,
                Title = displayTitle,
                ImageUrl = imageUrl,
                BannerUrl = media["bannerImage"]?.ToString(),
                Score = score,
                Description = description,
                Season = season,
                Year = year,
                Status = status,
                Episodes = epDisplay,
                Genres = genres
            };
        }

        public async Task<List<AnimeCard>> GetMultipleAnimesAsync(int count)
        {
            var tasks = new List<Task<AnimeCard>>();
            for (int i = 0; i < count; i++)
            {
                tasks.Add(GetRandomAnimeAsync());
            }

            var results = await Task.WhenAll(tasks);
            return results.Where(a => a != null).ToList();
        }
    }
}