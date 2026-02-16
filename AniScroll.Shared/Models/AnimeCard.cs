namespace AniScroll.Shared.Models
{
    public class AnimeCard
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleRomaji { get; set; } = string.Empty;
        public string TitleNative { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string BannerUrl { get; set; } = string.Empty;
        public string Score { get; set; } = "N/A";
        public string Description { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Episodes { get; set; } = "N/A";
        public int? TotalEpisodes { get; set; }
        public List<string> Genres { get; set; } = new List<string>();

        // Extended fields for detail popup
        public string Format { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int? Duration { get; set; }
        public int? Popularity { get; set; }
        public int? Favourites { get; set; }
        public int? MeanScore { get; set; }
        public List<string> Studios { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public string SiteUrl { get; set; } = string.Empty;

        public int? NextEpisode { get; set; }
        public long? NextAiringAt { get; set; }

        public List<AnimeRelation> Relations { get; set; } = new List<AnimeRelation>();
    }

    public class AnimeRelation
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}