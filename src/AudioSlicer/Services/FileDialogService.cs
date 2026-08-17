using Microsoft.Win32;

namespace AudioSlicer.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? SelectMediaFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择视频文件",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "视频文件|*.mp4;*.mkv;*.flv;*.avi;*.mov;*.webm;*.m4v;*.ts;*.mts|所有文件|*.*",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectProjectToOpen()
    {
        var dialog = new OpenFileDialog { Title = "打开 Audio Slicer 工程", Filter = "Audio Slicer 工程|*.audioslice|所有文件|*.*", CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectProjectToSave(string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = "保存 Audio Slicer 工程", Filter = "Audio Slicer 工程|*.audioslice", AddExtension = true, DefaultExt = ".audioslice", FileName = suggestedName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectExportDirectory()
    {
        var dialog = new OpenFolderDialog { Title = "选择音频导出目录", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? SelectZipPath(string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = "保存 ZIP 压缩包", Filter = "ZIP 压缩包|*.zip", AddExtension = true, DefaultExt = ".zip", FileName = suggestedName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
