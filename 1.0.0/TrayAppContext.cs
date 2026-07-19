using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopAppFolder;

/// <summary>
/// 目に見えるメインウィンドウを持たず、タスクトレイに常駐して動作する。
/// フォルダーの実体はデスクトップ上の .lnk ショートカットとして存在し、
/// ダブルクリック/ドラッグ&ドロップされると本プロセス(または既存の常駐プロセス)が
/// コマンドを受け取り、該当フォルダーのポップアップを表示する。
/// </summary>
public class TrayAppContext : ApplicationContext
{
    const string AppName = "DeskGG";
    const string Version = "1.0.1"; 

    private readonly Control _uiMarshal;
    private readonly NotifyIcon _trayIcon;
    private readonly Dictionary<Guid, FolderData> _folders = new();
    private readonly Dictionary<Guid, PopupForm> _openPopups = new();
    private readonly SynchronizationContext _uiContext;

    private readonly HashSet<string> _selfWrittenFileNames = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _desktopWatcher;
    private System.Windows.Forms.Timer? _positionPollTimer;

    public TrayAppContext()
    {
        _uiMarshal = new Control();
        _uiMarshal.CreateControl(); // この時点(コンストラクタ呼び出しスレッド=UIスレッド)でハンドルを強制生成

        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "DeskGG"
        };
        _trayIcon.ContextMenuStrip = BuildMenu();

        LoadExistingFolders();

