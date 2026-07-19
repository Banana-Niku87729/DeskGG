namespace DesktopAppFolder;

/// <summary>
/// exeに埋め込まれたicon.ico(ApplicationIconとして設定したもの)を1度だけ読み込み、
/// アプリ内の全Formで使い回すための共有アイコン。
/// これをFormのIconに明示的にセットしないと、タスクマネージャーやAlt+Tabなどで
/// WinForms既定のアイコンが表示されてしまい、タスクトレイのアイコンと見た目が揃わない。
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Icon> _shared = new(() =>
    {
        try
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null) return icon;
        }
        catch { /* フォールバックへ */ }

        return SystemIcons.Application;
    });

    public static Icon Shared => _shared.Value;
}
