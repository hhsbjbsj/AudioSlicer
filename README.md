# Audio Slicer

Audio Slicer 是一款 Windows x64 视频辅助音频切片器，使用 C#、.NET 10、WPF、MVVM、LibVLCSharp、FFmpeg/ffprobe 与 NAudio 开发。

它专注于人工精确切片：视频与波形共用同一时间轴，所有编辑均为非破坏性决策，最终导出时才生成 WAV、FLAC 或 MP3。程序不包含 AI 识别、VAD、自动分段、自动降噪或说话人识别。

## 功能

- MP4、MKV、FLV 等视频预览，播放、暂停、跳转、音量与倍速。
- 异步音频解码、多级峰值缓存、长视频波形缩放和平移。
- 毫秒/亚毫秒时间输入、边界拖动、统一播放头与循环选区。
- 片段创建、修改、重命名、多选、排序和 500 项虚拟化列表。
- 片段内部删除与静音；删除拼接处使用几毫秒交叉淡化。
- `Ctrl+Z`/`Ctrl+Y` 撤销重做。
- JSON `.audioslice` 工程保存和恢复，视频哈希校验与重新定位。
- WAV/FLAC/MP3 批量导出、Windows 文件名清理、冲突避让与 ZIP 打包。
- 自包含 Windows x64 发布，无需安装 .NET、VLC 或 FFmpeg。

## 构建与测试

```powershell
.\.dotnet\dotnet.exe restore .\AudioSlicer.sln
.\.dotnet\dotnet.exe test .\AudioSlicer.sln -c Release
.\scripts\publish.ps1
```

最终包输出到 `publish\AudioSlicer-win-x64.zip`。
