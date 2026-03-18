using AniScroll.Shared.Models;

namespace AniScroll.Shared.Models;

// ─── User preference model ────────────────────────────────────────────────────
// Extracted from the user's list, injected into the recommendation engine.
// Pure in-memory computation — no async, no allocations beyond what's needed.

/// <summary>
/// Preference profile derived from the user's list.
/// Build once per session via <see cref="BuildFrom"/>, then pass to
/// <see cref="AniScroll.Shared.Services.AniListService.GetMultipleAnimesAsync"/>.
/// </summary>
public class UserPreferences
{
    /// <summary>True when there is enough data to personalise the feed (min 5 engaged entries).</summary>
    public bool HasData { get; set; } = false;

    /// <summary>Top 3 genres by weighted score+status — primary signal for pool 1 and 2.</summary>
    public List<string> FavoriteGenres { get; set; } = new();

    /// <summary>Next 2 genres — secondary signal, widens the niche pool.</summary>
    public List<string> SecondaryGenres { get; set; } = new();

    /// <summary>
    /// Genres the user has NOT explored yet but co-occur with their favourites
    /// across the broader catalogue — drives the discovery pool (Instagram "suggested").
    /// </summary>
    public List<string> DiscoveryGenres { get; set; } = new();

    /// <summary>Top 6 non-spoiler tags by weighted occurrence — reserved for future tag-based pools.</summary>
    public List<string> FavoriteTags { get; set; } = new();

    /// <summary>
    /// Genres to penalise: present in entries rated below 4 or explicitly Dropped.
    /// Appended to genre_not_in alongside the always-excluded set.
    /// </summary>
    public List<string> DislikedGenres { get; set; } = new();

    /// <summary>True when TV Series dominates the user's completed list.</summary>
    public bool PrefersTV { get; set; } = true;

    /// <summary>User's average personal score on a 1–10 scale (0 = no scores recorded).</summary>
    public double AvgPersonalScore { get; set; } = 0;

    /// <summary>
    /// Minimum AniList average score to show in personalised pools.
    /// Calibrated from the user's own ratings: a user who only watches 8+ titles
    /// doesn't benefit from seeing 60/100 anime.
    /// </summary>
    public int MinScoreThreshold { get; set; } = 60;

    // ── Always-excluded genres (regardless of user preferences) ──────────────
    private static readonly HashSet<string> _alwaysExcluded =
        new(StringComparer.OrdinalIgnoreCase) { "Hentai", "Ecchi" };

    // ─── Query filter helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Builds a GraphQL <c>genre_in: [...]</c> fragment using the top
    /// <paramref name="favCount"/> favourite genres.  Empty string when there are no prefs.
    /// </summary>
    public string GenreInFav(int favCount = 2)
    {
        if (!HasData || !FavoriteGenres.Any()) return "";
        var genres = FavoriteGenres.Take(favCount).Select(EscapeQuoted);
        return $", genre_in: [{string.Join(",", genres)}]";
    }

    /// <summary>
    /// Builds a <c>genre_in</c> fragment for the niche pool using the top favourite
    /// plus secondary genres (up to <paramref name="total"/> combined).
    /// </summary>
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

    /// <summary>
    /// Builds a <c>genre_in</c> fragment for the discovery pool (adjacent genres).
    /// Falls back to an empty string so the pool uses no genre filter when empty.
    /// </summary>
    public string GenreInDiscovery()
    {
        if (!HasData || !DiscoveryGenres.Any()) return "";
        return $", genre_in: [{string.Join(",", DiscoveryGenres.Select(EscapeQuoted))}]";
    }

    /// <summary>
    /// Builds the <c>genre_not_in</c> fragment: always-excluded genres + disliked genres.
    /// Safe to use even when there are no preferences (returns the baseline exclusions).
    /// </summary>
    public string GenreNotIn()
    {
        var excluded = _alwaysExcluded
            .Concat(DislikedGenres)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(EscapeQuoted);
        return $", genre_not_in: [{string.Join(",", excluded)}]";
    }

