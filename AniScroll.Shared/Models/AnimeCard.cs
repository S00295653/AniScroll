namespace AniScroll.Shared.Models
{
    public class AnimeCard
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string BannerUrl { get; set; } = string.Empty;
        public string Score { get; set; } = "N/A";
        public string Description { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Episodes { get; set; } = "N/A";
        public List<string> Genres { get; set; } = new List<string>();
    }
}