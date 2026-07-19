namespace DesktopAppFolder;

public class AppItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string IconFile { get; set; } = "";
    public string? HiddenSourcePath { get; set; }
}