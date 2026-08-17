# 维护与后续升级注意事项

## 当前稳定产品范围（v1.1.1）

已经完成并暴露在 UI 中：

- 视频导入和同步播放
- 波形生成、缩放、平移和精确选区
- 选区立即生成独立 WAV
- 右侧重命名、播放、勾选
- 勾选批量删除、右键单项删除、Undo / Redo
- 勾选任务音频打包 ZIP

代码中还保留工程保存、更多格式导出、内部删除/静音等基础模块，但当前极简 UI 没有全部暴露。后续只有在完整工作流和真实 UI 测试通过后再增加入口，避免出现“按钮存在、功能链未接通”。

## 推荐升级顺序

1. 为每个新增功能先写 ViewModel 工作流测试。
2. 再添加 UI 入口和绑定回归测试。
3. 用短测试媒体实机走完整路径。
4. 最后发布版本化目录，不覆盖正在运行的版本。

适合下一步评估的功能：

- 恢复工程保存/打开的现代化入口。
- 恢复 WAV / FLAC / MP3 配置和批量导出。
- 片段切点重新编辑。
- 片段内部删除、静音和交叉淡化。
- 可视化缓存管理。
- Release 自动上传和签名。

## 历史故障与防回归重点

### 右侧生成按钮无反应

原因是 SegmentListView 被绑定到 SegmentListViewModel，但 XAML 命令绑定按 MainViewModel 编写。必须保持 SegmentPanel.DataContext 为主 ViewModel，并保留测试中的断言。

### 后端测试通过但真实 UI 不工作

直接调用 ViewModel 会绕过 XAML 绑定。核心流程必须同时具备 ViewModel 行为测试、XAML DataContext / Command 绑定测试，以及发布 EXE 的实机点击验证。

### 右键菜单命令为空

WPF ContextMenu 不属于主视觉树。命令通过 PlacementTarget.Tag 取得主 ViewModel，参数通过 ContextMenu 的 DataContext 取得当前片段。不要改成普通祖先绑定。

### VideoView 遮挡 WPF 覆盖层

LibVLCSharp 的视频宿主涉及 WPF Airspace。不要假设任意 WPF 控件都能覆盖在视频表面上；占位层、字幕层和弹出层必须实机验证。

### 发布目录被锁定

运行中的 Windows EXE 会锁定部分文件。发布目录包含版本号，升级时创建新目录；不要强杀用户正在使用的旧进程，也不要在锁定失败后扩大删除范围。

## 不可破坏的约束

- 不修改原始媒体。
- 不把选区保存成像素。
- 不在 UI 线程运行 FFmpeg。
- 不把 AI、VAD 或自动分段加入核心流程。
- 不把未经验证的按钮放进主界面。
- 不提交 .dotnet、tools/ffmpeg 下的 EXE、publish、artifacts、日志和用户媒体。

## 提交前检查

~~~powershell
git status -sb
git diff --check
dotnet test .\AudioSlicer.sln -c Release
~~~

确认 git status 中没有视频、生成音频、ZIP、日志、缓存、IDE 配置或本机 SDK。
