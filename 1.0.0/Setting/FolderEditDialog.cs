namespace DesktopAppFolder;

/// <summary>フォルダーの新規作成/編集に使う、名前とテーマカラーを入力するダイアログ。</summary>
public class FolderEditDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly Panel _colorPreview;
    private readonly Button _colorButton;

    public string FolderNameInput => _nameBox.Text.Trim();
    public Color ThemeColor { get; private set; }

    /// <summary>ThemeColorが未設定(0)のまま確定されたか。</summary>
    public bool UseDefaultColor { get; private set; }

    public FolderEditDialog(string title, string initialName, int initialThemeColorArgb)
    {
        Icon = AppIcon.Shared;
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 200);

        UseDefaultColor = initialThemeColorArgb == 0;
        ThemeColor = UseDefaultColor ? Color.FromArgb(32, 32, 36) : Color.FromArgb(initialThemeColorArgb);

        var nameLabel = new Label
        {
            Text = "フォルダー名:",
            AutoSize = true,
            Location = new Point(16, 18)
        };
        _nameBox = new TextBox
        {
            Text = initialName,
            Location = new Point(16, 40),
            Width = 368
        };

        var colorLabel = new Label
        {
            Text = "テーマカラー:",
            AutoSize = true,
            Location = new Point(16, 78)
        };

        _colorPreview = new Panel
        {
            Location = new Point(16, 100),
            Size = new Size(32, 28),
            BackColor = ThemeColor,
            BorderStyle = BorderStyle.FixedSingle
        };

        _colorButton = new Button
        {
            Text = "色を選択...",
            Location = new Point(58, 99),
            Size = new Size(120, 30),
            AutoSize = false
        };
        _colorButton.Click += (_, _) => PickColor();

        var resetButton = new Button
        {
            Text = "既定色に戻す",
            Location = new Point(186, 99),
            Size = new Size(120, 30),
            AutoSize = false
        };
        resetButton.Click += (_, _) =>
        {
            UseDefaultColor = true;
            ThemeColor = Color.FromArgb(32, 32, 36);
            _colorPreview.BackColor = ThemeColor;
        };

        // ── 下段ボタン(OK/キャンセル)は右端基準で配置し、見切れないようにする ──
        const int buttonWidth = 96;
        const int buttonHeight = 32;
        const int margin = 16;

        var cancelButton = new Button
        {
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel,
            Size = new Size(buttonWidth, buttonHeight),
            AutoSize = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        cancelButton.Location = new Point(ClientSize.Width - margin - buttonWidth, ClientSize.Height - margin - buttonHeight);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(buttonWidth, buttonHeight),
            AutoSize = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        okButton.Location = new Point(cancelButton.Left - 8 - buttonWidth, cancelButton.Top);
        okButton.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show(this, "フォルダー名を入力してください。", "DeskGG設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        Controls.Add(nameLabel);
        Controls.Add(_nameBox);
        Controls.Add(colorLabel);
        Controls.Add(_colorPreview);
        Controls.Add(_colorButton);
        Controls.Add(resetButton);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void PickColor()
    {
        using var dlg = new ColorDialog
        {
            Color = ThemeColor,
            FullOpen = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            UseDefaultColor = false;
            ThemeColor = dlg.Color;
            _colorPreview.BackColor = ThemeColor;
        }
    }
}