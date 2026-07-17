namespace DesktopAppFolder;

/// <summary>
/// 起動時に SplashImage.png を枠なしで一定時間だけ表示するスプラッシュ画面。
/// </summary>
internal class SplashForm : Form
{
    public SplashForm(Image image)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

        ClientSize = image.Size;

        var pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        Controls.Add(pictureBox);
    }

    /// <summary>SplashImage.pngをexeと同じフォルダーから読み込み、指定ミリ秒間表示してから閉じる。
    /// 画像が見つからない場合は何もしない。</summary>
    public static void ShowForMilliseconds(int milliseconds)
    {
        string imagePath = Path.Combine(AppContext.BaseDirectory, "SplashImage.png");
        if (!File.Exists(imagePath)) return;

        Image image;
        try
        {
            using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            image = Image.FromStream(fs);
        }
        catch
        {
            return;
        }

        using var splash = new SplashForm(image);
        splash.Show();
        splash.Refresh();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        image.Dispose();
        splash.Close();
    }
}
