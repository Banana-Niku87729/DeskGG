namespace DesktopAppFolder;

/// <summary>DeskGGが対応する表示言語。</summary>
public enum AppLanguage
{
    Jp,
    En,
    Kr,
    Cn
}

/// <summary>
/// C:\rec877dev\valid\_language (拡張子なしファイル) の中身を読み取り、
/// アプリ全体の表示言語を決定する。
/// ファイルの中身: "jp" / "en" / "kr" / "cn" のいずれか。
/// ファイルが無い/不正な場合は日本語(jp)にフォールバックする。
/// </summary>
public static class Loc
{
    private const string LanguageFilePath = @"C:\rec877dev\valid\_language deskgg";

    public static AppLanguage Current { get; private set; } = AppLanguage.Jp;

    static Loc()
    {
        Load();
    }

    /// <summary>_language ファイルを読み直して現在の言語を更新する。</summary>
    public static void Load()
    {
        Current = ReadLanguageFromFile();
    }

    private static AppLanguage ReadLanguageFromFile()
    {
        try
        {
            if (!File.Exists(LanguageFilePath))
                return AppLanguage.Jp;

            string raw = File.ReadAllText(LanguageFilePath).Trim().ToLowerInvariant();
            return raw switch
            {
                "en" => AppLanguage.En,
                "kr" => AppLanguage.Kr,
                "cn" => AppLanguage.Cn,
                "jp" => AppLanguage.Jp,
                _ => AppLanguage.Jp
            };
        }
        catch
        {
            return AppLanguage.Jp;
        }
    }

