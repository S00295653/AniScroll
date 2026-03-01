namespace AniScroll.Shared.Models;

public class UserCustomList
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = string.Empty;
    public string Color       { get; set; } = "#6366f1";
    public int    Order       { get; set; } = 0;
    /// <summary>
    /// true  → apparaît dans l'onglet Status du manager + dans le dropdown Status de ListsPanel.
    /// false → apparaît dans l'onglet Custom Lists + dans le dropdown Custom Lists (multi-select).
    /// </summary>
    public bool   IsStatusList { get; set; } = false;
}