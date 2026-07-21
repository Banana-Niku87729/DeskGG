namespace DesktopAppFolder;

internal static class SettingsProgram
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SettingsForm());
    }
}
