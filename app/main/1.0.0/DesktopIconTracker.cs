using System.Runtime.InteropServices;
using System.Text;

namespace DesktopAppFolder;

/// <summary>
/// デスクトップのアイコン位置を、名前（拡張子抜きの表示名）指定で取得/設定する。
/// デスクトップアイコンの位置はファイルの属性ではなく、Explorerが内部で持つ
/// SysListView32 の状態として管理されているため、非公開のWin32メッセージを
/// 直接送って読み書きする（公式にサポートされたAPIではない点に注意）。
/// </summary>
internal static class DesktopIconTracker
{
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const int LVM_SETITEMPOSITION = LVM_FIRST + 15;
    private const int LVM_GETITEMPOSITION = LVM_FIRST + 16;
    private const int LVM_GETITEMTEXTW = LVM_FIRST + 115;

    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LVITEMW
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>デスクトップのアイコン一覧を表示している SysListView32 のハンドルを探す。</summary>
    private static IntPtr FindDesktopListView()
    {
        // 通常パターン: Progman -> SHELLDLL_DefView -> SysListView32
        IntPtr progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
                if (listView != IntPtr.Zero) return listView;
            }
        }

        // Windows10以降など、WorkerWの下にSHELLDLL_DefViewがぶら下がることがあるパターン
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            var sb = new StringBuilder(64);
            GetClassName(hWnd, sb, sb.Capacity);
            if (sb.ToString() != "WorkerW") return true;

            IntPtr defView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero) return true;

            IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
            if (listView == IntPtr.Zero) return true;

            found = listView;
            return false; // 見つかったので列挙終了
        }, IntPtr.Zero);

        return found;
    }

    private static int FindItemIndexByName(IntPtr listView, string name)
    {
        GetWindowThreadProcessId(listView, out uint pid);
        IntPtr hProcess = OpenProcess(
            PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION,
            false, pid);
        if (hProcess == IntPtr.Zero) return -1;

        try
        {
            int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            const int textBufSize = 512;

            IntPtr remoteTextBuf = VirtualAllocEx(hProcess, IntPtr.Zero, textBufSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            IntPtr remoteStruct = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)Marshal.SizeOf<LVITEMW>(), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteTextBuf == IntPtr.Zero || remoteStruct == IntPtr.Zero) return -1;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    var item = new LVITEMW
                    {
                        iSubItem = 0,
                        pszText = remoteTextBuf,
                        cchTextMax = textBufSize / 2
                    };

                    byte[] structBytes = StructToBytes(item);
                    WriteProcessMemory(hProcess, remoteStruct, structBytes, (uint)structBytes.Length, out _);

                    SendMessage(listView, LVM_GETITEMTEXTW, (IntPtr)i, remoteStruct);

                    byte[] textBytes = new byte[textBufSize];
                    ReadProcessMemory(hProcess, remoteTextBuf, textBytes, textBufSize, out _);
                    string text = Encoding.Unicode.GetString(textBytes).TrimEnd('\0');

                    // .lnk 単体の名前と、"." を含む区切りで途中終端されている場合の両方を許容
                    int nul = text.IndexOf('\0');
                    if (nul >= 0) text = text.Substring(0, nul);

                    if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            finally
            {
                VirtualFreeEx(hProcess, remoteTextBuf, 0, MEM_RELEASE);
                VirtualFreeEx(hProcess, remoteStruct, 0, MEM_RELEASE);
            }

            return -1;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private static byte[] StructToBytes(LVITEMW item)
    {
        int size = Marshal.SizeOf<LVITEMW>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(item, ptr, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>指定した表示名(拡張子抜き)のデスクトップアイコンの現在位置を取得する。見つからなければnull。</summary>
    public static Point? TryGetPosition(string displayName)
    {
        try
        {
            IntPtr listView = FindDesktopListView();
            if (listView == IntPtr.Zero) return null;

            int index = FindItemIndexByName(listView, displayName);
            if (index < 0) return null;

            GetWindowThreadProcessId(listView, out uint pid);
            IntPtr hProcess = OpenProcess(
                PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION,
                false, pid);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                IntPtr remotePoint = VirtualAllocEx(hProcess, IntPtr.Zero, 8, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remotePoint == IntPtr.Zero) return null;

                try
                {
                    SendMessage(listView, LVM_GETITEMPOSITION, (IntPtr)index, remotePoint);

                    byte[] buf = new byte[8];
                    ReadProcessMemory(hProcess, remotePoint, buf, 8, out _);

                    int x = BitConverter.ToInt32(buf, 0);
                    int y = BitConverter.ToInt32(buf, 4);
                    return new Point(x, y);
                }
                finally
                {
                    VirtualFreeEx(hProcess, remotePoint, 0, MEM_RELEASE);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>指定した表示名(拡張子抜き)のデスクトップアイコンの位置を設定する。</summary>
    public static void TrySetPosition(string displayName, Point pos)
    {
        try
        {
            IntPtr listView = FindDesktopListView();
            if (listView == IntPtr.Zero) return;

            int index = FindItemIndexByName(listView, displayName);
            if (index < 0) return;

            // LVM_SETITEMPOSITION は lParam に直接 x,y を詰めて渡せる（クロスプロセスのメモリ確保は不要）
            IntPtr lParam = (IntPtr)((pos.Y << 16) | (pos.X & 0xFFFF));
            SendMessage(listView, LVM_SETITEMPOSITION, (IntPtr)index, lParam);
        }
        catch
        {
            // ignore
        }
    }
}
