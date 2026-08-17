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
}

