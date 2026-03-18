using AniScroll.Shared.Models;

namespace AniScroll.Shared.Models;

// ─── User preference model ────────────────────────────────────────────────────

/// <summary>
/// Preference profile derived from the user's list.
/// Build once per session via <see cref="BuildFrom"/>, then pass to
/// AniListService.GetMultipleAnimesAsync.
/// </summary>
public class UserPreferences
{
    /// <summary>True when there is enough data to personalise the feed (min 5 engaged entries).</summary>
    public bool HasData { get; set; } = false;

    /// <summary>Top 3 genres by TF-IDF weighted score — primary signal for pools 1 and 2.</summary>
    public List<string> FavoriteGenres { get; set; } = new();

    /// <summary>Next 2 genres — secondary signal, widens the niche pool.</summary>
    public List<string> SecondaryGenres { get; set; } = new();

    /// <summary>Genres the user hasn't explored but co-occur with favourites — drives discovery pool.</summary>
    public List<string> DiscoveryGenres { get; set; } = new();

    /// <summary>Top 6 non-spoiler tags by TF-IDF weighted occurrence.</summary>
    public List<string> FavoriteTags { get; set; } = new();

    /// <summary>Genres present in dropped/low-scored entries — appended to genre_not_in.</summary>
    public List<string> DislikedGenres { get; set; } = new();

    /// <summary>True when TV Series dominates the user's completed list.</summary>
    public bool PrefersTV { get; set; } = true;

    /// <summary>User's average personal score on a 1–10 scale (0 = no scores recorded).</summary>
    public double AvgPersonalScore { get; set; } = 0;

    /// <summary>Minimum AniList average score to show in personalised pools.</summary>
    public int MinScoreThreshold { get; set; } = 60;

    // ── Always-excluded genres ────────────────────────────────────────────────
    private static readonly HashSet<string> _alwaysExcluded =
        new(StringComparer.OrdinalIgnoreCase) { "Hentai", "Ecchi" };

    // ── Tags that are near-universal and carry no signal about personal taste ──
    // These appear in 60–90% of all anime, making them statistically meaningless
    // as preference indicators. We hard-block them regardless of TF-IDF score.
    private static readonly HashSet<string> _ubiquitousTags =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Protagonist gender / perspective
            "Male Protagonist", "Female Protagonist",
            "Non-Human Protagonist", "Ensemble Cast",

            // Trivially common narrative devices
            "Based on a Manga", "Based on a Light Novel",
            "School", "High School", "Middle School",
            "Japan", "Japanese",

            // Format / production trivia (not taste signals)
            "Anime Original", "Short Episodes",

