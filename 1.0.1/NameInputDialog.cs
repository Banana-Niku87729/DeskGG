namespace DesktopAppFolder;

/// <summary>フォルダー名の入力/変更に使うシンプルなダイアログ。</summary>
public class NameInputDialog : Form
{
    private readonly TextBox _textBox;

    public string InputText => _textBox.Text;

    public NameInputDialog(string initialText)
    {
        Icon = AppIcon.Shared;
        Text = "フォルダー名";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(280, 90);

        var label = new Label
        {
            Text = "新しいフォルダー名を入力してください:",
            AutoSize = true,
            Location = new Point(12, 12)
        };

        _textBox = new TextBox
        {
            Text = initialText,
            Location = new Point(12, 34),
            Width = 256
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(112, 60)
        };

        var cancelButton = new Button
        {
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel,
            Location = new Point(193, 60)
        };

        Controls.Add(label);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}