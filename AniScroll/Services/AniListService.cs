using AniScroll.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AniScroll.Services
{
    public class AniListService : IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random;
        
        // 🔥 NOUVEAU SYSTÈME DE BUFFER
        private Queue<AnimeCard> _animeBuffer = new Queue<AnimeCard>();
        private HashSet<int> _loadedAnimeIds = new HashSet<int>();
        private SemaphoreSlim _bufferLock = new SemaphoreSlim(1, 1);
        
        // Configuration du buffer
        private const int MIN_BUFFER_SIZE = 10; // Toujours garder 10 animes d'avance
        private const int MAX_BUFFER_SIZE = 20; // Ne pas dépasser 20 pour économiser la mémoire
        private const int DELAY_BETWEEN_REQUESTS_MS = 300; // 300ms entre chaque requête pour ne pas spammer l'API
        
        // Worker en arrière-plan
        private CancellationTokenSource _backgroundLoaderCts = new CancellationTokenSource();
        private Task? _backgroundLoaderTask;
        private bool _isBackgroundLoaderRunning = false;
        
        private const string ANILIST_API_URL = "https://graphql.anilist.co";

        public AniListService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _random = new Random();
        }

        /// <summary>
        /// Démarre le chargement en arrière-plan
        /// </summary>
        public void StartBackgroundLoading()
        {
            if (_isBackgroundLoaderRunning)
                return;

            _isBackgroundLoaderRunning = true;
            _backgroundLoaderTask = Task.Run(BackgroundLoaderWorker, _backgroundLoaderCts.Token);
            System.Diagnostics.Debug.WriteLine("🚀 Background loader démarré");
        }

        /// <summary>
        /// Worker qui charge continuellement des animes en arrière-plan
        /// </summary>
        private async Task BackgroundLoaderWorker()
        {
            while (!_backgroundLoaderCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Vérifier la taille du buffer
                    await _bufferLock.WaitAsync();
                    int currentBufferSize = _animeBuffer.Count;
                    _bufferLock.Release();

                    // Si le buffer est plein, attendre un peu
                    if (currentBufferSize >= MAX_BUFFER_SIZE)
                    {
                        await Task.Delay(1000, _backgroundLoaderCts.Token);
                        continue;
                    }

                    // Charger un anime
                    var anime = await GetRandomAnimeAsync();
                    
                    if (anime != null)
                    {
                        await _bufferLock.WaitAsync();
                        _animeBuffer.Enqueue(anime);
                        System.Diagnostics.Debug.WriteLine($"📦 Buffer: {_animeBuffer.Count} animes | Cache: {_loadedAnimeIds.Count}");
                        _bufferLock.Release();
                    }

                    // Délai entre les requêtes pour ne pas spammer l'API
                    await Task.Delay(DELAY_BETWEEN_REQUESTS_MS, _backgroundLoaderCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Erreur background loader: {ex.Message}");
                    await Task.Delay(1000, _backgroundLoaderCts.Token);
                }
            }
            
            System.Diagnostics.Debug.WriteLine("🛑 Background loader arrêté");
        }

        /// <summary>
        /// Récupère des animes depuis le buffer (instantané pour l'utilisateur)
        /// </summary>
        public async Task<List<AnimeCard>> GetAnimesFromBufferAsync(int count)
        {
            var result = new List<AnimeCard>();

            await _bufferLock.WaitAsync();
            try
            {
                // Prendre autant d'animes que possible depuis le buffer
                int available = Math.Min(count, _animeBuffer.Count);
                
                for (int i = 0; i < available; i++)
                {
                    result.Add(_animeBuffer.Dequeue());
                }

                System.Diagnostics.Debug.WriteLine($"✅ Fourni {result.Count}/{count} animes depuis le buffer. Reste: {_animeBuffer.Count}");
            }
            finally
            {
                _bufferLock.Release();
            }

            // Si on n'a pas assez d'animes, charger directement le reste
            int needed = count - result.Count;
            if (needed > 0)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Buffer insuffisant, chargement direct de {needed} animes...");
                
                for (int i = 0; i < needed; i++)
                {
                    var anime = await GetRandomAnimeAsync();
                    if (anime != null)
                    {
                        result.Add(anime);
                    }
                    
                    // Petit délai entre les requêtes
                    if (i < needed - 1)
                        await Task.Delay(DELAY_BETWEEN_REQUESTS_MS);
                }
            }

            return result;
        }

        /// <summary>
        /// Charge UN anime aléatoire (avec retry logic)
        /// </summary>
        private async Task<AnimeCard?> GetRandomAnimeAsync()
        {
            const int MAX_RETRIES = 3; // Réduit de 5 à 3 pour aller plus vite
            
            for (int retry = 0; retry < MAX_RETRIES; retry++)
            {
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

                    var requestBody = new { query = query, variables = new { page = randomPage } };
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var request = new HttpRequestMessage(HttpMethod.Post, ANILIST_API_URL)
                    {
                        Content = content
                    };
                    
                    request.Headers.Add("Accept", "application/json");
                    
                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Erreur API: {response.StatusCode}");
                        await Task.Delay(500); // Attendre avant retry
                        continue;
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject data = JObject.Parse(jsonResponse);

                    var page = data["data"]?["Page"];
                    if (page == null)
                        continue;

                    var mediaArray = page["media"];
                    if (mediaArray == null || !mediaArray.HasValues)
                        continue;

                    var media = mediaArray[0];
                    if (media == null)
                        continue;

                    var anime = ParseAnimeCard(media);
                    
                    // Vérifier si on a déjà cet anime
                    if (_loadedAnimeIds.Contains(anime.Id))
                    {
                        System.Diagnostics.Debug.WriteLine($"🔄 Doublon détecté (ID: {anime.Id}), retry...");
                        continue;
                    }
                    
                    // Ajouter à la liste des IDs chargés
                    _loadedAnimeIds.Add(anime.Id);
                    return anime;
                }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ HTTP Error: {ex.Message}");
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
                    await Task.Delay(200);
                }
            }

            return null;
        }

        private AnimeCard ParseAnimeCard(JToken media)
        {
            var titleObj = media["title"];
            string displayTitle = (!string.IsNullOrEmpty(titleObj?["english"]?.ToString()))
                ? titleObj["english"]!.ToString()
                : titleObj?["romaji"]?.ToString() ?? "Unknown";

            var coverObj = media["coverImage"];
            string imageUrl = coverObj?["extraLarge"]?.ToString() ?? coverObj?["large"]?.ToString() ?? "";

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
                BannerUrl = media["bannerImage"]?.ToString() ?? "",
                Score = score,
                Description = description,
                Season = season,
                Year = year,
                Status = status,
                Episodes = epDisplay,
                Genres = genres
            };
        }

        /// <summary>
        /// Obtient la taille actuelle du buffer
        /// </summary>
        public async Task<int> GetBufferSizeAsync()
        {
            await _bufferLock.WaitAsync();
            try
            {
                return _animeBuffer.Count;
            }
            finally
            {
                _bufferLock.Release();
            }
        }

        public void ClearCache()
        {
            _loadedAnimeIds.Clear();
            _animeBuffer.Clear();
            System.Diagnostics.Debug.WriteLine("🗑️ Cache et buffer vidés");
        }

        public async ValueTask DisposeAsync()
        {
            _backgroundLoaderCts?.Cancel();
            
            if (_backgroundLoaderTask != null)
            {
                try
                {
                    await _backgroundLoaderTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            
            _backgroundLoaderCts?.Dispose();
            _bufferLock?.Dispose();
        }
    }
}