        StartDesktopWatcher();
        StartPositionPolling();
    }

    /// <summary>数秒後に自分で書いたファイル名を「自己起因のイベント」判定から外す。</summary>
    private void MarkSelfWritten(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        _selfWrittenFileNames.Add(fileName);

        var t = new System.Windows.Forms.Timer { Interval = 3000 };
        t.Tick += (_, _) =>
        {
            t.Stop();
            t.Dispose();
            _selfWrittenFileNames.Remove(fileName);
        };
        t.Start();
    }

    /// <summary>デスクトップ上の.lnkのリネームを監視し、ユーザーが手動で改名したら
    /// こちら側のFolderNameにも反映する（＝リネームを正として受け入れる）。</summary>
    private void StartDesktopWatcher()
    {
        try
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            _desktopWatcher = new FileSystemWatcher(desktopDir, "*.lnk")
            {
                NotifyFilter = NotifyFilters.FileName
            };
            _desktopWatcher.Renamed += (_, e) =>
                _uiContext.Post(_ => OnDesktopShortcutRenamed(e.OldName, e.Name), null);
            _desktopWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            LogToFile($"StartDesktopWatcher failed: {ex}");
        }
    }

    private void OnDesktopShortcutRenamed(string? oldName, string? newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;

        // こちらが自分でリネームした結果のイベントは無視する
        if (_selfWrittenFileNames.Remove(newName)) return;

        var data = _folders.Values.FirstOrDefault(f =>
            string.Equals(f.ShortcutFileName, oldName, StringComparison.OrdinalIgnoreCase));
        if (data == null) return;

        string newBaseName = Path.GetFileNameWithoutExtension(newName);
        if (string.IsNullOrWhiteSpace(newBaseName)) return;

        data.FolderName = newBaseName;
        data.ShortcutFileName = newName;
        Storage.Save(data);

        if (_openPopups.TryGetValue(data.Id, out var popup) && !popup.IsDisposed)
        {
            popup.UpdateHeaderText(data.FolderName);
        }

        LogToFile($"Desktop rename detected: {oldName} -> {newName}, saved to json.");
    }

    /// <summary>デスクトップアイコンの位置を定期的に読み取り、変化していればjsonへ保存する。</summary>
    private void StartPositionPolling()
    {
        _positionPollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _positionPollTimer.Tick += (_, _) => PollShortcutPositions();
        _positionPollTimer.Start();
    }

    private void PollShortcutPositions()
    {
        foreach (var data in _folders.Values.ToArray())
        {
            if (string.IsNullOrEmpty(data.ShortcutFileName)) continue;

            string displayName = Path.GetFileNameWithoutExtension(data.ShortcutFileName);
            var pos = DesktopIconTracker.TryGetPosition(displayName);
            if (pos == null) continue;

            if (!data.PositionKnown || data.X != pos.Value.X || data.Y != pos.Value.Y)
            {
                data.X = pos.Value.X;
                data.Y = pos.Value.Y;
                data.PositionKnown = true;
                Storage.Save(data);
            }
        }
    }

    /// <summary>ショートカットファイルを作り直した直後に、保存済みの位置へ戻す。</summary>
    private void RestorePositionDelayed(FolderData data)
    {
        if (!data.PositionKnown) return;

        string displayName = Path.GetFileNameWithoutExtension(data.ShortcutFileName);
        var pos = new Point(data.X, data.Y);

        var t = new System.Windows.Forms.Timer { Interval = 700 };
        t.Tick += (_, _) =>
        {
            t.Stop();
            t.Dispose();
            DesktopIconTracker.TrySetPosition(displayName, pos);
        };
        t.Start();
    }

    /// <summary>タスクトレイのアイコンとして、exe自身に埋め込まれたicon.ico
    /// (ApplicationIconとして設定したもの)を使用する。</summary>
    private static Icon CreateTrayIcon()
    {
        try
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null) return icon;
        }
        catch { /* フォールバックへ */ }

        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var version = menu.Items.Add($"{AppName} v{Version}");
        menu.Items.Add(new ToolStripSeparator());
        version.Enabled = false;

        menu.Items.Add("新規フォルダー作成", null, (_, _) => CreateNewFolder());
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Windows起動時に自動実行")
        {
            CheckOnClick = true,
            Checked = StartupHelper.IsRegistered()
        };
        startupItem.Click += (_, _) => StartupHelper.SetRegistered(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Ko-Fiを開く", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://ko-fi.com/rec877dev") { UseShellExecute = true }); }
            catch { /* ignore */ }
        });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("サポートDiscordサーバーに参加", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://discord.gg/AYWMDp3a8b") { UseShellExecute = true }); }
            catch { /* ignore */ }
        });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("開発者のTwitterを開く", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://twitter.com/bniku87729") { UseShellExecute = true }); }
            catch { /* ignore */ }
        });

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());

        return menu;
    }

    private void LoadExistingFolders()
    {
        var all = Storage.LoadAll();
        var survivors = new List<FolderData>();

        foreach (var data in all)
        {
            // ShortcutFileNameが設定済み(=一度は作成済み)なのに、
            // デスクトップ上に実体が無い場合はユーザーが手動で削除したとみなす
            if (!string.IsNullOrEmpty(data.ShortcutFileName))
            {
                string expectedPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    data.ShortcutFileName);

                if (!File.Exists(expectedPath))
                {
                    Storage.DeleteFolder(data);
                    continue; // 復活させない
                }
            }

            survivors.Add(data);
        }

        if (survivors.Count == 0)
        {
            survivors.Add(Storage.CreateNew("DeskGG", new Point(100, 100)));
        }

        foreach (var data in survivors)
        {
            _folders[data.Id] = data;
            RegenerateIconAndShortcut(data);
        }
    }

    private void CreateNewFolder()
    {
        var data = Storage.CreateNew($"新しいフォルダー {_folders.Count + 1}", new Point(100, 100));
        _folders[data.Id] = data;
        RegenerateIconAndShortcut(data);

        MessageBox.Show(
            $"デスクトップに「{data.FolderName}」を作成しました。",
            "DeskGG", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>他プロセス(Named Pipe経由)からのコマンドをUIスレッドにマーシャリングして処理する。</summary>
    public void PostCommand(string[] args)
    {
        if (_uiMarshal.IsHandleCreated && !_uiMarshal.IsDisposed)
        {
            _uiMarshal.BeginInvoke(new Action(() => HandleCommand(args)));
        }
    }


    /// <summary>
    /// "--show-folder {guid} [ドロップされたファイルパス...]" を処理する。
    /// 自プロセスが起動直後にこのコマンドを引数として受け取った場合もここを通る。
    /// </summary>
    public void HandleCommand(string[] args)
    {
        LogToFile($"HandleCommand: args={string.Join(",", args)}");
        if (args.Length < 2 || args[0] != "--show-folder")
        {
            LogToFile("HandleCommand: rejected (bad args)");
            return;
        }
        if (!Guid.TryParse(args[1], out var id))
        {
            LogToFile("HandleCommand: guid parse failed");
            return;
        }
        if (!_folders.TryGetValue(id, out var data))
        {
            LogToFile($"HandleCommand: folder not found for id={id}");
            return;
        }
        LogToFile($"HandleCommand: folder found, showing popup for {data.FolderName}");
        //if (args.Length < 2 || args[0] != "--show-folder") return;
        //if (!Guid.TryParse(args[1], out var id)) return;
        //if (!_folders.TryGetValue(id, out var data)) return;

        var droppedFiles = args.Skip(2).Where(File.Exists).ToArray();

        if (droppedFiles.Length > 0)
        {
            AddApps(data, droppedFiles);
        }

        ShowPopupNearCursor(data);
    }

    private void AddApps(FolderData data, string[] files)
    {
        foreach (var file in files)
        {
            if (data.IsFull)
            {
                MessageBox.Show("フォルダーは最大9個までです。", "DeskGG",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            }
            string name = Path.GetFileNameWithoutExtension(file);
            string iconFile = Storage.ExtractAndSaveIcon(data.Id, file);
            string resolvedPath = ResolveTargetIfShortcut(data.Id, file); // ← data.Id を渡す
            data.Apps.Add(new AppItem { Name = name, Path = resolvedPath, IconFile = iconFile });
        }

        Storage.Save(data);
        RegenerateIconAndShortcut(data);

        if (_openPopups.TryGetValue(data.Id, out var popup) && !popup.IsDisposed)
        {
            popup.RefreshGrid();
        }
    }
    private string ResolveTargetIfShortcut(Guid folderId, string path)
    {
        string ext = Path.GetExtension(path);

        if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string? resolved = ShortcutManager.ResolveShortcutTarget(path);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                return resolved;

            // 実体が解決できない場合は.lnk自体をコピーして保持する
            try { return Storage.SaveShortcutCopy(folderId, path); }
            catch { return path; }
        }

        if (string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase))
        {
            // .url (Steamのゲームショートカット等)は実体パスを解決できないため、
            // ファイル自体をフォルダー保存領域にコピーして永続化する。
            // これをしないとデスクトップの元ファイルを消した時点で起動できなくなる。
            try { return Storage.SaveShortcutCopy(folderId, path); }
            catch { return path; }
        }

        return path;
    }

    private void ShowPopupNearCursor(FolderData data)
    {
        if (_openPopups.TryGetValue(data.Id, out var existing) && !existing.IsDisposed)
        {
            existing.RefreshGrid();
            existing.ShowNear(new Rectangle(Cursor.Position, new Size(1, 1)));
            return;
        }

        var popup = new PopupForm(data, () => OnFolderDataChanged(data), OnFolderDeleteRequested);
        popup.FormClosed += (_, _) => _openPopups.Remove(data.Id);
        _openPopups[data.Id] = popup;
        popup.ShowNear(new Rectangle(Cursor.Position, new Size(1, 1)));
    }

    private void OnFolderDataChanged(FolderData data)
    {
        Storage.Save(data);
        RegenerateIconAndShortcut(data);
    }

    private void OnFolderDeleteRequested(FolderData data)
    {
        if (_openPopups.TryGetValue(data.Id, out var popup) && !popup.IsDisposed)
        {
            popup.Close();
            _openPopups.Remove(data.Id);
        }

        ShortcutManager.DeleteShortcut(data);
        Storage.DeleteFolder(data);
        _folders.Remove(data.Id);
        RefreshDesktopIcons();
    }

    private void RegenerateIconAndShortcut(FolderData data)
    {
        string previousFileName = data.ShortcutFileName;

        string icoPath = IconGenerator.GenerateIconFile(data);
        ShortcutManager.CreateOrUpdateShortcut(data, icoPath);
        Storage.Save(data); // ShortcutFileName の更新分を保存
        RefreshDesktopIcons();

        bool fileNameChanged = !string.Equals(previousFileName, data.ShortcutFileName, StringComparison.OrdinalIgnoreCase);
        if (fileNameChanged)
        {
            // ファイルを作り直した = Renamedイベントが飛んでくるので、
            // それをOnDesktopShortcutRenamedで誤って「ユーザーによる手動リネーム」と
            // 判定しないよう自己起因としてマークしておく
            MarkSelfWritten(data.ShortcutFileName);
            RestorePositionDelayed(data);
        }
    }

    /// <summary>Explorerのアイコンキャッシュをクリアして見た目を反映させる。</summary>
    private void RefreshDesktopIcons()
    {
        NativeMethods.SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    // TrayAppContext.cs 内に追加
    private static void LogToFile(string msg)
    {
        try
        {
            Directory.CreateDirectory(Storage.RootDir);
            string path = Path.Combine(Storage.RootDir, "DesktopAppFolder_crash.log");
            File.AppendAllText(path, $"[{DateTime.Now}] {msg}\n");
        }
        catch { }
    }

    private void ExitApp()
    {
        foreach (var popup in _openPopups.Values.ToArray())
        {
            popup.Close();
        }
        _openPopups.Clear();

        _desktopWatcher?.Dispose();
        _positionPollTimer?.Stop();
        _positionPollTimer?.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        ExitThread();
    }
}

internal static class NativeMethods
{
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_MENU = 0x12; // Alt
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// バックグラウンドスレッドから表示したウィンドウはWindowsの
    /// 「フォアグラウンド奪取防止」に阻まれてSetForegroundWindowが失敗することがある。
    /// Altキーの疑似押下→解放を挟むことで制限を回避する、よく知られた手法。
    /// </summary>
    public static void ForceSetForegroundWindow(IntPtr hWnd)
    {
        try
        {
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            SetForegroundWindow(hWnd);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { /* ignore */ }
    }
}