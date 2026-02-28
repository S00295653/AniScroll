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
        public int EpisodesWatched { get; set; } = 0;
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}


