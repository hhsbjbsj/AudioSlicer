# Audio Slicer 使用说明

1. 双击 `AudioSlicer.exe`，点击“导入视频”。支持 MP4、MKV、FLV、AVI、MOV、WebM、M4V、TS 和 MTS。
2. 在波形上左键拖动创建选区；拖动两侧边界微调。滚轮缩放，`Shift+滚轮` 横移，右键或中键拖动画布。
3. 可在下方直接输入秒数或 `hh:mm:ss.fff`，点击“应用时间”。按 `L` 循环试听选区。
4. 点击“添加”或按 `Enter` 创建非破坏性片段。双击右侧片段可返回其原视频位置。
5. 双击片段后，在其范围内框选小区间，再点击“删除区间”或“静音区间”。删除会在导出时拼接左右音频并应用极短交叉淡化；静音保持总时长。
6. 在右侧选择 WAV、FLAC 或 MP3、采样率和声道，勾选要导出的片段，然后批量导出或打包 ZIP。
7. 使用“保存工程”保存 `.audioslice`。工程只保存视频引用和编辑决策，不复制或修改原视频。

常用快捷键：`Space` 播放/暂停，方向键移动 100ms，`Shift+方向键` 移动 1ms，`[`/`]` 设置边界，`Enter` 添加片段，`Delete` 删除片段，`Ctrl+Z`/`Ctrl+Y` 撤销/重做，`Ctrl+A` 全选，`Home` 回到选区开始，`Escape` 取消选区。

日志位置：`%LOCALAPPDATA%\AudioSlicer\logs\app.log`。波形缓存位于 `%LOCALAPPDATA%\AudioSlicer\cache`。

