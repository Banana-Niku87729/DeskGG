using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeskGGUpdater
{
    internal static class Program
    {
        // ================= 設定 =================

        /// <summary>
        /// インストール先パスが書かれた「ファイル」(フォルダではない)。
        /// 中身の例: C:\rec877dev\deskgg
        /// これにより、DeskGGの実際の設置場所をユーザー側で自由に変更できる。
        /// </summary>
        private const string ValidMarkerFilePath = @"C:\rec877dev\valid\deskgg";

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

        /// <summary>このUpdater自身の実行ファイルのフルパス。自己上書き回避に使用する。</summary>
        private static readonly string SelfExePath =
            Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppDir, "DeskGGUpdater.exe");

        /// <summary>このUpdater自身の拡張子なしファイル名 (例: "DeskGGUpdater")。
        /// 同名の.exe/.dll/.pdb/.json等をまとめて上書き対象から除外するために使う。</summary>
        private static readonly string SelfBaseName = Path.GetFileNameWithoutExtension(SelfExePath);

        // valid マーカーファイルの中身から解決される、実際のインストール先。
        // MainFlowAsync内で決定するため実行時に設定する。
        private static string InstallDir = string.Empty;
        private static string DeskGGExePath = string.Empty;

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
            // ---- valid マーカーファイルの確認 & インストール先の解決 ----
            if (!TryResolveInstallDir(out InstallDir, out string resolveError))
            {
                // マーカーファイルが無い/内容が不正な場合は、Updaterと同じフォルダに
                // DeskGG.exeがあればそれをそのまま起動する(最後の手段)。
                string fallbackExe = Path.Combine(AppDir, "DeskGG.exe");
                if (File.Exists(fallbackExe))
                {
                    InstallDir = AppDir;
                    DeskGGExePath = fallbackExe;
                    LaunchDeskGG();
                    return;
                }

                MessageBox.Show(
                    $"インストール先を特定できませんでした。\n{resolveError}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            DeskGGExePath = Path.Combine(InstallDir, "DeskGG.exe");

            if (!File.Exists(DeskGGExePath))
            {
                MessageBox.Show(
                    $"指定されたインストール先にDeskGG.exeが見つかりません。\n{InstallDir}",
                    "DeskGG Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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

        // ================= インストール先の解決 =================

        /// <summary>
        /// C:\rec877dev\valid\deskgg というファイル(フォルダではない)の中身を読み、
        /// そこに書かれたパスが実在し、DeskGG.exeを含んでいるかを確認する。
        /// </summary>
        private static bool TryResolveInstallDir(out string installDir, out string error)
        {
            installDir = string.Empty;
            error = string.Empty;

            if (Directory.Exists(ValidMarkerFilePath))
            {
                // 誤ってフォルダが存在している場合は不正とみなす
                error = $"{ValidMarkerFilePath} はファイルではなくフォルダとして存在しています。";
                return false;
            }

            if (!File.Exists(ValidMarkerFilePath))
            {
                error = $"{ValidMarkerFilePath} が見つかりません。";
                return false;
            }

            string rawPath;
            try
            {
                rawPath = File.ReadAllText(ValidMarkerFilePath).Trim();
            }
            catch (Exception ex)
            {
                error = $"{ValidMarkerFilePath} の読み込みに失敗しました。\n{ex.Message}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rawPath))
            {
                error = $"{ValidMarkerFilePath} の内容が空です。インストール先パスを記入してください。";
                return false;
            }

            if (!Directory.Exists(rawPath))
            {
                error = $"{ValidMarkerFilePath} に記載されたフォルダが存在しません。\n記載内容: {rawPath}";
                return false;
            }

            installDir = rawPath;
            return true;
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

        // ================= 自己ファイル判定 (フリーズ/上書きエラー対策) =================

        /// <summary>
        /// 指定パスがUpdater自身の実行ファイル(またはその関連ファイル)かどうかを判定する。
        /// InstallDirの中にUpdater.exe自体が同居しているケースで、
        /// 実行中の自分自身を削除・上書きしようとしてフリーズ/例外になるのを防ぐ。
        /// </summary>
        private static bool IsSelfRelatedFile(string filePath)
        {
            string fileBaseName = Path.GetFileNameWithoutExtension(filePath);
            return string.Equals(fileBaseName, SelfBaseName, StringComparison.OrdinalIgnoreCase);
        }

        // ================= バックアップ =================

        private static string BackupCurrentInstall()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(BackupRootDir, timestamp);
            Directory.CreateDirectory(backupDir);
            CopyDirectory(InstallDir, backupDir, skipSelf: false);
            return backupDir;
        }

        /// <summary>
        /// skipSelf=true の場合、Updater自身に関連するファイルはコピー/削除対象から除外する。
        /// (自分自身が実行中のままロックされているファイルへの操作を避けるため)
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir, bool skipSelf)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (skipSelf && IsSelfRelatedFile(file))
                {
                    continue;
                }

                string dest = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir, skipSelf);
            }
        }

        // ================= ダウンロード & インストール =================

        /// <summary>
        /// 差分更新: リモート(Latestフォルダ)の内容を再帰的に確認し、
        /// ローカルに存在しない/内容が異なるファイルだけをダウンロードして上書きする。
        /// リモートに存在しないローカルファイルは削除せずそのまま残す。
        /// </summary>
        private static async Task DownloadAndInstallLatestAsync()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGGUpdater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            await SyncGitHubFolderAsync(http, GitHubApiContentsUrl, InstallDir);
        }

        /// <summary>
        /// GitHub Contents APIを使ってフォルダ内容を再帰的に確認し、
        /// 変更があったファイルのみをダウンロードしてlocalDirに反映する。
        /// </summary>
        private static async Task SyncGitHubFolderAsync(HttpClient http, string apiUrl, string localDir)
        {
            Directory.CreateDirectory(localDir);

            string json = await http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString()!;
                string type = item.GetProperty("type").GetString()!;
                string localPath = Path.Combine(localDir, name);

                if (type == "dir")
                {
                    string subApiUrl = item.GetProperty("url").GetString()!;
                    await SyncGitHubFolderAsync(http, subApiUrl, localPath);
                }
                else if (type == "file")
                {
                    // Updater自身に関連するファイルは上書きしない(実行中ロック対策)。
                    if (IsSelfRelatedFile(localPath))
                    {
                        continue;
                    }

                    string remoteSha = item.GetProperty("sha").GetString()!;

                    // ローカルに同名ファイルがあり、内容(Git blob sha)が一致していればスキップ。
                    if (File.Exists(localPath) && ComputeGitBlobSha1(localPath) == remoteSha)
                    {
                        continue;
                    }

                    string downloadUrl = item.GetProperty("download_url").GetString()!;
                    byte[] data = await http.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(localPath, data);
                }
            }
        }

        /// <summary>
        /// GitのblobオブジェクトのSHA1ハッシュをローカルファイルから計算する。
        /// 形式: "blob {byte数}\0{内容}" のSHA1。GitHub APIが返すsha値と同じ計算方法。
        /// </summary>
        private static string ComputeGitBlobSha1(string filePath)
        {
            byte[] content = File.ReadAllBytes(filePath);
            byte[] header = Encoding.UTF8.GetBytes($"blob {content.Length}\0");

            byte[] combined = new byte[header.Length + content.Length];
            Buffer.BlockCopy(header, 0, combined, 0, header.Length);
            Buffer.BlockCopy(content, 0, combined, header.Length, content.Length);

            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(combined);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
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