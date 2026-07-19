using Microsoft.Win32;

namespace DesktopAppFolder;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run にエントリを追加/削除して
/// Windowsログイン時の自動起動を切り替える。
/// </summary>
public static class StartupHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "DesktopAppFolder";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        if (key?.GetValue(ValueName) == null) return false;

        // Runキーに値があっても、タスクマネージャーで「無効」にされていると
        // StartupApproved\Run 側にそのフラグが残っていて実際には起動しない。
        return !IsDisabledByTaskManager();
    }

    public static void SetRegistered(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                         ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
            ClearTaskManagerDisableFlag();
        }
        else
        {
            if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName);
            }
        }
    }

    /// <summary>
    /// タスクマネージャーの「スタートアップ アプリ」で無効化されているかを、
    /// StartupApproved\Run のバイナリ値（先頭バイトが 02 なら無効）から判定する。
    /// </summary>
    private static bool IsDisabledByTaskManager()
    {
        using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, false);
        if (approvedKey?.GetValue(ValueName) is byte[] data && data.Length > 0)
        {
            return data[0] == 0x02;
        }
        return false;
    }

    /// <summary>
    /// StartupApproved\Run の無効化フラグを「有効」状態に書き換える。
    /// タスクマネージャーで一度無効化されたエントリは、Runキーを書き直すだけでは
    /// 復活しないため、こちらも合わせて更新する必要がある。
    /// </summary>
    private static void ClearTaskManagerDisableFlag()
    {
        try
        {
            using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, true)
                                     ?? Registry.CurrentUser.CreateSubKey(StartupApprovedKeyPath);

            // Windowsが実際に書き込む形式は12バイトだが、有効/無効の判定に使われるのは
            // 先頭1バイトのみなので、既存データがあればそこだけ0に、無ければ12バイトのゼロ配列を書く。
            byte[] data = approvedKey.GetValue(ValueName) as byte[] ?? new byte[12];
            if (data.Length == 0) data = new byte[12];
            data[0] = 0x00;

            approvedKey.SetValue(ValueName, data, RegistryValueKind.Binary);
        }
        catch
        {
            // StartupApprovedキーは環境によってはアクセス不可の場合があるため、
            // 失敗しても致命的ではない（Runキー自体は正しく設定済み）。
        }
    }
}