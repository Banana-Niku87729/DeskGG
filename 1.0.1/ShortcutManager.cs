using System.Runtime.InteropServices;
using System.Text;

namespace DesktopAppFolder;
    
internal static class ShortcutManager
{
    private static string DesktopDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static void CreateOrUpdateShortcut(FolderData data, string iconPath)
    {
        string baseName = SanitizeFileName(data.FolderName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "DeskGG";

        string? currentPath = !string.IsNullOrEmpty(data.ShortcutFileName)
            ? Path.Combine(DesktopDir, data.ShortcutFileName)
            : null;

        // 名前(=ファイル名)が変わっておらず実体も存在するなら、
        // 削除→再作成はせず同じファイルに上書き保存する。
        // Explorerにとって「同一ファイルの更新」に見えるため、
        // デスクトップ上の配置位置が維持される。
        string currentBaseName = string.IsNullOrEmpty(data.ShortcutFileName)
            ? ""
            : Path.GetFileNameWithoutExtension(data.ShortcutFileName);

        bool needsNewFile = currentPath == null
            || !File.Exists(currentPath)
            || !string.Equals(currentBaseName, baseName, StringComparison.Ordinal);

        string fileName;
        if (needsNewFile)
        {
            // 名前が変わった/実体が無い場合のみ、古いものを片付けて新規作成する
            DeleteShortcut(data);
            DeleteAllShortcutsForId(data.Id);
            fileName = FindAvailableFileName(baseName, data.Id);
            data.ShortcutFileName = fileName;
        }
        else
        {
            fileName = data.ShortcutFileName;
        }

        string fullPath = Path.Combine(DesktopDir, fileName);

        var link = (IShellLinkW)new ShellLink();
        link.SetPath(Application.ExecutablePath);
        link.SetArguments($"--show-folder {data.Id}");
        link.SetWorkingDirectory(Path.GetDirectoryName(Application.ExecutablePath)!);
        link.SetDescription(data.FolderName);
        link.SetIconLocation(iconPath, 0);

        var file = (IPersistFile)link;
        file.Save(fullPath, false); // 同名なら上書き＝ファイル実体は維持され、位置もそのまま
        Marshal.ReleaseComObject(link);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    /// <summary>同名の.lnkが既にある場合は " (2)" のように連番を付けて衝突を避ける。</summary>
    private static string FindAvailableFileName(string baseName, Guid ownerId)
    {
        string candidate = $"{baseName}.lnk";
        int n = 2;
        while (File.Exists(Path.Combine(DesktopDir, candidate)))
        {
            candidate = $"{baseName} ({n}).lnk";
            n++;
        }
        return candidate;
    }

    public static void DeleteShortcut(FolderData data)
    {
        if (string.IsNullOrEmpty(data.ShortcutFileName)) return;
        try
        {
            string path = Path.Combine(DesktopDir, data.ShortcutFileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// デスクトップ上の全.lnkを調べ、引数に指定GUIDを含むもの（=このフォルダーのショートカット）を
    /// すべて削除する。リネーム時などに孤立した古いショートカットが残るのを防ぐための保険。
    /// </summary>
    private static void DeleteAllShortcutsForId(Guid id)
    {
        string idText = id.ToString();
        if (!Directory.Exists(DesktopDir)) return;

        foreach (var path in Directory.GetFiles(DesktopDir, "*.lnk"))
        {
            try
            {
                string? args = TryGetArguments(path);
                if (args != null && args.Contains(idText, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                }
            }
            catch { /* このファイルはスキップ */ }
        }
    }

    private static string? TryGetArguments(string lnkPath)
    {
        var link = (IShellLinkW)new ShellLink();
        var file = (IPersistFile)link;
        file.Load(lnkPath, 0);

        var sb = new StringBuilder(1024);
        link.GetArguments(sb, sb.Capacity);
        Marshal.ReleaseComObject(link);
        return sb.ToString();
    }

    /// <summary>.lnkが指す実体パスを解決する。UWPアプリ等、実体が取れない場合はnull。</summary>
    public static string? ResolveShortcutTarget(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            var file = (IPersistFile)link;
            file.Load(lnkPath, 0);

            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            Marshal.ReleaseComObject(link);

            string target = sb.ToString();
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    // --- COM相互運用 ---

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(StringBuilder pszName, int cchMaxName);
        void SetDescription(string pszName);
        void GetWorkingDirectory(StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory(string pszDir);
        void GetArguments(StringBuilder pszArgs, int cchMaxPath);
        void SetArguments(string pszArgs);
        void GetHotkey(out short wHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int iShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation(string pszIconPath, int iIcon);
        void SetRelativePath(string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath(string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load(string pszFileName, int dwMode);
        void Save(string pszFileName, bool fRemember);
        void SaveCompleted(string pszFileName);
        void GetCurFile(out string ppszFileName);
    }
}