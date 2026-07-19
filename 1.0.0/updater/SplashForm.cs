using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DeskGGUpdater
{
    /// <summary>
    /// 画像1枚をセンター表示するシンプルなスプラッシュウィンドウ。
    /// Checking用 / Update用のどちらの表示にも共通して使用する。
    /// </summary>
    public class SplashForm : Form
    {
        private readonly PictureBox _pictureBox;
        private readonly Label _statusLabel;

        public SplashForm(string imagePath, string statusText)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Size = new Size(480, 320);

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            if (File.Exists(imagePath))
            {
                try
                {
                    using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                    _pictureBox.Image = Image.FromStream(stream);

                    if (_pictureBox.Image != null)
                    {
                        // 画像サイズに合わせてウィンドウサイズを調整(大きすぎる場合は上限を設ける)
                        int w = Math.Min(_pictureBox.Image.Width, 900);
                        int h = Math.Min(_pictureBox.Image.Height, 650);
                        Size = new Size(w, h);
                    }
                }
                catch
                {
                    // 画像読み込みに失敗しても起動は継続する
                }
            }

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(160, 0, 0, 0),
                Text = statusText,
                Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular)
            };

            Controls.Add(_pictureBox);
            Controls.Add(_statusLabel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pictureBox.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
