namespace AniScroll.Shared.Models
{
    public class AnimeCard
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string NativeTitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string BannerUrl { get; set; } = string.Empty;
        public string CoverColor { get; set; } = string.Empty;   // dominant color from AniList CDN
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

        // New fields
        public List<AnimeTag> Tags { get; set; } = new List<AnimeTag>();
        public List<AnimeExternalLink> ExternalLinks { get; set; } = new List<AnimeExternalLink>();
        public List<AnimeRanking> Rankings { get; set; } = new List<AnimeRanking>();
        public int? NextAiringEpisodeNum { get; set; }
        public int? NextAiringTimeUntil { get; set; } // seconds
        public string? CountryOfOrigin { get; set; }
        public bool IsAdult { get; set; }
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

    public class AnimeTag
    {
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; }
        public bool IsMediaSpoiler { get; set; }
    }

    public class AnimeExternalLink
    {
        public string Url { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class AnimeRanking
    {
        public int Rank { get; set; }
        public string Type { get; set; } = string.Empty;   // RATED or POPULAR
        public string Context { get; set; } = string.Empty;
        public bool AllTime { get; set; }
        public string Season { get; set; } = string.Empty;
        public int? Year { get; set; }
    }

    public class JikanSearchResult
    {
        public int MalId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Score { get; set; } = "N/A";
        public string Type { get; set; } = string.Empty;
        public int? Episodes { get; set; }

        public double RelevanceScore { get; set; } = 0;
    }
}