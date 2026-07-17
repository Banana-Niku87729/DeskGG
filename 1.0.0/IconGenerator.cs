using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DesktopAppFolder;

/// <summary>
/// フォルダーの中身から「角丸四角＋3x3ミニプレビュー」の画像を作り、
/// デスクトップショートカットのアイコンとして使える .ico ファイルに保存する。
/// </summary>
internal static class IconGenerator
{
    private const int Size = 256; // .ico は大きめに作っておくと大アイコン表示でも綺麗
    private const int CornerRadius = 48;

    public static string GenerateIconFile(FolderData data)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var rect = new Rectangle(4, 4, Size - 8, Size - 8);
            using var path = RoundedRect(rect, CornerRadius);

            // フォルダーのテーマカラーがあればそれを使う。なければ既定色。
            Color bgColor = data.ThemeColor != 0
                ? Color.FromArgb(235, Color.FromArgb(data.ThemeColor))
                : Color.FromArgb(235, 45, 45, 50);

            using (var bg = new SolidBrush(bgColor))
                g.FillPath(bg, path);

            var oldClip = g.Clip;
            g.SetClip(path, CombineMode.Intersect);
            DrawMiniGrid(g, data, rect);
            g.Clip = oldClip;

            using var edgePen = new Pen(Color.FromArgb(160, 255, 255, 255), 3f);
            g.DrawPath(edgePen, path);
        }

        string dir = Storage.IconOutputDir(data.Id);
        Directory.CreateDirectory(dir);
        string icoPath = Path.Combine(dir, "folder_icon.ico");

        SaveAsIcon(bmp, icoPath);
        return icoPath;
    }

    private static void DrawMiniGrid(Graphics g, FolderData data, Rectangle area)
    {
        const int PreviewSlots = 9; // アイコン上のミニプレビューは常に3x3固定
        int cols = 3, rows = 3;
        int cellW = area.Width / cols;
        int cellH = area.Height / rows;
        int pad = area.Width / 40;

        for (int i = 0; i < PreviewSlots; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var cellRect = new Rectangle(
                area.X + col * cellW + pad,
                area.Y + row * cellH + pad,
                cellW - pad * 2,
                cellH - pad * 2);

            if (i < data.Apps.Count)
            {
                try
                {
                    using var img = Storage.LoadIcon(data.Id, data.Apps[i].IconFile);
                    g.DrawImage(img, cellRect);
                }
                catch
                {
                    using var b = new SolidBrush(Color.FromArgb(180, Color.Gray));
                    g.FillRectangle(b, cellRect);
                }
            }
        }
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

    /// <summary>32bppArgbのBitmapを、透過を保ったまま単一サイズの.icoとして保存する。</summary>
    // IconGenerator.cs 内の SaveAsIcon を置き換え

    /// <summary>32bppArgbのBitmapを、PNG形式のままICOファイルに埋め込んで保存する。
    /// GetHicon()はアルファチャンネルを正しく扱えないため使用しない。</summary>
    private static void SaveAsIcon(Bitmap bmp, string path)
    {
        using var pngStream = new MemoryStream();
        bmp.Save(pngStream, ImageFormat.Png);
        byte[] pngBytes = pngStream.ToArray();

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // ICONDIR
        writer.Write((short)0); // reserved
        writer.Write((short)1); // type = icon
        writer.Write((short)1); // image count

        // ICONDIRENTRY
        writer.Write((byte)0);  // width (0 = 256px)
        writer.Write((byte)0);  // height (0 = 256px)
        writer.Write((byte)0);  // color count
        writer.Write((byte)0);  // reserved
        writer.Write((short)1); // planes
        writer.Write((short)32);// bit count
        writer.Write(pngBytes.Length); // bytes in resource
        writer.Write(22); // offset (6 + 16 = 22)

        writer.Write(pngBytes);
    }
}