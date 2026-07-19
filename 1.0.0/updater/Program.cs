using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskGGUpdater
{
    internal static class Program
    {
        // ================= 設定 =================

        /// <summary>DeskGGがインストールされているか確認するフォルダ</summary>
        private const string ValidDeskGGDir = @"C:\rec877dev\valid\deskgg";

        /// <summary>DeskGGの実際のインストール先(置き換え対象)。
        /// 必要に応じてValidDeskGGDirと別のパスに変更してください。</summary>
        private const string InstallDir = ValidDeskGGDir;

        private const string VersionDir = @"C:\rec877dev\version\deskgg";
        private static readonly string LocalVersionJsonPath = Path.Combine(VersionDir, "version.json");
        private static readonly string LocalVersionTxtPath = Path.Combine(VersionDir, "version.txt");
        private static readonly string BackupRootDir = Path.Combine(VersionDir, "backup");

        private const string RemoteVersionJsonUrl =
            "https://raw.githubusercontent.com/Banana-Niku87729/DeskGG/main/version.json";

        private const string GitHubApiContentsUrl =
            "https://api.github.com/repos/Banana-Niku87729/DeskGG/contents/app/main/Latest";

        private const string DeskGGProcessName = "DeskGG";

        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string CheckingSplashImage =
            Path.Combine(AppDir, "DeskGGUpdate_SplashImage_Checking.png");
        private static readonly string UpdateSplashImage =
            Path.Combine(AppDir, "Update_SplashImage.png");

        private static readonly string DeskGGExePath = Path.Combine(InstallDir, "DeskGG.exe");

        private static SplashForm? _checkingSplash;
        private static SplashForm? _updateSplash;

        // ================= エントリポイント =================

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var context = new ApplicationContext();

            _ = RunAsync(context);

            Application.Run(context);
        }

        private static async Task RunAsync(ApplicationContext context)
        {
            try
            {
                await MainFlowAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"予期しないエラーが発生しました。\n\n{ex.Message}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                CloseSplash(ref _checkingSplash);
                CloseSplash(ref _updateSplash);
                context.ExitThread();
            }
        }

        // ================= メインフロー =================

        private static async Task MainFlowAsync()
        {
            bool deskggExists = Directory.Exists(ValidDeskGGDir) && File.Exists(DeskGGExePath);

            if (!deskggExists)
            {
                // インストールが確認できない場合はそのまま起動を試みる
                LaunchDeskGG();
                return;
            }

            // ---- バージョン確認 ----
            ShowSplash(ref _checkingSplash, CheckingSplashImage, "更新を確認しています...");

            bool updateAvailable;
            string? remoteVersionJsonRaw;

            try
            {
                (updateAvailable, remoteVersionJsonRaw) = await CheckForUpdateAsync();
            }
            catch (Exception ex)
            {
                CloseSplash(ref _checkingSplash);
                MessageBox.Show(
                    $"バージョン情報の取得に失敗しました。そのままDeskGGを起動します。\n\n{ex.Message}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                LaunchDeskGG();
                return;
            }

            CloseSplash(ref _checkingSplash);

            if (!updateAvailable || remoteVersionJsonRaw is null)
            {
                LaunchDeskGG();
                return;
            }

            // ---- 更新あり ----
            ShowSplash(ref _updateSplash, UpdateSplashImage, "更新をインストールしています...");

            bool proceed = EnsureDeskGGNotRunning();
            if (!proceed)
            {
                // ユーザーが更新中断を選択した場合はそのまま終了(起動もしない)
                CloseSplash(ref _updateSplash);
                return;
            }

            try
            {
                string backupDir = BackupCurrentInstall();
                await DownloadAndInstallLatestAsync();
                UpdateLocalVersionFiles(remoteVersionJsonRaw);

                CloseSplash(ref _updateSplash);
                MessageBox.Show(
                    $"DeskGGを最新バージョンに更新しました。\n(バックアップ先: {backupDir})",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                CloseSplash(ref _updateSplash);
                MessageBox.Show(
                    $"更新中にエラーが発生しました。DeskGGは起動しません。\n\n{ex.Message}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            LaunchDeskGG();
        }

        // ================= バージョン確認 =================

        private static async Task<(bool updateAvailable, string? remoteRaw)> CheckForUpdateAsync()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGGUpdater");

            string remoteJson = await http.GetStringAsync(RemoteVersionJsonUrl);
            string remoteVersionStr = ExtractVersionString(remoteJson);

            string localVersionStr = "0";
            if (File.Exists(LocalVersionJsonPath))
            {
                string localJson = await File.ReadAllTextAsync(LocalVersionJsonPath);
                try
                {
                    localVersionStr = ExtractVersionString(localJson);
                }
                catch
                {
                    // ローカルversion.jsonが壊れている場合は"0"扱いとして更新させる
                }
            }

            bool isNewer = CompareVersionStrings(remoteVersionStr, localVersionStr) > 0;
            return (isNewer, remoteJson);
        }

        private static string ExtractVersionString(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var versionElement))
            {
                return versionElement.ValueKind == JsonValueKind.Number
                    ? versionElement.GetRawText()
                    : versionElement.GetString() ?? "0";
            }

            throw new InvalidOperationException("version.json に \"version\" フィールドが見つかりません。");
        }

        private static int CompareVersionStrings(string a, string b)
        {
            if (Version.TryParse(NormalizeVersion(a), out var va) &&
                Version.TryParse(NormalizeVersion(b), out var vb))
            {
                return va.CompareTo(vb);
            }

            if (double.TryParse(a, out var da) && double.TryParse(b, out var db))
            {
                return da.CompareTo(db);
            }

            return string.CompareOrdinal(a, b);
        }

        private static string NormalizeVersion(string v)
        {
            // "1.2" のような2要素のバージョンをVersion.Parseできる形式に整える
            var parts = v.Split('.');
            return parts.Length == 1 ? v + ".0" : v;
        }

        // ================= 実行中プロセスの確認 =================

        private static bool EnsureDeskGGNotRunning()
        {
            var procs = Process.GetProcessesByName(DeskGGProcessName);
            if (procs.Length == 0)
            {
                return true;
            }

            var result = MessageBox.Show(
                "DeskGGが起動中です。更新を行うには終了する必要があります。\n終了して更新を続行しますか?",
                "DeskGG Updater",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return false;
            }

            foreach (var p in procs)
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(5000))
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                }
                catch
                {
                    // 既に終了している等のケースは無視して続行
                }
                finally
                {
                    p.Dispose();
                }
            }

            return true;
        }

        // ================= バックアップ =================

        private static string BackupCurrentInstall()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(BackupRootDir, timestamp);
            Directory.CreateDirectory(backupDir);
            CopyDirectory(InstallDir, backupDir);
            return backupDir;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        // ================= ダウンロード & インストール =================

        private static async Task DownloadAndInstallLatestAsync()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGGUpdater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            string tempDir = Path.Combine(Path.GetTempPath(), "DeskGGUpdate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                await DownloadGitHubFolderAsync(http, GitHubApiContentsUrl, tempDir);

                // 既存のインストール内容を削除してから新しいファイルで置き換える
                foreach (var file in Directory.GetFiles(InstallDir))
                {
                    File.Delete(file);
                }

                foreach (var dir in Directory.GetDirectories(InstallDir))
                {
                    Directory.Delete(dir, recursive: true);
                }

                CopyDirectory(tempDir, InstallDir);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // 一時フォルダの削除失敗は無視
                }
            }
        }

        /// <summary>
        /// GitHub Contents APIを使ってフォルダ内容を再帰的にダウンロードする。
        /// </summary>
        private static async Task DownloadGitHubFolderAsync(HttpClient http, string apiUrl, string localDir)
        {
            Directory.CreateDirectory(localDir);

            string json = await http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString()!;
                string type = item.GetProperty("type").GetString()!;

                if (type == "dir")
                {
                    string subApiUrl = item.GetProperty("url").GetString()!;
                    string subLocalDir = Path.Combine(localDir, name);
                    await DownloadGitHubFolderAsync(http, subApiUrl, subLocalDir);
                }
                else if (type == "file")
                {
                    string downloadUrl = item.GetProperty("download_url").GetString()!;
                    string localPath = Path.Combine(localDir, name);
                    byte[] data = await http.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(localPath, data);
                }
            }
        }

        // ================= バージョン情報の更新 =================

        private static void UpdateLocalVersionFiles(string remoteVersionJson)
        {
            Directory.CreateDirectory(VersionDir);
            File.WriteAllText(LocalVersionJsonPath, remoteVersionJson);

            string versionStr = ExtractVersionString(remoteVersionJson);
            File.WriteAllText(LocalVersionTxtPath, versionStr);
        }

        // ================= DeskGG起動 =================

        private static void LaunchDeskGG()
        {
            try
            {
                if (File.Exists(DeskGGExePath))
                {
                    Process.Start(new ProcessStartInfo(DeskGGExePath)
                    {
                        WorkingDirectory = InstallDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show(
                        "DeskGG.exe が見つかりませんでした。",
                        "DeskGG Updater",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"DeskGGの起動に失敗しました。\n\n{ex.Message}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ================= スプラッシュ表示補助 =================

        private static void ShowSplash(ref SplashForm? splash, string imagePath, string statusText)
        {
            splash = new SplashForm(imagePath, statusText);
            splash.Show();
            Application.DoEvents();
        }

        private static void CloseSplash(ref SplashForm? splash)
        {
            if (splash != null && !splash.IsDisposed)
            {
                splash.Close();
                splash.Dispose();
            }

            splash = null;
            Application.DoEvents();
        }
    }
}
