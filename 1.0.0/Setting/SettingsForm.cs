using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopAppFolder;

public class SettingsForm : Form
{
    // Windows 11 風カラーパレット
    private static readonly Color BgColor = Color.FromArgb(243, 243, 243);
    private static readonly Color CardColor = Color.White;
    private static readonly Color CardHoverColor = Color.FromArgb(245, 245, 245);
    private static readonly Color CardBorderColor = Color.FromArgb(229, 229, 229);
    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
    private static readonly Color AccentHoverColor = Color.FromArgb(23, 103, 171);
    private static readonly Color TextColor = Color.FromArgb(32, 32, 32);
    private static readonly Color SubTextColor = Color.FromArgb(96, 96, 96);

    private readonly FlowLayoutPanel _flow;
    private readonly Panel _headerPanel;
    private readonly Button _addButton;

    private List<FolderData> _folders = new();

    public SettingsForm()
    {
        Icon = AppIcon.Shared;
        Text = "DeskGG 設定";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(880, 480);
        MinimumSize = new Size(120, 140);

        // 最大化は無効化(サイズ変更・最小化は可能なままにする)
        //FormBorderStyle = FormBorderStyle.Sizable;
        //MaximizeBox = false;
        //MinimizeBox = true;

        BackColor = BgColor;
        Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        // ── ヘッダー ──────────────────────────────
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = BgColor
        };

        var titleLabel = new Label
        {
            Text = "デスクトップフォルダー",
            AutoSize = true,
            Location = new Point(20, 16),
            Font = new Font("Yu Gothic UI", 13f, FontStyle.Bold),
            ForeColor = TextColor,
            BackColor = Color.Transparent
        };

        _addButton = CreateAccentButton("+ 追加", new Point(0, 12), new Size(96, 32));
        _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _addButton.Click += (_, _) => AddFolder();

        _headerPanel.Controls.Add(titleLabel);
        _headerPanel.Controls.Add(_addButton);
        _headerPanel.Resize += (_, _) =>
        {
            _addButton.Location = new Point(_headerPanel.ClientSize.Width - _addButton.Width - 20, 12);
        };

        // ── タイル一覧 ──────────────────────────────
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = BgColor,
            Padding = new Padding(16, 8, 16, 16),
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        Controls.Add(_flow);
        Controls.Add(_headerPanel);

