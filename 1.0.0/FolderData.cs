namespace DesktopAppFolder;

public class FolderData
{
    public const int MaxApps = 60;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string FolderName { get; set; } = "DeskGG";
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public bool PositionKnown { get; set; } = false;
    public List<AppItem> Apps { get; set; } = new();
    public string ShortcutFileName { get; set; } = "";

    /// <summary>フォルダーのテーマカラー(ARGB int)。0なら既定色。</summary>
    public int ThemeColor { get; set; } = 0;

    public bool IsFull => Apps.Count >= MaxApps;
}