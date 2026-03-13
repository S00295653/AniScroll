// AniScroll.Shared/Models/SwipeSettings.cs
namespace AniScroll.Shared.Models;

public class SwipeSettings
{
    /// <summary>Status applied when swiping RIGHT (default: Completed)</summary>
    public ListStatus RightStatus { get; set; } = ListStatus.Completed;

    /// <summary>Status applied when swiping LEFT (default: Planning)</summary>
    public ListStatus LeftStatus { get; set; } = ListStatus.Planning;
}