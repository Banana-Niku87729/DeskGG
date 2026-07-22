using System.Drawing.Drawing2D;

namespace DesktopAppFolder;

public class PopupForm : Form
{
    private const int Cols = 3;
    private const int Rows = 3;
    private const int CellSize = 90;
    private const int Padding = 16;
    private const int HeaderHeight = 34;
    private const int CornerRadius = 16;

    // 内部ドラッグ(並び替え/外出し)識別用のクリップボード形式名
    private const string InternalDragFormat = "DesktopAppFolder.InternalAppDrag";

    private static readonly string DesktopDir =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private readonly FolderData _data;
    private readonly Action _onChanged;
    private readonly Action<FolderData> _onDeleteRequested;
    private readonly FlowLayoutPanel _grid;
    private readonly Label _headerLabel;

    private bool _suppressAutoHide;

    public PopupForm(FolderData data, Action onChanged, Action<FolderData> onDeleteRequested)
    {
        _data = data;
        _onChanged = onChanged;
        _onDeleteRequested = onDeleteRequested;

        Icon = AppIcon.Shared;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = _data.ThemeColor != 0 ? Color.FromArgb(_data.ThemeColor) : Color.FromArgb(32, 32, 36);
        Opacity = 0.88;
        DoubleBuffered = true;
        AllowDrop = true;

        int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
        int width = Padding * 2 + Cols * CellSize + scrollBarWidth;
        int height = HeaderHeight + Padding * 2 + Rows * CellSize;
        Size = new Size(width, height);

        _headerLabel = new Label
        {
            Text = _data.FolderName,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Top,
            Height = HeaderHeight,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = Color.Transparent
        };
        _headerLabel.ContextMenuStrip = BuildHeaderContextMenu();

        var closeBtn = new Label
        {
            Text = "✕",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9f),
            AutoSize = false,
            Size = new Size(HeaderHeight, HeaderHeight),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        closeBtn.Click += (_, _) => Hide();
        closeBtn.MouseEnter += (_, _) => closeBtn.ForeColor = Color.White;
        closeBtn.MouseLeave += (_, _) => closeBtn.ForeColor = Color.Gainsboro;
        _headerLabel.Controls.Add(closeBtn);

        _grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Padding),
            BackColor = Color.Transparent,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = true,
            AllowDrop = true
        };
        _grid.DragEnter += OnGridDragEnter;
        _grid.DragDrop += OnGridDragDrop;

        Controls.Add(_grid);
        Controls.Add(_headerLabel);

        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius));
        };
        Resize += (_, _) => ApplyRoundedRegion();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        Deactivate += (_, _) =>
        {
            if (!_suppressAutoHide) Hide();
        };

        RefreshGrid();
    }

    private void ApplyRoundedRegion()
    {
        Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius));
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void ShowNear(Rectangle anchorBounds)
    {
        var screen = Screen.FromRectangle(anchorBounds).WorkingArea;

        int x = anchorBounds.Right + 8;
        int y = anchorBounds.Top;

        if (x + Width > screen.Right) x = anchorBounds.Left - Width - 8;
        if (x < screen.Left) x = screen.Left + 8;
        if (y + Height > screen.Bottom) y = screen.Bottom - Height - 8;
        if (y < screen.Top) y = screen.Top + 8;

        Location = new Point(x, y);
        ApplyRoundedRegion();
        Show();
        Activate();
        NativeMethods.ForceSetForegroundWindow(Handle);
    }

    public void RefreshGrid()
    {
        _grid.SuspendLayout();
        _grid.Controls.Clear();

        foreach (var app in _data.Apps)
        {
            _grid.Controls.Add(BuildAppCell(app));
        }

        int emptyToShow = Math.Max(0, 9 - _data.Apps.Count);
        for (int i = 0; i < emptyToShow; i++)
        {
            _grid.Controls.Add(BuildEmptyCell());
        }

        _grid.ResumeLayout();
    }

    private Control BuildAppCell(AppItem app)
    {
        var panel = new Panel
        {
            Size = new Size(CellSize, CellSize),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            AllowDrop = true,
            Tag = app
        };

        var picture = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point((CellSize - 40) / 2, 6),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try { picture.Image = Storage.LoadIcon(_data.Id, app.IconFile); }
        catch { picture.Image = SystemIcons.Application.ToBitmap(); }

        var label = new Label
        {
            Text = app.Name,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 7.5f),
            TextAlign = ContentAlignment.TopCenter,
            AutoEllipsis = true,
            Size = new Size(CellSize - 6, 32),
            Location = new Point(3, 50),
            BackColor = Color.Transparent
        };

        panel.Controls.Add(picture);
        panel.Controls.Add(label);

        // --- ドラッグ&クリック処理 ---
        Point dragStart = Point.Empty;
        bool dragging = false;
        bool dragStarted = false;

        void OnMouseDown(object? s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragStart = e.Location;
                dragging = false;
            }
        }

        void OnMouseMove(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragStart == Point.Empty || dragging) return;

            int dx = Math.Abs(e.Location.X - dragStart.X);
            int dy = Math.Abs(e.Location.Y - dragStart.Y);
            if (dx < SystemInformation.DragSize.Width && dy < SystemInformation.DragSize.Height) return;

            dragging = true;
            dragStarted = true;
            _lastDropWasInternalReorder = false; // ★ドラッグ開始時に必ずリセット

            string sourcePath = app.HiddenSourcePath ?? app.Path;
            string? dragPath = PrepareDragFile(sourcePath, app.Name);

            var data = new DataObject();
            data.SetData(InternalDragFormat, app.Id.ToString());
            if (dragPath != null && File.Exists(dragPath))
            {
                data.SetData(DataFormats.FileDrop, new[] { dragPath });
            }

            // --- ドラッグ中の見た目(シャドウ風) ---
            picture.Region = null;
            var originalPictureLoc = picture.Location;
            var shadowPanel = new Panel
            {
                Size = panel.Size,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(60, 255, 255, 255)
            };
            panel.Controls.Add(shadowPanel);
            shadowPanel.BringToFront();
            picture.SendToBack();

            var result = DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);

            panel.Controls.Remove(shadowPanel);
            shadowPanel.Dispose();

            // ★条件を緩和: 内部での並び替えでなければ、外に出たとみなす
            if (!_lastDropWasInternalReorder && result != DragDropEffects.None)
            {
                RestoreAndRemove(app);
            }

            _lastDropWasInternalReorder = false;
            dragging = false;
            dragStart = Point.Empty;
        }

        void OnMouseUp(object? s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !dragStarted)
            {
                LaunchApp(app);
            }
            dragStart = Point.Empty;
            dragging = false;
            dragStarted = false;
        }

        panel.MouseDown += OnMouseDown;
        panel.MouseMove += OnMouseMove;
        panel.MouseUp += OnMouseUp;
        picture.MouseDown += OnMouseDown;
        picture.MouseMove += OnMouseMove;
        picture.MouseUp += OnMouseUp;
        label.MouseDown += OnMouseDown;
        label.MouseMove += OnMouseMove;
        label.MouseUp += OnMouseUp;

        // セル自体をドロップ先にして、他のアプリがここに来たら並び替える
        panel.DragEnter += (s, e) =>
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(InternalDragFormat)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        };
        panel.DragDrop += (s, e) => HandleInternalReorder(e, app);

        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.T("popup.remove_from_folder"), null, (_, _) => RemoveApp(app));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("popup.run_as_admin"), null, (_, _) => RunAsAdmin(app));
        menu.Items.Add(Loc.T("popup.open_file_location"), null, (_, _) => OpenFileLocation(app));
        menu.Items.Add(Loc.T("popup.properties"), null, (_, _) => ShowProperties(app));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("popup.show_more_options"), null, (_, _) => ShowNativeContextMenu(app));
        panel.ContextMenuStrip = menu;
        picture.ContextMenuStrip = menu;
        label.ContextMenuStrip = menu;

        return panel;
    }

    // グリッドの並び替え中かどうかを一時的に示すフラグ(同一ドラッグ操作内で共有)
    private bool _lastDropWasInternalReorder;

    private void HandleInternalReorder(DragEventArgs e, AppItem targetApp)
    {
        if (e.Data == null || !e.Data.GetDataPresent(InternalDragFormat)) return;
        string? idText = e.Data.GetData(InternalDragFormat) as string;
        if (idText == null || !Guid.TryParse(idText, out Guid sourceId)) return;
        if (sourceId == targetApp.Id) return;

        var sourceApp = _data.Apps.FirstOrDefault(a => a.Id == sourceId);
        if (sourceApp == null) return;

        int sourceIndex = _data.Apps.IndexOf(sourceApp);
        int targetIndex = _data.Apps.IndexOf(targetApp);
        if (sourceIndex < 0 || targetIndex < 0) return;

        _data.Apps.RemoveAt(sourceIndex);
        _data.Apps.Insert(targetIndex, sourceApp);

        _lastDropWasInternalReorder = true;
        e.Effect = DragDropEffects.Move;

        RefreshGrid();
        _onChanged();
    }

    private void OnGridDragEnter(object? sender, DragEventArgs e)
    {
        // 内部ドラッグが空きスペースに来た場合は末尾に追加する形で受理
        e.Effect = e.Data != null && e.Data.GetDataPresent(InternalDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnGridDragDrop(object? sender, DragEventArgs e)
    {
        // 空きスペースにドロップされた内部ドラッグは「並び替えなし」として扱う
        // (すでにフォルダー内なので何もしない。誤ってFileDrop側のOnDragDropで追加されないよう
        //  ここでInternalDragFormatを検出したことでイベントを消費する)
        if (e.Data != null && e.Data.GetDataPresent(InternalDragFormat))
        {
            _lastDropWasInternalReorder = true;
            e.Effect = DragDropEffects.Move;
        }
    }

    private void RestoreAndRemove(AppItem app)
    {
        if (app.HiddenSourcePath != null && File.Exists(app.HiddenSourcePath))
        {
            try
            {
                File.SetAttributes(app.HiddenSourcePath,
                    File.GetAttributes(app.HiddenSourcePath) & ~FileAttributes.Hidden);
            }
            catch { /* ignore */ }
        }

        Storage.DeleteIcon(_data.Id, app.IconFile);
        _data.Apps.Remove(app);
        RefreshGrid();
        _onChanged();
    }

    private Control BuildEmptyCell()
    {
        var panel = new Panel
        {
            Size = new Size(CellSize, CellSize),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            AllowDrop = true
        };
        panel.DragEnter += OnGridDragEnter;
        panel.DragDrop += OnGridDragDrop;
        return panel;
    }

    private void LaunchApp(AppItem app)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = app.Path,
                WorkingDirectory = Path.GetDirectoryName(app.Path) ?? "",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("popup.launch_failed", ex.Message), Loc.T("appname"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveApp(AppItem app)
    {
        RestoreAndRemove(app);
    }

    private void RunAsAdmin(AppItem app)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = app.Path,
                WorkingDirectory = Path.GetDirectoryName(app.Path) ?? "",
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(psi);
            Hide();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UACダイアログでユーザーがキャンセルした場合など。何もしない。
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("popup.admin_launch_failed", ex.Message), Loc.T("appname"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenFileLocation(AppItem app)
    {
        try
        {
            if (!File.Exists(app.Path))
            {
                MessageBox.Show(Loc.T("popup.file_not_found"), Loc.T("appname"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{app.Path}\"",
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }

    private void ShowProperties(AppItem app)
    {
        try { ShellContextMenu.ShowFileProperties(app.Path); }
        catch { /* ignore */ }
    }

    private void ShowNativeContextMenu(AppItem app)
    {
        try { ShellContextMenu.Show(Handle, app.Path, Cursor.Position); }
        catch { /* ignore */ }
    }

    /// <summary>
    /// フォルダー内部の保存領域にあるショートカットコピー(GUIDファイル名)をそのままデスクトップへ
    /// ドラッグすると、置いた先の名前もGUIDのままになってしまう。これを避けるため、ドラッグ時には
    /// 表示名(app.Name)を付けた一時コピーを作り、そちらをドラッグデータに使う。
    /// 元々デスクトップにあった実ファイル(隠しファイル化して復元する対象)は、
    /// 既に正しい名前になっているのでそのまま使う。
    /// </summary>
    private static string? PrepareDragFile(string sourcePath, string displayName)
    {
        if (!File.Exists(sourcePath)) return null;

        string currentName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.Equals(currentName, displayName, StringComparison.Ordinal))
            return sourcePath;

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DesktopAppFolder_Drag");
            Directory.CreateDirectory(tempDir);

            string ext = Path.GetExtension(sourcePath);
            string safeName = SanitizeFileName(displayName);
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "App";
            string tempPath = Path.Combine(tempDir, safeName + ext);

            File.Copy(sourcePath, tempPath, overwrite: true);
            return tempPath;
        }
        catch
        {
            return sourcePath; // 失敗時は従来通りの動作にフォールバック
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    public void UpdateHeaderText(string name)
    {
        _headerLabel.Text = name;
    }

    private ContextMenuStrip BuildHeaderContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(Loc.T("popup.rename_folder"), null, (_, _) => RenameFolder());
        menu.Items.Add(Loc.T("popup.customize_folder"), null, (_, _) => CustomizeColor());

        menu.Items.Add(Loc.T("popup.remove_all_contents"), null, (_, _) =>
        {
            _suppressAutoHide = true;
            try
            {
                if (MessageBox.Show(this, Loc.T("popup.remove_all_confirm"), Loc.T("confirm_title"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (var app in _data.Apps.ToList()) RestoreAndRemove(app);
                }
            }
            finally { _suppressAutoHide = false; }
        });

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(Loc.T("popup.delete_this_folder"), null, (_, _) =>
        {
            _suppressAutoHide = true;
            try
            {
                if (MessageBox.Show(this, Loc.F("popup.delete_folder_confirm", _data.FolderName),
                        Loc.T("confirm_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _onDeleteRequested(_data);
                    Hide();
                }
            }
            finally { _suppressAutoHide = false; }
        });

        return menu;
    }

    private void CustomizeColor()
    {
        _suppressAutoHide = true;
        try
        {
            using var dlg = new ColorDialog
            {
                Color = BackColor,
                FullOpen = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                BackColor = dlg.Color;
                _data.ThemeColor = dlg.Color.ToArgb();
                Invalidate();

                try
                {
                    // デスクトップショートカットのアイコンを新しい色で再生成
                    string icoPath = IconGenerator.GenerateIconFile(_data);
                    ShortcutManager.CreateOrUpdateShortcut(_data, icoPath);

                    // グリッドサムネイルも保存(new_grid_thumbnail.png的な用途)
                    using var thumb = new Bitmap(160, 160);
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.Clear(_data.ThemeColor != 0 ? Color.FromArgb(_data.ThemeColor) : Color.FromArgb(32, 32, 36));
                    }
                    Storage.SaveGridThumbnail(_data.Id, thumb);
                }
                catch { /* アイコン再生成に失敗しても色設定自体は継続 */ }

                _onChanged();
            }
        }
        finally { _suppressAutoHide = false; }
    }

    private void RenameFolder()
    {
        _suppressAutoHide = true;
        try
        {
            using var dlg = new NameInputDialog(_data.FolderName);
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.InputText))
            {
                _data.FolderName = dlg.InputText.Trim();
                UpdateHeaderText(_data.FolderName);
                _onChanged();
            }
        }
        finally
        {
            _suppressAutoHide = false;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(InternalDragFormat))
        {
            // 内部ドラッグはグリッド側のDragEnterに任せる(ここではファイル追加とみなさない)
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) && !_data.IsFull
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        // 内部ドラッグ(フォルダー内の並び替え/外出し操作)はここでは処理しない。
        // これを弾かないと、フォルダー内で移動しただけなのに
        // 「デスクトップからの新規ファイル追加」として二重登録されてしまう。
        if (e.Data != null && e.Data.GetDataPresent(InternalDragFormat)) return;

        if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

        foreach (var file in files)
        {
            if (_data.IsFull) break;
            string name = Path.GetFileNameWithoutExtension(file);
            string iconFile = Storage.ExtractAndSaveIcon(_data.Id, file);
            string resolvedPath = ResolveTargetIfShortcut(file);

            string? hiddenSource = null;
            if (string.Equals(Path.GetDirectoryName(file), DesktopDir, StringComparison.OrdinalIgnoreCase)
                && File.Exists(file))
            {
                try
                {
                    File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
                    hiddenSource = file;
                }
                catch { /* ignore */ }
            }

            _data.Apps.Add(new AppItem
            {
                Name = name,
                Path = resolvedPath,
                IconFile = iconFile,
                HiddenSourcePath = hiddenSource
            });
        }

        RefreshGrid();
        _onChanged();
    }

    private string ResolveTargetIfShortcut(string path)
    {
        string ext = Path.GetExtension(path);

        if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string? resolved = ShortcutManager.ResolveShortcutTarget(path);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                return resolved;

            try { return Storage.SaveShortcutCopy(_data.Id, path); }
            catch { return path; }
        }

        if (string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase))
        {
            // .url は Steamのゲームショートカット(steam://rungameid/...)などで使われる形式で、
            // .lnkと違い「実体パス」を解決できない(URLを起動するだけの中身のため)。
            // そのため .url ファイル自体をフォルダーの保存領域にコピーし、そちらを起動対象にする。
            // これをしないと、デスクトップ上の元の.urlファイルが消えた/移動した時点で
            // 「指定されたファイルが見つかりません」エラーになってしまう。
            try { return Storage.SaveShortcutCopy(_data.Id, path); }
            catch { return path; }
        }

        return path;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Hide();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }
}