using System.Runtime.InteropServices;
using System.Text;

namespace DesktopAppFolder;

/// <summary>
/// 指定したファイルに対する「エクスプローラー標準の右クリックメニュー」を表示したり、
/// 「プロパティ」ダイアログをネイティブAPIで呼び出すためのヘルパー。
/// シェル拡張(ウイルス対策ソフトが追加する項目など)も含めて、実際にエクスプローラーで
/// 右クリックした時と同じメニューを再現する。
/// </summary>
internal static class ShellContextMenu
{
    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const int SW_SHOWNORMAL = 1;

    /// <summary>指定パスに対するエクスプローラー標準の右クリックメニューをその場に表示し、選択されたコマンドを実行する。</summary>
    public static void Show(IntPtr ownerHwnd, string path, Point screenPoint)
    {
        if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path))) return;

        IntPtr pidlFull = IntPtr.Zero;
        IShellFolder? parentFolder = null;
        IContextMenu? contextMenu = null;
        IntPtr hMenu = IntPtr.Zero;

        try
        {
            if (SHParseDisplayName(path, IntPtr.Zero, out pidlFull, 0, out _) != 0 || pidlFull == IntPtr.Zero)
                return;

            Guid iidShellFolder = typeof(IShellFolder).GUID;
            if (SHBindToParent(pidlFull, ref iidShellFolder, out parentFolder, out IntPtr pidlLast) != 0 || parentFolder == null)
                return;

            Guid iidContextMenu = typeof(IContextMenu).GUID;
            IntPtr apidl = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(apidl, pidlLast);
                parentFolder.GetUIObjectOf(ownerHwnd, 1, apidl, ref iidContextMenu, IntPtr.Zero, out IntPtr ppv);
                if (ppv == IntPtr.Zero) return;
                contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu));
                Marshal.Release(ppv);
            }
            finally
            {
                Marshal.FreeHGlobal(apidl);
            }

            const uint cmdFirst = 1;
            const uint cmdLast = 0x7FFF;

            hMenu = CreatePopupMenu();
            int hr = contextMenu.QueryContextMenu(hMenu, 0, cmdFirst, cmdLast, CMF_NORMAL | CMF_EXPLORE);
            if (hr < 0) return;

            int cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN, screenPoint.X, screenPoint.Y, ownerHwnd, IntPtr.Zero);
            if (cmd <= 0) return;

            var ici = new CMINVOKECOMMANDINFO
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                fMask = 0,
                hwnd = ownerHwnd,
                lpVerb = (IntPtr)(cmd - cmdFirst),
                lpParameters = IntPtr.Zero,
                lpDirectory = IntPtr.Zero,
                nShow = SW_SHOWNORMAL
            };
            contextMenu.InvokeCommand(ref ici);
        }
        catch
        {
            // シェル拡張周りは環境依存の失敗があり得るため、失敗時は静かに諦める
        }
        finally
        {
            if (hMenu != IntPtr.Zero) DestroyMenu(hMenu);
            if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
            if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
            if (pidlFull != IntPtr.Zero) CoTaskMemFree(pidlFull);
        }
    }

    /// <summary>ネイティブの「プロパティ」ダイアログを表示する。</summary>
    public static void ShowFileProperties(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        var info = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            lpVerb = "properties",
            lpFile = path,
            nShow = SW_SHOWNORMAL,
            fMask = SEE_MASK_INVOKEIDLIST
        };
        ShellExecuteEx(ref info);
    }

    // --- P/Invoke ---

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out IShellFolder ppv, out IntPtr ppidlLast);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr apidl, [In] ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        [PreserveSig] int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, StringBuilder pszName, uint cchMax);
    }
}
