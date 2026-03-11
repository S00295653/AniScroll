using static AniScroll.Shared.Components.Layout.SortFilterPopup;

namespace AniScroll.Shared.Models
{
    public class SortFilterOptions
    {
        // ── Sort ─────────────────────────────────────────────────────────────
        public SortMode Sort { get; set; } = SortMode.RecentlyUpdated;

        // ── Chip filters ─────────────────────────────────────────────────────
        public HashSet<string> AiringStatuses { get; set; } = new();
        public HashSet<string> Formats { get; set; } = new();
        public HashSet<string> Seasons { get; set; } = new();
        public HashSet<string> Genres { get; set; } = new();
        public HashSet<string> Studios { get; set; } = new();
        public HashSet<string> Tags { get; set; } = new();
        public HashSet<string> Sources { get; set; } = new();
        public HashSet<string> Platforms { get; set; } = new();
        public HashSet<string> Countries { get; set; } = new();
        public bool? Adult { get; set; }

        // ── Range filters (null = no filter applied) ─────────────────────────
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public double? ScoreFrom { get; set; }
        public double? ScoreTo { get; set; }
        public int? EpFrom { get; set; }
        public int? EpTo { get; set; }

        // ── Helpers ───────────────────────────────────────────────────────────
        public bool IsDefault =>
            Sort == SortMode.RecentlyUpdated &&
            !AiringStatuses.Any() && !Formats.Any() && !Seasons.Any() &&
            !Genres.Any() && !Studios.Any() && !Tags.Any() &&
            !Sources.Any() && !Platforms.Any() && !Countries.Any() &&
            !Adult.HasValue &&
            !YearFrom.HasValue && !YearTo.HasValue &&
            !ScoreFrom.HasValue && !ScoreTo.HasValue &&
            !EpFrom.HasValue && !EpTo.HasValue;

        public int ActiveFilterCount
        {
            get
            {
                int n = 0;
                if (Sort != SortMode.RecentlyUpdated) n++;
                if (AiringStatuses.Any()) n++;
                if (Formats.Any()) n++;
                if (Seasons.Any()) n++;
                if (Genres.Any()) n++;
                if (Studios.Any()) n++;
                if (Tags.Any()) n++;
                if (Sources.Any()) n++;
                if (Platforms.Any()) n++;
                if (Countries.Any()) n++;
                if (Adult.HasValue) n++;
                if (YearFrom.HasValue || YearTo.HasValue) n++;
                if (ScoreFrom.HasValue || ScoreTo.HasValue) n++;
                if (EpFrom.HasValue || EpTo.HasValue) n++;
                return n;
            }
        }
    }
}