        RefreshList();
    }

    // ── Win11風アクセントボタン生成 ──────────────────────────────
    private Button CreateAccentButton(string text, Point location, Size size)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentColor,
            ForeColor = Color.White,
            Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        btn.FlatAppearance.MouseDownBackColor = AccentHoverColor;
        btn.Region = RoundedRegion(btn.Width, btn.Height, 6);
        return btn;
    }

    private static Region RoundedRegion(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d, 0, d, d, 270, 90);
        path.AddArc(width - d, height - d, d, d, 0, 90);
        path.AddArc(0, height - d, d, d, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    /// <summary>
    /// IconGeneratorが生成した実際の.icoファイルを読み込み、タイル表示用の画像として返す。
    /// 何らかの理由で読み込めない場合はnullを返し、タイル側で色付きフォルダー描画にフォールバックする。
    /// </summary>
    private static Image? LoadTileIcon(FolderData data)
    {
        try
        {
            string icoPath = IconGenerator.GenerateIconFile(data);
            if (!File.Exists(icoPath)) return null;

            using var icon = new Icon(icoPath, 48, 48);
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    private void RefreshList()
    {
        _folders = Storage.LoadAll().OrderBy(f => f.FolderName, StringComparer.OrdinalIgnoreCase).ToList();

        _flow.SuspendLayout();
        foreach (Control c in _flow.Controls.OfType<FolderTile>().ToList())
        {
            c.Dispose();
        }
        _flow.Controls.Clear();

        foreach (var data in _folders)
        {
            Image? icon = LoadTileIcon(data);
            var tile = new FolderTile(data, icon, CardColor, CardHoverColor, CardBorderColor, TextColor, SubTextColor);
            tile.EditRequested += (_, _) => EditFolder(data);
            tile.DeleteRequested += (_, _) => DeleteFolder(data);
            _flow.Controls.Add(tile);
        }
        _flow.ResumeLayout();
    }

    private void AddFolder()
    {
        using var dlg = new FolderEditDialog("フォルダーの追加", $"新しいフォルダー {_folders.Count + 1}", 0);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var data = Storage.CreateNew(dlg.FolderNameInput, new Point(100, 100));
        if (!dlg.UseDefaultColor)
        {
            data.ThemeColor = dlg.ThemeColor.ToArgb();
        }
        Storage.Save(data);

        RegenerateIconAndShortcut(data);
        NotifyAndRefresh();
    }

    private void EditFolder(FolderData data)
    {
        using var dlg = new FolderEditDialog("フォルダーの編集", data.FolderName, data.ThemeColor);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        data.FolderName = dlg.FolderNameInput;
        data.ThemeColor = dlg.UseDefaultColor ? 0 : dlg.ThemeColor.ToArgb();
        Storage.Save(data);

        RegenerateIconAndShortcut(data);
        NotifyAndRefresh();
    }

    private void DeleteFolder(FolderData data)
    {
        var result = MessageBox.Show(this,
            $"「{data.FolderName}」を削除しますか?\n(フォルダー内のアプリ自体は削除されません)",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        ShortcutManager.DeleteShortcut(data);
        Storage.DeleteFolder(data);

        RefreshDesktopIcons();
        NotifyAndRefresh();
    }

    /// <summary>アイコン画像とデスクトップショートカット(.lnk)を最新の名前/色で作り直す。</summary>
    private static void RegenerateIconAndShortcut(FolderData data)
    {
        string icoPath = IconGenerator.GenerateIconFile(data);
        ShortcutManager.CreateOrUpdateShortcut(data, icoPath);
        Storage.Save(data); // ShortcutFileNameの更新分を保存
        RefreshDesktopIcons();
    }

    /// <summary>常駐中のDeskGG本体へ変更を通知し、一覧を再読み込みする。</summary>
    private void NotifyAndRefresh()
    {
        PipeNotifier.NotifyReload();
        RefreshList();
    }

    private static void RefreshDesktopIcons()
    {
        SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

/// <summary>
/// Windows 11のスタートメニュー風に、フォルダー1件をカード(タイル)として表示するコントロール。
/// クリックで選択状態を切り替え、ダブルクリックで編集、右クリックで編集/削除メニューを表示する。
/// </summary>
internal sealed class FolderTile : Panel
{
    private const int TileWidth = 120;
    private const int TileHeight = 120;
    private const int IconSize = 48;
    private const int CornerRadius = 8;

    private readonly FolderData _data;
    private readonly Image? _icon;
    private readonly Color _baseColor;
    private readonly Color _hoverColor;
    private readonly Color _borderColor;
    private readonly Color _textColor;
    private readonly Color _subTextColor;
    private bool _hovering;

    public event EventHandler? EditRequested;
    public event EventHandler? DeleteRequested;

    public FolderTile(FolderData data, Image? icon, Color baseColor, Color hoverColor, Color borderColor,
        Color textColor, Color subTextColor)
    {
        _data = data;
        _icon = icon;
        _baseColor = baseColor;
        _hoverColor = hoverColor;
        _borderColor = borderColor;
        _textColor = textColor;
        _subTextColor = subTextColor;

        Size = new Size(TileWidth, TileHeight);
        Margin = new Padding(8);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        var menu = new ContextMenuStrip();
        menu.Items.Add("編集", null, (_, _) => EditRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("削除", null, (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty));
        ContextMenuStrip = menu;

        MouseEnter += (_, _) => { _hovering = true; Invalidate(); };
        MouseLeave += (_, _) => { _hovering = false; Invalidate(); };
        MouseDoubleClick += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPath(rect, CornerRadius);

        using (var brush = new SolidBrush(_hovering ? _hoverColor : _baseColor))
        {
            g.FillPath(brush, path);
        }
        using (var pen = new Pen(_borderColor, 1f))
        {
            g.DrawPath(pen, path);
        }

        var iconRect = new Rectangle((Width - IconSize) / 2, 14, IconSize, IconSize);

        if (_icon != null)
        {
            // IconGeneratorが生成した実際の.icoを表示
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_icon, iconRect);
        }
        else
        {
            // アイコンが読み込めない場合は、テーマカラーを反映した色付きフォルダーで代用
            Color iconColor = _data.ThemeColor != 0 ? Color.FromArgb(_data.ThemeColor) : Color.FromArgb(32, 32, 36);
            using (var iconPath = RoundedPath(iconRect, 10))
            using (var iconBrush = new SolidBrush(iconColor))
            {
                g.FillPath(iconBrush, iconPath);
            }
            // フォルダーらしい「タブ」部分
            var tabRect = new Rectangle(iconRect.Left + 4, iconRect.Top - 6, IconSize / 2, 10);
            using (var tabPath = RoundedPath(tabRect, 4))
            using (var tabBrush = new SolidBrush(iconColor))
            {
                g.FillPath(tabBrush, tabPath);
            }
        }

        // フォルダー名(2行まで、省略表示)
        var textRect = new Rectangle(6, IconSize + 22, Width - 12, Height - (IconSize + 22) - 6);
        TextRenderer.DrawText(g, _data.FolderName, Font, textRect, _textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
            TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}