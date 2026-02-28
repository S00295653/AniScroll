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
        public ListStatus? Status { get; set; }   // nullable: score can be saved without a status

        // Tracking
        public double Score { get; set; } = 0;          // 0 = no score, 0.5-10
        public int EpisodesWatched { get; set; } = 0;
        public int TotalRewatches { get; set; } = 0;

        // Dates
        public DateTime? StartDate { get; set; }
        public DateTime? FinishDate { get; set; }

        // Notes & options
        public string Notes { get; set; } = string.Empty;
        public bool IsPrivate { get; set; } = false;
        public bool HideFromStatusLists { get; set; } = false;

        // Metadata
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<string> CustomListIds { get; set; } = new();
    }
}