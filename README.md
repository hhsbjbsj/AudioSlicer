# Audio Slicer

Audio Slicer 是一款原生 Windows x64 视频辅助音频切片器，面向“边看视频、边听声音、手动精确选区并导出”的工作流。

项目使用 C#、.NET 10、WPF、MVVM、LibVLCSharp、FFmpeg/ffprobe 与 NAudio。它不会加入自动识别、VAD、自动分段或说话人识别；时间数据是唯一真实来源，原始媒体始终只读。

## 当前可用功能

- 导入 MP4、MKV、FLV、AVI、MOV、WebM、M4V、TS、MTS 等媒体。
- 视频播放、进度跳转、音量、倍速，以及与波形共用的统一时间轴。
- 波形滚轮缩放、Shift + 滚轮平移、右键/中键拖动画布。
- 鼠标拖动创建选区、拖动边界微调、直接输入精确时间。
- 点击“生成选区音频”或按 Enter，立即生成独立的 48 kHz / 16-bit / Mono WAV。
- 右侧直接播放、重命名、勾选片段。
- 一键删除勾选片段，或右键单独删除；Ctrl+Z 可撤销。
- 将勾选的独立 WAV 文件打包成 ZIP。
- 长视频多级波形缓存、自包含 Windows x64 发布。

## 快速开始

最终用户直接从 Release 或本地 publish 目录运行 AudioSlicer.exe，无需另装 .NET、VLC 或 FFmpeg。

开发环境需要 Windows、PowerShell 和 .NET SDK 10.0.400：

~~~powershell
dotnet restore .\AudioSlicer.sln
.\scripts\download-ffmpeg.ps1
dotnet test .\AudioSlicer.sln -c Release
.\scripts\publish.ps1
~~~

发布脚本会读取 AudioSlicer.csproj 中的版本号，输出：

~~~text
publish/AudioSlicer-win-x64-v<Version>/
publish/AudioSlicer-win-x64-v<Version>.zip
~~~

## 文档

- [用户使用说明](USER_GUIDE.zh-CN.md)
- [架构与数据流](docs/ARCHITECTURE.zh-CN.md)
- [开发、测试与发布](docs/DEVELOPMENT.zh-CN.md)
- [维护与后续升级注意事项](docs/MAINTENANCE.zh-CN.md)
- [版本记录](CHANGELOG.md)
- [第三方组件声明](THIRD_PARTY_NOTICES.md)

当前稳定版本：1.1.2。
