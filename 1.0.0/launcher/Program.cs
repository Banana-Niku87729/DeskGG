using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskGGLauncher
{
    internal static class Program
    {
        // ===== 設定項目 =====
        private const string GitHubVersionJsonUrl =
            "https://raw.githubusercontent.com/Banana-Niku87729/DeskGG/main/version.json";

        private const string LocalVersionJsonPath =
            @"C:\rec877dev\version\deskgg\version.json";

        // 実行ファイルの場所（起動元フォルダ内を想定。必要に応じてフルパスに変更してください）
        private static readonly string UpdaterExePath =
            Path.Combine(AppContext.BaseDirectory, "updater.exe");

        private static readonly string DeskGGExePath =
            Path.Combine(AppContext.BaseDirectory, "DeskGG.exe");

        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            RunAsync().GetAwaiter().GetResult();
        }

        private static async Task RunAsync()
        {
            try
            {
                Version localVersion = ReadLocalVersion();
                Version remoteVersion = await FetchRemoteVersionAsync();

                if (remoteVersion > localVersion)
                {
                    DialogResult result = MessageBox.Show(
                        $"新しいバージョンが見つかりました。\n\n" +
                        $"現在のバージョン: {localVersion}\n" +
                        $"最新バージョン: {remoteVersion}\n\n" +
                        "アップデートしますか？",
                        "アップデートの確認",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.Yes)
                    {
                        StartUpdater();
                        return;
                    }
                    else
                    {
                        StartDeskGG();
                        return;
                    }
                }
                else
                {
                    // バージョンが同じ、またはローカルの方が新しい場合はそのまま起動
                    StartDeskGG();
                    return;
                }
            }
            catch (Exception ex)
            {
                // バージョン確認に失敗した場合は、確認せずそのままアプリを起動する
                MessageBox.Show(
                    $"バージョン確認中にエラーが発生しました。\nそのままアプリを起動します。\n\n詳細: {ex.Message}",
                    "警告",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                StartDeskGG();
            }
        }

        /// <summary>
        /// ローカルの version.json からバージョンを読み込む
        /// </summary>
        private static Version ReadLocalVersion()
        {
            if (!File.Exists(LocalVersionJsonPath))
            {
                // ローカルファイルが存在しない場合は最も低いバージョン扱いにして
                // 強制的にアップデートを促す
                return new Version(0, 0, 0, 0);
            }

            string json = File.ReadAllText(LocalVersionJsonPath);
            return ParseVersionFromJson(json);
        }

        /// <summary>
        /// GitHub上の version.json をダウンロードしてバージョンを取得する
        /// </summary>
        private static async Task<Version> FetchRemoteVersionAsync()
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGGLauncher/1.0");

            string json = await client.GetStringAsync(GitHubVersionJsonUrl);
            return ParseVersionFromJson(json);
        }

        /// <summary>
        /// JSON文字列からバージョンを抽出する。
        /// { "version": "1.2.3" } の形式を想定。
        /// もし実際のキー名やフォーマットが異なる場合はここを調整してください。
        /// </summary>
        private static Version ParseVersionFromJson(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string versionString;

            if (root.TryGetProperty("version", out JsonElement versionElement))
            {
                versionString = versionElement.ValueKind == JsonValueKind.Number
                    ? versionElement.GetRawText()
                    : versionElement.GetString();
            }
            else
            {
                throw new InvalidOperationException("version.json に 'version' プロパティが見つかりません。");
            }

            return NormalizeToVersion(versionString);
        }

        /// <summary>
        /// "1.2.3" や "1.2" や "5" のような文字列を System.Version に正規化する
        /// </summary>
        private static Version NormalizeToVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
            {
                throw new InvalidOperationException("バージョン文字列が空です。");
            }

            versionString = versionString.Trim();

            // 先頭の "v" などを除去 (例: "v1.2.3" -> "1.2.3")
            if (versionString.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionString = versionString.Substring(1);
            }

            // ドットの数を数え、Systemバージョンが要求する形式に合わせる
            int dotCount = versionString.Split('.').Length - 1;

            if (dotCount == 0)
            {
                versionString += ".0";
            }

            return Version.Parse(versionString);
        }

        private static void StartUpdater()
        {
            StartProcess(UpdaterExePath, "updater.exe");
        }

        private static void StartDeskGG()
        {
            StartProcess(DeskGGExePath, "DeskGG.exe");
        }

        private static void StartProcess(string path, string displayName)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(
                    $"{displayName} が見つかりません。\nパス: {path}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory
            });
        }
    }
}
