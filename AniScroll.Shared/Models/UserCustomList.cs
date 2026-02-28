namespace AniScroll.Shared.Models;

public class UserCustomList
{
    public string Id    { get; set; } = Guid.NewGuid().ToString();
    public string Name  { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public int    Order { get; set; } = 0;
}