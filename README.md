# Audio Slicer

Audio Slicer 是一款面向 Windows 的非破坏性视频辅助音频切片工具。当前代码完成 Phase 1：WPF/MVVM 应用骨架、深色主界面、媒体文件导入、LibVLC 视频播放、进度跳转、音量、倍速与统一时间显示。

## 构建

仓库内 `.dotnet` 是本地开发 SDK（已被 Git 忽略）。在仓库根目录运行：

```powershell
.\.dotnet\dotnet.exe restore .\AudioSlicer.sln
.\.dotnet\dotnet.exe build .\AudioSlicer.sln -c Release
```

## 设计原则

- 原始媒体始终只读。
- 时间值是编辑数据的唯一事实来源，像素只负责显示。
- 后续耗时媒体任务必须异步且支持取消。
- 不包含 AI 识别、VAD、自动分段或自动降噪。

