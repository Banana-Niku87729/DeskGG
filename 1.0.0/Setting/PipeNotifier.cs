using System.IO.Pipes;
using System.Text;

namespace DesktopAppFolder;

/// <summary>
/// 常駐中のDeskGG本体(TrayAppContext)へ、名前付きパイプ経由で
/// 「フォルダー情報が変わったので再読み込みしてほしい」と通知するためのヘルパー。
/// 本体が起動していない場合は何もしない(次回起動時にディスクの内容がそのまま読まれるため問題ない)。
/// </summary>
internal static class PipeNotifier
{
    private const string PipeName = "DesktopAppFolder_Pipe";

    public static void NotifyReload()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("--reload");
        }
        catch
        {
            // DeskGG本体が起動していない場合など。無視してよい。
        }
    }
}
