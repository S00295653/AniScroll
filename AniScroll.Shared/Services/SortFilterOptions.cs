using AniScroll.Shared.Components.Layout;

namespace AniScroll.Shared.Models;

public class SortFilterOptions
{
    public SortFilterPopup.SortMode Sort { get; set; } = SortFilterPopup.SortMode.RecentlyAdded;

    public HashSet<string> AiringStatuses { get; set; } = new();
    public HashSet<string> Formats { get; set; } = new();
    public HashSet<string> Seasons { get; set; } = new();
    public HashSet<int> Years { get; set; } = new();
    public HashSet<string> Genres { get; set; } = new();
    public HashSet<string> Tags { get; set; } = new();
    public HashSet<string> Sources { get; set; } = new();
    public HashSet<string> Platforms { get; set; } = new();
    public HashSet<string> Countries { get; set; } = new();
    public bool? Adult { get; set; } = null;

    public bool IsDefault =>
        Sort == SortFilterPopup.SortMode.RecentlyAdded &&
        !AiringStatuses.Any() && !Formats.Any() && !Seasons.Any() &&
        !Years.Any() && !Genres.Any() && !Tags.Any() &&
        !Sources.Any() && !Platforms.Any() && !Countries.Any() &&
        !Adult.HasValue;

    public int ActiveFilterCount =>
        (AiringStatuses.Any() ? 1 : 0) + (Formats.Any() ? 1 : 0) +
        (Seasons.Any() ? 1 : 0) + (Years.Any() ? 1 : 0) +
        (Genres.Any() ? 1 : 0) + (Tags.Any() ? 1 : 0) +
        (Sources.Any() ? 1 : 0) + (Platforms.Any() ? 1 : 0) +
        (Countries.Any() ? 1 : 0) + (Adult.HasValue ? 1 : 0);
}