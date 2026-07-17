using System.Drawing.Imaging;
using System.Text.Json;

namespace DesktopAppFolder;

public static class Storage
{
    public static readonly string RootDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopAppFolder");

    public static readonly string FoldersDir = Path.Combine(RootDir, "Folders");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static Storage()
    {
        Directory.CreateDirectory(FoldersDir);
    }

    private static string FolderDir(Guid id) => Path.Combine(FoldersDir, id.ToString());
    private static string DataFile(Guid id) => Path.Combine(FolderDir(id), "data.json");
    private static string IconsDir(Guid id) => Path.Combine(FolderDir(id), "Icons");
    public static string ThumbnailFile(Guid id) => Path.Combine(FolderDir(id), "thumbnail.png");
    public static string IconOutputDir(Guid id) => Path.Combine(FolderDir(id), "GeneratedIcon");

    public static List<FolderData> LoadAll()
    {
        var list = new List<FolderData>();
        if (!Directory.Exists(FoldersDir)) return list;

        foreach (var dir in Directory.GetDirectories(FoldersDir))
        {
            string file = Path.Combine(dir, "data.json");
            if (!File.Exists(file)) continue;
            try
            {
                var json = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<FolderData>(json);
                if (data != null) list.Add(data);
            }
            catch { /* 壊れているフォルダーはスキップ */ }
        }
        return list;
    }

    public static FolderData CreateNew(string name, Point location)
    {
        var data = new FolderData
        {
            Id = Guid.NewGuid(),
            FolderName = name,
            X = location.X,
            Y = location.Y
        };
        Directory.CreateDirectory(FolderDir(data.Id));
        Directory.CreateDirectory(IconsDir(data.Id));
        Save(data);
        return data;
    }

    public static void Save(FolderData data)
    {
        Directory.CreateDirectory(FolderDir(data.Id));
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(DataFile(data.Id), json);
    }

    public static void DeleteFolder(FolderData data)
    {
        try
        {
            var dir = FolderDir(data.Id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch { /* ignore */ }
    }

    public static string ExtractAndSaveIcon(Guid folderId, string targetPath)
    {
        Directory.CreateDirectory(IconsDir(folderId));

        // .lnk自体からアイコンを抽出すると、Windowsが矢印オーバーレイを
        // 焼き込んだ状態で返してくることがあるため、リンク先の実体パスを解決してから抽出する
        string extractPath = targetPath;
        if (string.Equals(Path.GetExtension(targetPath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string? resolved = ShortcutManager.ResolveShortcutTarget(targetPath);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                extractPath = resolved;
            }
        }

        Icon? icon = null;
        try { icon = Icon.ExtractAssociatedIcon(extractPath); }
        catch { /* フォールバックへ */ }

        string fileName = Guid.NewGuid().ToString("N") + ".png";
        string fullPath = Path.Combine(IconsDir(folderId), fileName);

        using (Bitmap bmp = icon != null ? icon.ToBitmap() : SystemIcons.Application.ToBitmap())
        {
            bmp.Save(fullPath, ImageFormat.Png);
        }
        icon?.Dispose();

        return fileName;
    }

    public static Image LoadIcon(Guid folderId, string iconFile)
    {
        string fullPath = Path.Combine(IconsDir(folderId), iconFile);
        if (File.Exists(fullPath))
        {
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return Image.FromStream(fs);
        }
        return SystemIcons.Application.ToBitmap();
    }

    public static string SaveShortcutCopy(Guid folderId, string sourceLnkPath)
    {
        string dir = Path.Combine(FolderDir(folderId), "Shortcuts");
        Directory.CreateDirectory(dir);
        string fileName = Guid.NewGuid().ToString("N") + ".lnk";
        string fullPath = Path.Combine(dir, fileName);
        File.Copy(sourceLnkPath, fullPath, overwrite: true);
        return fullPath;
    }

    public static void DeleteIcon(Guid folderId, string iconFile)
    {
        try
        {
            string fullPath = Path.Combine(IconsDir(folderId), iconFile);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch { /* ignore */ }
    }

    public static void SaveGridThumbnail(Guid folderId, Bitmap bmp)
    {
        try
        {
            Directory.CreateDirectory(FolderDir(folderId));
            bmp.Save(ThumbnailFile(folderId), ImageFormat.Png);
        }
        catch { /* ignore */ }
    }
}