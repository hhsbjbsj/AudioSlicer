# 架构与数据流

## 技术栈

- .NET 10 / C# 14
- WPF
- CommunityToolkit.Mvvm
- LibVLCSharp + VideoLAN.LibVLC.Windows
- FFmpeg / ffprobe
- NAudio
- xUnit

## 目录职责

~~~text
src/AudioSlicer/
├─ Views/          WPF 视图和少量纯界面事件
├─ ViewModels/     页面状态、命令和工作流编排
├─ Models/         Segment、AudioRange、工程和导出设置
├─ Media/          LibVLC、ffprobe、进程调用、独立音频播放
├─ Waveform/       波形生成、缓存、绘制和时间坐标转换
├─ Export/         FFmpeg 音频裁切、批量导出和 ZIP
├─ Editing/        Undo / Redo 快照
├─ Project/        .audioslice JSON 序列化
└─ Services/       文件对话框、日志等桌面服务
~~~

## 核心数据流

~~~text
媒体文件
  ├─ LibVLC → 视频画面 / 播放状态
  ├─ ffprobe → 时长、音轨、媒体信息
  └─ FFmpeg → PCM 峰值 → 多级波形缓存

波形拖动
  → SelectionStartSeconds / SelectionEndSeconds
  → 生成 Segment（保存真实时间）
  → FFmpeg 精确裁切
  → 独立 WAV
  → 右侧播放 / 勾选 / ZIP
~~~

## 必须保持的设计原则

### 时间是唯一真实数据

选区始终保存为 TimeSpan 或秒数，不保存 GUI 像素。缩放、窗口尺寸和 DPI 只影响绘制，不能改变真实切点。

### 原始媒体永远只读

所有操作都写入新的任务 WAV、导出文件或工程 JSON。不得覆盖、重命名或修改用户导入的视频。

### 预览精度与导出精度分离

LibVLC Seek 受视频关键帧影响，允许轻微预览偏差；最终音频必须由 FFmpeg 按真实时间重新解码和裁切。

### UI 只暴露已经打通的工作流

当前产品表面只保留导入、播放、精确选区、生成独立 WAV、播放、删除和 ZIP。后台即使已有实验性命令，也不应在没有完整实机验证前放到主界面。

## ViewModel 绑定注意事项

SegmentListView 必须继承 MainViewModel 作为 DataContext。它的绑定同时访问：

- AddSegmentCommand
- DeleteCheckedSegmentsCommand
- DeleteSegmentCommand
- PlayTaskAudioCommand
- PackageTaskAudioCommand
- Segments.Segments

不要把它重新绑定为 Segments，否则列表可能显示但主命令会全部失效。这是项目历史上最重要的一次 UI 链路故障。

WPF ContextMenu 位于独立的 Popup 视觉树中，不能直接用普通 AncestorType=UserControl 找主 ViewModel。当前实现通过卡片 Tag 把主 ViewModel 传给 PlacementTarget，修改右键菜单时必须保留这一点。

## 缓存与任务音频

- 波形缓存：%LOCALAPPDATA%\AudioSlicer\cache
- 任务 WAV：%LOCALAPPDATA%\AudioSlicer\task-audio/<media-hash>/<segment-id>.wav

从右侧删除片段时只移除任务模型，并保留 Undo 能力；不会删除原始媒体。任务 WAV 属于可再生缓存，未来如增加缓存清理，应独立实现并明确提示用户。

## 异步边界

媒体探测、波形生成、音频裁切和 ZIP 都必须异步运行，并接受 CancellationToken。WPF UI 线程只更新可观察状态，不运行 FFmpeg 或大量文件 I/O。
