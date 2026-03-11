namespace AniScroll.Shared.Models;

public class AniListUserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}

public class AniListImportEntry
{
    public int MediaId { get; set; }
    public string AniListStatus { get; set; } = string.Empty;   // CURRENT / COMPLETED / …
    public double Score { get; set; }
    public int Progress { get; set; }
    public int Repeat { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool HideFromStatusLists { get; set; }
    public List<string> CustomListNames { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Timestamps from AniList (Unix → UTC DateTime), mapped in AniListAuthService.ParseEntry()
    public DateTime UpdatedAt { get; set; } = DateTime.MinValue;
    public DateTime CreatedAt { get; set; } = DateTime.MinValue;

    public AnimeCard AnimeCard { get; set; } = new();
}