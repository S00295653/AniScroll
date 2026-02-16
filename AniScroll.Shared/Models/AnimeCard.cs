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
        
        // Extended details
        public string Format { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int? Duration { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int? Popularity { get; set; }
        public int? Favourites { get; set; }
        public List<AnimeStudio> Studios { get; set; } = new List<AnimeStudio>();
        public List<AnimeRelation> Relations { get; set; } = new List<AnimeRelation>();
        public string TrailerUrl { get; set; } = string.Empty;
    }

    public class AnimeStudio
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AnimeRelation
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}