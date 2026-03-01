namespace AniScroll.Shared.Models;

public class StatusListSetting
{
    public string Key         { get; set; } = string.Empty; // e.g. "Watching"
    public string DisplayName { get; set; } = string.Empty;
    public string Color       { get; set; } = string.Empty;
    public int    Order       { get; set; }
}