    /// <summary>言語を変更し、_language ファイルへ書き戻す。</summary>
    public static void SetLanguage(AppLanguage lang)
    {
        Current = lang;
        try
        {
            string code = lang switch
            {
                AppLanguage.En => "en",
                AppLanguage.Kr => "kr",
                AppLanguage.Cn => "cn",
                _ => "jp"
            };

            string? dir = Path.GetDirectoryName(LanguageFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(LanguageFilePath, code);
        }
        catch
        {
            // 書き込みに失敗しても、実行中のアプリの表示言語(メモリ上)は変更済みのまま継続する
        }
    }

    /// <summary>現在の言語に対応する文言を取得する。見つからない場合は日本語→キーの順にフォールバックする。</summary>
    public static string T(string key)
    {
        if (Map.TryGetValue(key, out var byLang))
        {
            if (byLang.TryGetValue(Current, out var s)) return s;
            if (byLang.TryGetValue(AppLanguage.Jp, out var jp)) return jp;
        }
        return key;
    }

    /// <summary>string.Formatと組み合わせて使う版。</summary>
    public static string F(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Map = new()
    {
        // ── 共通 ──────────────────────────────
        ["ok"] = new() { [AppLanguage.Jp] = "OK", [AppLanguage.En] = "OK", [AppLanguage.Kr] = "확인", [AppLanguage.Cn] = "确定" },
        ["cancel"] = new() { [AppLanguage.Jp] = "キャンセル", [AppLanguage.En] = "Cancel", [AppLanguage.Kr] = "취소", [AppLanguage.Cn] = "取消" },
        ["confirm_title"] = new() { [AppLanguage.Jp] = "確認", [AppLanguage.En] = "Confirm", [AppLanguage.Kr] = "확인", [AppLanguage.Cn] = "确认" },
        ["appname"] = new() { [AppLanguage.Jp] = "DeskGG", [AppLanguage.En] = "DeskGG", [AppLanguage.Kr] = "DeskGG", [AppLanguage.Cn] = "DeskGG" },

        // ── NameInputDialog ──────────────────────────────
        ["nameinput.title"] = new() { [AppLanguage.Jp] = "フォルダー名", [AppLanguage.En] = "Folder Name", [AppLanguage.Kr] = "폴더 이름", [AppLanguage.Cn] = "文件夹名称" },
        ["nameinput.label"] = new() { [AppLanguage.Jp] = "新しいフォルダー名を入力してください:", [AppLanguage.En] = "Enter a new folder name:", [AppLanguage.Kr] = "새 폴더 이름을 입력하세요:", [AppLanguage.Cn] = "请输入新的文件夹名称:" },

        // ── FolderEditDialog ──────────────────────────────
        ["folderedit.add_title"] = new() { [AppLanguage.Jp] = "フォルダーの追加", [AppLanguage.En] = "Add Folder", [AppLanguage.Kr] = "폴더 추가", [AppLanguage.Cn] = "添加文件夹" },
        ["folderedit.edit_title"] = new() { [AppLanguage.Jp] = "フォルダーの編集", [AppLanguage.En] = "Edit Folder", [AppLanguage.Kr] = "폴더 편집", [AppLanguage.Cn] = "编辑文件夹" },
        ["folderedit.name_label"] = new() { [AppLanguage.Jp] = "フォルダー名:", [AppLanguage.En] = "Folder Name:", [AppLanguage.Kr] = "폴더 이름:", [AppLanguage.Cn] = "文件夹名称:" },
        ["folderedit.theme_label"] = new() { [AppLanguage.Jp] = "テーマカラー:", [AppLanguage.En] = "Theme Color:", [AppLanguage.Kr] = "테마 색상:", [AppLanguage.Cn] = "主题颜色:" },
        ["folderedit.pick_color"] = new() { [AppLanguage.Jp] = "色を選択...", [AppLanguage.En] = "Choose Color...", [AppLanguage.Kr] = "색상 선택...", [AppLanguage.Cn] = "选择颜色..." },
        ["folderedit.reset_color"] = new() { [AppLanguage.Jp] = "既定色に戻す", [AppLanguage.En] = "Reset to Default", [AppLanguage.Kr] = "기본 색상으로 재설정", [AppLanguage.Cn] = "恢复默认颜色" },
        ["folderedit.name_required"] = new() { [AppLanguage.Jp] = "フォルダー名を入力してください。", [AppLanguage.En] = "Please enter a folder name.", [AppLanguage.Kr] = "폴더 이름을 입력하세요.", [AppLanguage.Cn] = "请输入文件夹名称。" },
        ["folderedit.new_name_default"] = new() { [AppLanguage.Jp] = "新しいフォルダー {0}", [AppLanguage.En] = "New Folder {0}", [AppLanguage.Kr] = "새 폴더 {0}", [AppLanguage.Cn] = "新建文件夹 {0}" },

        // ── SettingsForm ──────────────────────────────
        ["settings.title"] = new() { [AppLanguage.Jp] = "DeskGG 設定", [AppLanguage.En] = "DeskGG Settings", [AppLanguage.Kr] = "DeskGG 설정", [AppLanguage.Cn] = "DeskGG 设置" },
        ["settings.header"] = new() { [AppLanguage.Jp] = "デスクトップフォルダー", [AppLanguage.En] = "Desktop Folders", [AppLanguage.Kr] = "데스크톱 폴더", [AppLanguage.Cn] = "桌面文件夹" },
        ["settings.add_button"] = new() { [AppLanguage.Jp] = "+ 追加", [AppLanguage.En] = "+ Add", [AppLanguage.Kr] = "+ 추가", [AppLanguage.Cn] = "+ 添加" },
        ["settings.delete_confirm"] = new()
        {
            [AppLanguage.Jp] = "「{0}」を削除しますか?\n(フォルダー内のアプリ自体は削除されません)",
            [AppLanguage.En] = "Delete \"{0}\"?\n(Apps inside the folder will not be deleted.)",
            [AppLanguage.Kr] = "\"{0}\"을(를) 삭제하시겠습니까?\n(폴더 안의 앱 자체는 삭제되지 않습니다)",
            [AppLanguage.Cn] = "确定要删除“{0}”吗?\n(文件夹内的应用本身不会被删除)"
        },
        ["settings.language_label"] = new() { [AppLanguage.Jp] = "言語:", [AppLanguage.En] = "Language:", [AppLanguage.Kr] = "언어:", [AppLanguage.Cn] = "语言:" },
        ["settings.language_changed"] = new()
        {
            [AppLanguage.Jp] = "言語を変更しました。\n変更をすべて反映するには、DeskGGを再起動してください。",
            [AppLanguage.En] = "Language changed.\nPlease restart DeskGG for the change to fully take effect.",
            [AppLanguage.Kr] = "언어가 변경되었습니다.\n변경 사항을 완전히 적용하려면 DeskGG를 다시 시작하세요.",
            [AppLanguage.Cn] = "语言已更改。\n请重新启动 DeskGG 以使更改完全生效。"
        },

        // ── TrayAppContext (トレイメニュー) ──────────────────────────────
        ["tray.new_folder"] = new() { [AppLanguage.Jp] = "新規フォルダー作成", [AppLanguage.En] = "Create New Folder", [AppLanguage.Kr] = "새 폴더 만들기", [AppLanguage.Cn] = "新建文件夹" },
        ["tray.open_settings"] = new() { [AppLanguage.Jp] = "設定を開く", [AppLanguage.En] = "Open Settings", [AppLanguage.Kr] = "설정 열기", [AppLanguage.Cn] = "打开设置" },
        ["tray.run_at_startup"] = new() { [AppLanguage.Jp] = "Windows起動時に自動実行", [AppLanguage.En] = "Run at Windows Startup", [AppLanguage.Kr] = "Windows 시작 시 자동 실행", [AppLanguage.Cn] = "开机自动启动" },
        ["tray.open_kofi"] = new() { [AppLanguage.Jp] = "Ko-Fiを開く", [AppLanguage.En] = "Open Ko-Fi", [AppLanguage.Kr] = "Ko-Fi 열기", [AppLanguage.Cn] = "打开 Ko-Fi" },
        ["tray.join_discord"] = new() { [AppLanguage.Jp] = "サポートDiscordサーバーに参加", [AppLanguage.En] = "Join Support Discord Server", [AppLanguage.Kr] = "지원 디스코드 서버 참가", [AppLanguage.Cn] = "加入支持 Discord 服务器" },
        ["tray.open_twitter"] = new() { [AppLanguage.Jp] = "開発者のTwitterを開く", [AppLanguage.En] = "Open Developer's Twitter", [AppLanguage.Kr] = "개발자 트위터 열기", [AppLanguage.Cn] = "打开开发者的 Twitter" },
        ["tray.exit"] = new() { [AppLanguage.Jp] = "終了", [AppLanguage.En] = "Exit", [AppLanguage.Kr] = "종료", [AppLanguage.Cn] = "退出" },

        ["tray.settings_file_missing"] = new()
        {
            [AppLanguage.Jp] = "設定ファイルが見つかりません。\n{0}",
            [AppLanguage.En] = "Settings file not found.\n{0}",
            [AppLanguage.Kr] = "설정 파일을 찾을 수 없습니다.\n{0}",
            [AppLanguage.Cn] = "找不到设置文件。\n{0}"
        },
        ["tray.settings_file_empty"] = new()
        {
            [AppLanguage.Jp] = "設定ファイルの中身が空です。",
            [AppLanguage.En] = "The settings file is empty.",
            [AppLanguage.Kr] = "설정 파일의 내용이 비어 있습니다.",
            [AppLanguage.Cn] = "设置文件内容为空。"
        },
        ["tray.setting_exe_missing"] = new()
        {
            [AppLanguage.Jp] = "setting.exe が見つかりません。\n{0}",
            [AppLanguage.En] = "setting.exe was not found.\n{0}",
            [AppLanguage.Kr] = "setting.exe를 찾을 수 없습니다.\n{0}",
            [AppLanguage.Cn] = "找不到 setting.exe。\n{0}"
        },
        ["tray.settings_launch_failed"] = new()
        {
            [AppLanguage.Jp] = "設定アプリの起動に失敗しました。\n{0}",
            [AppLanguage.En] = "Failed to launch the settings app.\n{0}",
            [AppLanguage.Kr] = "설정 앱을 실행하지 못했습니다.\n{0}",
            [AppLanguage.Cn] = "启动设置应用失败。\n{0}"
        },
        ["tray.folder_created"] = new()
        {
            [AppLanguage.Jp] = "デスクトップに「{0}」を作成しました。",
            [AppLanguage.En] = "\"{0}\" was created on the desktop.",
            [AppLanguage.Kr] = "바탕화면에 \"{0}\"을(를) 만들었습니다.",
            [AppLanguage.Cn] = "已在桌面创建“{0}”。"
        },
        ["tray.folder_full"] = new()
        {
            [AppLanguage.Jp] = "フォルダーは最大9個までです。",
            [AppLanguage.En] = "A folder can hold up to 9 items.",
            [AppLanguage.Kr] = "폴더에는 최대 9개까지 담을 수 있습니다.",
            [AppLanguage.Cn] = "每个文件夹最多只能放 9 个项目。"
        },
        ["tray.default_folder_name"] = new() { [AppLanguage.Jp] = "DeskGG", [AppLanguage.En] = "DeskGG", [AppLanguage.Kr] = "DeskGG", [AppLanguage.Cn] = "DeskGG" },

        // ── SettingsForm フォルダータイルの右クリックメニュー ──────────────────────────────
        ["tile.edit"] = new() { [AppLanguage.Jp] = "編集", [AppLanguage.En] = "Edit", [AppLanguage.Kr] = "편집", [AppLanguage.Cn] = "编辑" },
        ["tile.delete"] = new() { [AppLanguage.Jp] = "削除", [AppLanguage.En] = "Delete", [AppLanguage.Kr] = "삭제", [AppLanguage.Cn] = "删除" },

        // ── PopupForm ──────────────────────────────
        ["popup.remove_from_folder"] = new() { [AppLanguage.Jp] = "フォルダーから削除", [AppLanguage.En] = "Remove from Folder", [AppLanguage.Kr] = "폴더에서 제거", [AppLanguage.Cn] = "从文件夹中移除" },
        ["popup.run_as_admin"] = new() { [AppLanguage.Jp] = "管理者として実行", [AppLanguage.En] = "Run as Administrator", [AppLanguage.Kr] = "관리자 권한으로 실행", [AppLanguage.Cn] = "以管理员身份运行" },
        ["popup.open_file_location"] = new() { [AppLanguage.Jp] = "ファイルの場所を開く", [AppLanguage.En] = "Open File Location", [AppLanguage.Kr] = "파일 위치 열기", [AppLanguage.Cn] = "打开文件所在位置" },
        ["popup.properties"] = new() { [AppLanguage.Jp] = "プロパティ", [AppLanguage.En] = "Properties", [AppLanguage.Kr] = "속성", [AppLanguage.Cn] = "属性" },
        ["popup.show_more_options"] = new() { [AppLanguage.Jp] = "その他のオプションを表示", [AppLanguage.En] = "Show More Options", [AppLanguage.Kr] = "추가 옵션 표시", [AppLanguage.Cn] = "显示更多选项" },

        ["popup.rename_folder"] = new() { [AppLanguage.Jp] = "フォルダー名を変更", [AppLanguage.En] = "Rename Folder", [AppLanguage.Kr] = "폴더 이름 변경", [AppLanguage.Cn] = "重命名文件夹" },
        ["popup.customize_folder"] = new() { [AppLanguage.Jp] = "フォルダーをカスタマイズ...", [AppLanguage.En] = "Customize Folder...", [AppLanguage.Kr] = "폴더 사용자 지정...", [AppLanguage.Cn] = "自定义文件夹..." },
        ["popup.remove_all_contents"] = new() { [AppLanguage.Jp] = "中身をすべて削除", [AppLanguage.En] = "Remove All Contents", [AppLanguage.Kr] = "모든 항목 삭제", [AppLanguage.Cn] = "清空全部内容" },
        ["popup.remove_all_confirm"] = new()
        {
            [AppLanguage.Jp] = "フォルダー内のすべてのアプリを削除しますか?",
            [AppLanguage.En] = "Remove all apps in this folder?",
            [AppLanguage.Kr] = "이 폴더의 모든 앱을 삭제하시겠습니까?",
            [AppLanguage.Cn] = "确定要删除此文件夹中的所有应用吗?"
        },
        ["popup.delete_this_folder"] = new() { [AppLanguage.Jp] = "このフォルダーを削除", [AppLanguage.En] = "Delete This Folder", [AppLanguage.Kr] = "이 폴더 삭제", [AppLanguage.Cn] = "删除此文件夹" },
        ["popup.delete_folder_confirm"] = new()
        {
            [AppLanguage.Jp] = "「{0}」を削除しますか?(中のアプリ自体は削除されません)",
            [AppLanguage.En] = "Delete \"{0}\"? (Apps inside will not be deleted.)",
            [AppLanguage.Kr] = "\"{0}\"을(를) 삭제하시겠습니까? (안의 앱 자체는 삭제되지 않습니다)",
            [AppLanguage.Cn] = "确定要删除“{0}”吗?(其中的应用本身不会被删除)"
        },
        ["popup.launch_failed"] = new()
        {
            [AppLanguage.Jp] = "起動に失敗しました:\n{0}",
            [AppLanguage.En] = "Failed to launch:\n{0}",
            [AppLanguage.Kr] = "실행에 실패했습니다:\n{0}",
            [AppLanguage.Cn] = "启动失败:\n{0}"
        },
        ["popup.admin_launch_failed"] = new()
        {
            [AppLanguage.Jp] = "管理者としての起動に失敗しました:\n{0}",
            [AppLanguage.En] = "Failed to launch as administrator:\n{0}",
            [AppLanguage.Kr] = "관리자 권한으로 실행하지 못했습니다:\n{0}",
            [AppLanguage.Cn] = "以管理员身份启动失败:\n{0}"
        },
        ["popup.file_not_found"] = new()
        {
            [AppLanguage.Jp] = "ファイルが見つかりませんでした。",
            [AppLanguage.En] = "The file could not be found.",
            [AppLanguage.Kr] = "파일을 찾을 수 없습니다.",
            [AppLanguage.Cn] = "未找到该文件。"
        },
    };
}