    private static string EscapeQuoted(string g) => $"\"{g.Replace("\"", "\\\"")}\"";

    // ─── Factory method ───────────────────────────────────────────────────────

    /// <summary>
    /// Derives a <see cref="UserPreferences"/> instance from the user's list entries.
    /// Algorithm:
    ///
    ///   1. Filter to "engaged" entries (Completed, Watching, Rewatching, or score ≥ 7).
    ///      Require at least 5 such entries — below that, cold-start mode is better.
    ///
    ///   2. Score each genre with: statusWeight × scoreMultiplier.
    ///      statusWeight : Rewatching=3 · Completed=2 · Watching=1.5 · others=1
    ///      scoreMultiplier : score/5 when scored, 1.0 otherwise
    ///
    ///   3. Subtract a penalty from disliked genres (Dropped entries and score ≤ 4)
    ///      so that genres the user clearly dislikes don't pollute the feed.
    ///
    ///   4. Build DiscoveryGenres via co-occurrence: genres that frequently appear
    ///      alongside the user's favourites in their watched catalogue but which
    ///      the user hasn't explored much themselves.  This mirrors how Instagram
    ///      surfaces "adjacent" content — related but not yet consumed.
    ///
    ///   5. Calibrate MinScoreThreshold from the user's own average score so that
    ///      the feed only shows anime matching their personal quality bar.
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

        // ── 2. Accumulate weighted genre / tag / format counts ─────────────────
        var genreWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var tagWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var formatCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        double scoreSum = 0;
        int scoreCount = 0;

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
                genreWeights[g] = genreWeights.GetValueOrDefault(g) + w;
            }

            // Tags: weight by rank (60–100%) — ignore low-confidence tags
            foreach (var t in e.Anime.Tags?.Where(t => !t.IsMediaSpoiler && t.Rank >= 60)
                                          ?? Enumerable.Empty<AnimeTag>())
            {
                tagWeights[t.Name] = tagWeights.GetValueOrDefault(t.Name) + w * (t.Rank / 100.0);
            }

            if (!string.IsNullOrEmpty(e.Anime.Format))
                formatCounts[e.Anime.Format] = formatCounts.GetValueOrDefault(e.Anime.Format) + 1;

            if (e.Score > 0) { scoreSum += e.Score; scoreCount++; }
        }

        // ── 3. Dislike penalty ─────────────────────────────────────────────────
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

        // Subtract 50% of dislike weight from the liked genre score
        foreach (var kv in dislikedWeights)
            if (genreWeights.ContainsKey(kv.Key))
                genreWeights[kv.Key] = Math.Max(0, genreWeights[kv.Key] - kv.Value * 0.5);

        // ── 4. Sort and partition genres ───────────────────────────────────────
        var sortedGenres = genreWeights
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        var fav = sortedGenres.Take(3).ToList();
        var sec = sortedGenres.Skip(3).Take(2).ToList();

        // ── 5. Discovery genres (co-occurrence with favourites) ────────────────
        var discovery = BuildDiscovery(engaged, fav, sec);

        // ── 6. Disliked genre list (net negative signal) ───────────────────────
        var dislikedFinal = dislikedWeights
            .Where(kv => kv.Value > 1.5 && !fav.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)
                                         && !sec.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => kv.Key)
            .ToList();

        // ── 7. Calibrate score threshold from user's own taste ─────────────────
        double avg = scoreCount > 0 ? scoreSum / scoreCount : 0;
        int minThreshold = avg >= 8.5 ? 78
                         : avg >= 7.5 ? 70
                         : avg >= 6.5 ? 62
                         : 58;

        // ── 8. Format preference ───────────────────────────────────────────────
        int tvCount = formatCounts.GetValueOrDefault("TV Series", 0);
        int otherCount = formatCounts.Values.Sum() - tvCount;
        bool prefersTV = tvCount > 0 && tvCount >= otherCount;

        // ── 9. Top tags ────────────────────────────────────────────────────────
        var topTags = tagWeights
            .OrderByDescending(kv => kv.Value)
            .Take(6)
            .Select(kv => kv.Key)
            .ToList();

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
    /// Finds genres that co-occur with the user's favourites in their watched catalogue,
    /// but which the user hasn't explicitly explored yet.  These become the "discovery" pool.
    /// </summary>
    private static List<string> BuildDiscovery(
        List<UserListEntry> engaged,
        List<string> fav,
        List<string> sec)
    {
        var known = new HashSet<string>(
            fav.Concat(sec).Concat(_alwaysExcluded),
            StringComparer.OrdinalIgnoreCase);

        // Count how often each "unknown" genre appears in anime that contain a favourite genre
        var coOccurrence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in engaged)
        {
            var genres = e.Anime.Genres ?? new List<string>();
            bool touchesFav = genres.Any(g =>
                fav.Contains(g, StringComparer.OrdinalIgnoreCase));
            if (!touchesFav) continue;

            foreach (var g in genres)
            {
                if (known.Contains(g)) continue;
                coOccurrence[g] = coOccurrence.GetValueOrDefault(g) + 1;
            }
        }

        return coOccurrence
            .OrderByDescending(kv => kv.Value)
            .Take(2)
            .Select(kv => kv.Key)
            .ToList();
    }
}