namespace AniScroll.Shared.Models
{
    public enum ListStatus
    {
        Watching,
        Rewatching,
        Completed,
        Planning,
        Paused,
        Dropped
    }

    public class UserListEntry
    {
        public AnimeCard Anime { get; set; } = new();
        public ListStatus Status { get; set; }

        // Tracking
        public int Score { get; set; } = 0;           // 0 = no score, 1-10
        public int EpisodesWatched { get; set; } = 0;
        public int TotalRewatches { get; set; } = 0;

        // Dates
        public DateTime? StartDate { get; set; }
        public DateTime? FinishDate { get; set; }

        // Notes & options
        public string Notes { get; set; } = string.Empty;
        public bool IsPrivate { get; set; } = false;
        public bool HideFromStatusLists { get; set; } = false;

        // Custom lists
        public bool WatchSoon { get; set; } = false;
        public bool Movies { get; set; } = false;

        // Metadata
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}