            // Ultra-common settings with no taste signal
            "Modern", "Contemporary",
        };

    // ── IDF baseline: approximate proportion of anime that contain each genre ──
    // Values are rough estimates from the AniList catalogue distribution (2024).
    // Higher value = more common = lower IDF weight.
    // Genres not listed default to 0.25 (moderately uncommon).
    private static readonly Dictionary<string, double> _genreDocFreq =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Very common → low discriminative power
            { "Action",       0.55 },
            { "Adventure",    0.50 },
            { "Comedy",       0.65 },
            { "Fantasy",      0.48 },
            { "Drama",        0.45 },
            { "Romance",      0.35 },
            { "Sci-Fi",       0.28 },

            // Moderately common
            { "Supernatural", 0.25 },
            { "Slice of Life",0.22 },
            { "Sports",       0.14 },
            { "Music",        0.10 },
            { "Mecha",        0.10 },

            // Niche → high discriminative power (low doc freq)
            { "Psychological",0.08 },
            { "Horror",       0.09 },
            { "Thriller",     0.07 },
            { "Mystery",      0.12 },
            { "Mahou Shoujo", 0.06 },
            { "Ecchi",        0.12 },
            { "Hentai",       0.04 },
        };

    // ── IDF baseline for tags: approximate catalogue prevalence ──────────────
    // Only the most ubiquitous ones need explicit entries; everything else gets
    // the default 0.20, which is already low enough to produce a useful IDF.
    private static readonly Dictionary<string, double> _tagDocFreq =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Near-universal tags (>50% of anime)
            { "Male Protagonist",      0.85 },
            { "Female Protagonist",    0.60 },
            { "Non-Human Protagonist", 0.55 },
            { "Ensemble Cast",         0.52 },

            // Very common tags (30–50%)
            { "School",                0.45 },
            { "High School",           0.42 },
            { "Japan",                 0.40 },
            { "Based on a Manga",      0.38 },
            { "Friendship",            0.38 },
            { "Love Interest",         0.36 },
            { "Heterosexual",          0.35 },
            { "Senpai-Kouhai",         0.30 },
            { "Coming of Age",         0.30 },

            // Common (15–30%)
            { "Power Levels",          0.28 },
            { "Rivalry",               0.25 },
            { "Over-Powered",          0.22 },
            { "Tournament",            0.20 },
        };

    // ─── Query filter helpers ─────────────────────────────────────────────────

    public string GenreInFav(int favCount = 2)
    {
        if (!HasData || !FavoriteGenres.Any()) return "";
        var genres = FavoriteGenres.Take(favCount).Select(EscapeQuoted);
        return $", genre_in: [{string.Join(",", genres)}]";
    }

    public string GenreInFavAndSecondary(int total = 4)
    {
        if (!HasData || !FavoriteGenres.Any()) return "";
        var genres = FavoriteGenres
            .Concat(SecondaryGenres)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(total)
            .Select(EscapeQuoted);
        return $", genre_in: [{string.Join(",", genres)}]";
    }

    public string GenreInDiscovery()
    {
        if (!HasData || !DiscoveryGenres.Any()) return "";
        return $", genre_in: [{string.Join(",", DiscoveryGenres.Select(EscapeQuoted))}]";
    }

    public string GenreNotIn()
    {
        var excluded = _alwaysExcluded
            .Concat(DislikedGenres)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(EscapeQuoted);
        return $", genre_not_in: [{string.Join(",", excluded)}]";
    }

    private static string EscapeQuoted(string g) => $"\"{g.Replace("\"", "\\\"")}\"";

    // ─── TF-IDF helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// IDF = log(1 / docFreq).
    /// A genre/tag present in 85% of anime gets IDF ≈ 0.07 → nearly ignored.
    /// A genre present in 8% gets IDF ≈ 1.10 → strongly boosted.
    /// </summary>
    private static double GenreIdf(string genre)
    {
        double df = _genreDocFreq.TryGetValue(genre, out var v) ? v : 0.25;
        return Math.Log(1.0 / Math.Max(0.01, df));
    }

    private static double TagIdf(string tag)
    {
        // Hard-block ubiquitous tags immediately — saves computation
        if (_ubiquitousTags.Contains(tag)) return 0.0;
        double df = _tagDocFreq.TryGetValue(tag, out var v) ? v : 0.20;
        return Math.Log(1.0 / Math.Max(0.01, df));
    }

    // ─── Factory method ───────────────────────────────────────────────────────

    /// <summary>
    /// Derives a UserPreferences instance from the user's list entries.
    ///
    /// Scoring pipeline (per genre / tag):
    ///   rawTF     = statusWeight × scoreMultiplier  (same as before)
    ///   tfidfScore = rawTF × IDF(genre)
    ///
    /// IDF penalises genres/tags that appear in a large fraction of the AniList
    /// catalogue, making "Action" and "Comedy" far less dominant when the user
    /// simply watches mainstream anime, while niche genres like "Psychological"
    /// or "Mahou Shoujo" are correctly surfaced even at lower raw frequency.
    ///
    /// Ubiquitous tags (Male Protagonist, Female Protagonist, School, etc.) are
    /// filtered out before scoring — they carry zero signal about personal taste.
    /// </summary>
    public static UserPreferences BuildFrom(IEnumerable<UserListEntry> allEntries)
    {
        var list = allEntries.ToList();

        // ── 1. Partition entries ───────────────────────────────────────────────
        var engaged = list.Where(e =>
            e.Status == ListStatus.Completed ||
            e.Status == ListStatus.Watching ||
            e.Status == ListStatus.Rewatching ||
            e.Score >= 7
        ).ToList();

        if (engaged.Count < 5) return new UserPreferences();   // cold-start

        var disliked = list.Where(e =>
            e.Status == ListStatus.Dropped ||
            (e.Score > 0 && e.Score <= 4)
        ).ToList();

        // ── 2. Accumulate raw TF weights ──────────────────────────────────────
        var genreRawTF = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var tagRawTF = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var formatCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        double scoreSum = 0; int scoreCount = 0;

        foreach (var e in engaged)
        {
            double sw = e.Status switch
            {
                ListStatus.Rewatching => 3.0,
                ListStatus.Completed => 2.0,
                ListStatus.Watching => 1.5,
                _ => 1.0
            };
            double scoreW = e.Score > 0 ? Math.Max(0.5, e.Score / 5.0) : 1.0;
            double w = sw * scoreW;

            foreach (var g in e.Anime.Genres ?? Enumerable.Empty<string>())
            {
                if (_alwaysExcluded.Contains(g)) continue;
                genreRawTF[g] = genreRawTF.GetValueOrDefault(g) + w;
            }

            // Tags: only rank ≥ 60, non-spoiler, non-ubiquitous
            foreach (var t in e.Anime.Tags?
                                     .Where(t => !t.IsMediaSpoiler && t.Rank >= 60)
                                  ?? Enumerable.Empty<AnimeTag>())
            {
                if (_ubiquitousTags.Contains(t.Name)) continue;   // pre-filter
                tagRawTF[t.Name] = tagRawTF.GetValueOrDefault(t.Name) + w * (t.Rank / 100.0);
            }

            if (!string.IsNullOrEmpty(e.Anime.Format))
                formatCounts[e.Anime.Format] = formatCounts.GetValueOrDefault(e.Anime.Format) + 1;

            if (e.Score > 0) { scoreSum += e.Score; scoreCount++; }
        }

        // ── 3. Apply IDF → TF-IDF scores ──────────────────────────────────────
        var genreTfIdf = genreRawTF
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value * GenreIdf(kv.Key),
                StringComparer.OrdinalIgnoreCase);

        var tagTfIdf = tagRawTF
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value * TagIdf(kv.Key),
                StringComparer.OrdinalIgnoreCase);

        // ── 4. Dislike penalty ─────────────────────────────────────────────────
        var dislikedWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in disliked)
        {
            double dw = e.Status == ListStatus.Dropped ? 2.0 : 1.0;
            foreach (var g in e.Anime.Genres ?? Enumerable.Empty<string>())
            {
                if (_alwaysExcluded.Contains(g)) continue;
                dislikedWeights[g] = dislikedWeights.GetValueOrDefault(g) + dw;
            }
        }

        // Subtract 50% of dislike weight from liked genre TF-IDF score
        foreach (var kv in dislikedWeights)
            if (genreTfIdf.ContainsKey(kv.Key))
                genreTfIdf[kv.Key] = Math.Max(0, genreTfIdf[kv.Key] - kv.Value * 0.5 * GenreIdf(kv.Key));

        // ── 5. Sort and partition genres ───────────────────────────────────────
        var sortedGenres = genreTfIdf
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        var fav = sortedGenres.Take(3).ToList();
        var sec = sortedGenres.Skip(3).Take(2).ToList();

        // ── 6. Discovery genres ────────────────────────────────────────────────
        var discovery = BuildDiscovery(engaged, fav, sec);

        // ── 7. Disliked genre list ─────────────────────────────────────────────
        var dislikedFinal = dislikedWeights
            .Where(kv => kv.Value > 1.5
                      && !fav.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)
                      && !sec.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => kv.Key)
            .ToList();

        // ── 8. Top tags (TF-IDF, ubiquitous already stripped) ─────────────────
        var topTags = tagTfIdf
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => kv.Key)
            .ToList();

        // ── 9. Calibrate score threshold ───────────────────────────────────────
        double avg = scoreCount > 0 ? scoreSum / scoreCount : 0;
        int minThreshold = avg >= 8.5 ? 78
                         : avg >= 7.5 ? 70
                         : avg >= 6.5 ? 62
                         : 58;

        // ── 10. Format preference ──────────────────────────────────────────────
        int tvCount = formatCounts.GetValueOrDefault("TV Series", 0);
        int otherCount = formatCounts.Values.Sum() - tvCount;
        bool prefersTV = tvCount > 0 && tvCount >= otherCount;

        return new UserPreferences
        {
            HasData = true,
            FavoriteGenres = fav,
            SecondaryGenres = sec,
            DiscoveryGenres = discovery,
            FavoriteTags = topTags,
            DislikedGenres = dislikedFinal,
            PrefersTV = prefersTV,
            AvgPersonalScore = avg,
            MinScoreThreshold = minThreshold,
        };
    }

    /// <summary>
    /// Finds genres that co-occur with the user's favourites but haven't been
    /// directly explored yet — drives the "adjacent content" discovery pool.
    /// Uses TF-IDF here too: co-occurring ubiquitous genres are ignored.
    /// </summary>
    private static List<string> BuildDiscovery(
        List<UserListEntry> engaged,
        List<string> fav,
        List<string> sec)
    {
        var known = new HashSet<string>(
            fav.Concat(sec).Concat(_alwaysExcluded),
            StringComparer.OrdinalIgnoreCase);

        var coOccurrence = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in engaged)
        {
            var genres = e.Anime.Genres ?? new List<string>();
            bool touchesFav = genres.Any(g =>
                fav.Contains(g, StringComparer.OrdinalIgnoreCase));
            if (!touchesFav) continue;

            foreach (var g in genres)
            {
                if (known.Contains(g)) continue;
                // Weight co-occurrence by IDF so common genres don't dominate
                coOccurrence[g] = coOccurrence.GetValueOrDefault(g) + GenreIdf(g);
            }
        }

        return coOccurrence
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(2)
            .Select(kv => kv.Key)
            .ToList();
    }
}