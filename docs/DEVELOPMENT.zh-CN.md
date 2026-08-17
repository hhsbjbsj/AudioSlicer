# 开发、测试与发布

## 环境

- Windows 10/11 x64
- PowerShell 7 或 Windows PowerShell 5.1+
- .NET SDK 10.0.400（由 global.json 固定）
- Git

仓库不会提交 .dotnet、FFmpeg 可执行文件、构建产物、缓存或发布包。

## 首次准备

~~~powershell
dotnet --info
dotnet restore .\AudioSlicer.sln
.\scripts\download-ffmpeg.ps1
~~~

download-ffmpeg.ps1 从 gyan.dev 下载 Windows essentials 构建，把 ffmpeg.exe 和 ffprobe.exe 放到 tools/ffmpeg。升级 FFmpeg 时要重新检查许可证和 THIRD_PARTY_NOTICES.md。

如果工作目录中存在 .dotnet\dotnet.exe，发布脚本优先使用它；否则使用系统 dotnet。

## 日常命令

~~~powershell
dotnet build .\AudioSlicer.sln -c Release
dotnet test .\AudioSlicer.sln -c Release
.\scripts\publish.ps1
~~~

测试会真实调用 FFmpeg，覆盖媒体探测、波形缓存、导出、工程序列化、选区生成独立 WAV，以及删除命令。

## 版本与发布

版本号只在 src/AudioSlicer/AudioSlicer.csproj 中维护。publish.ps1 会自动读取 Version，生成版本化目录和 ZIP。这样旧程序正在运行时也不会锁住新版本发布目录。

发布检查清单：

1. 更新 AudioSlicer.csproj 版本。
2. 更新 CHANGELOG.md。
3. 运行 git diff --check。
4. 运行 dotnet test -c Release。
5. 运行 scripts/publish.ps1。
6. 打开发布目录中的 EXE。
7. 实测“导入 → 波形选区 → 生成 WAV → 播放 → 删除/撤销 → ZIP”。
8. 计算 ZIP 的 SHA-256。
9. 创建 Git tag 和 GitHub Release，并把 ZIP 作为 Release 资产上传。

## 自动检查建议

提交前至少运行 `dotnet test .\AudioSlicer.sln -c Release`。发布包应在受控 Windows 环境中运行 `publish.ps1`，完成实机验收后再上传；如果以后接入 CI，可复用“安装固定版本 .NET SDK → 下载 FFmpeg → Restore → Release 测试”这条流程。

## 日志和诊断

- 日志：%LOCALAPPDATA%\AudioSlicer\logs\app.log
- 波形缓存：%LOCALAPPDATA%\AudioSlicer\cache
- 独立任务 WAV：%LOCALAPPDATA%\AudioSlicer\task-audio

遇到导入或导出问题时，首先保存日志、媒体容器格式、音轨编码和 FFmpeg 返回信息，避免只根据 UI 状态猜测。
