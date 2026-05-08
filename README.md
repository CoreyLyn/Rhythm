# Rhythm

Windows 桌面便签：每天显示一份重复 to-do 清单；勾选完成；本地午夜自动重置；事项可自定义。

## 功能

- 透明紧致的桌面便签窗口（暗色，圆角，半透明深灰）
- 桌面固定：窗口处于 z-order 最底（不抢焦点、不进 Alt-Tab、不进任务栏）
- 每日重置：跨过本地午夜自动把所有项变为未勾选
- 编辑事项：增 / 删 / 重命名 / 上下排序
- 托盘图标：双击显示窗口，右键菜单（显示窗口 / 编辑事项 / 开机启动 / 关于 / 退出）
- 关闭 = 隐藏到托盘（不退出进程，仅托盘 "退出" 真终止）
- 窗口可拖动，位置记忆；屏幕分辨率变化导致越界时回退默认（右上角偏下）
- 开机启动：托盘菜单一键切换（写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run）

## 系统要求

- Windows 10 / 11 (x64)
- 无需预装 .NET runtime（单文件 self-contained）

## 运行

双击 `publish/Rhythm.exe`。

首次启动会预填 3 个示例事项（早起冥想 / 运动 / 读书），可在托盘菜单 "编辑事项" 中删除替换。

## 数据

- 配置与状态：`%APPDATA%\Rhythm\state.json`
- 含字段：`items[]`、`completedToday[]`、`lastResetDate`、`windowPos`、`schemaVersion`

## 卸载

1. 托盘菜单 → "开机启动" 取消勾选（如曾启用）
2. 托盘菜单 → "退出"
3. 删除 `Rhythm.exe`
4. 删除 `%APPDATA%\Rhythm\` 目录

## 已知限制（v1 不做）

- 多设备同步 / 云存储 / 账号
- 提醒 / 通知 / 弹窗
- 多 list / 多 profile / 标签分组
- 历史完成记录 / 热力图 / 连续打卡统计
- 跟随系统暗 / 浅主题切换（v1 锁定暗色）
- 全局快捷键
- 字体 / 圆角 / 透明度等用户配置项
- Mica 背景效果（WPF `AllowsTransparency=True` 与 DWM Mica 互斥；v1 优先保留圆角与全窗透明，舍弃 Mica；详见 `.trellis/tasks/05-08-rhythm-windows-v1/prd.md` ADR D4）
- macOS / Linux / 移动端

## 开发

需要 .NET 10 SDK（`net10.0-windows` TargetFramework）。

```bash
# 构建
dotnet build Rhythm.slnx -c Release

# 跑单测
dotnet test Rhythm.slnx -c Release

# 发布单文件 self-contained exe（输出到 publish/）
dotnet publish src/Rhythm/Rhythm.csproj \
  -c Release \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish
```

## 架构

| 层 | 职责 | 关键文件 |
|---|---|---|
| 状态机 | 纯 C# 不依赖 WPF，可单测 | `src/Rhythm/State/RhythmState.cs` |
| 持久化 | System.Text.Json，atomic write via temp+rename | `src/Rhythm/State/StateStore.cs` |
| Win32 互操作 | P/Invoke：`HWND_BOTTOM` + `WS_EX_TOOLWINDOW` + `WM_WINDOWPOSCHANGING` 拦截 | `src/Rhythm/Interop/Win32.cs` |
| 自启动 | 写 / 读 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | `src/Rhythm/Autostart/AutostartManager.cs` |
| ViewModel | INotifyPropertyChanged + ObservableCollection | `src/Rhythm/UI/MainViewModel.cs` |
| 视图 | XAML：MainWindow（透明圆角便签）+ EditItemsWindow（编辑面板） | `src/Rhythm/MainWindow.xaml` 等 |
| 托盘 | H.NotifyIcon.Wpf | `src/Rhythm/App.xaml.cs` 中 `BuildTrayIcon` |

任务规划与决策记录见 `.trellis/tasks/05-08-rhythm-windows-v1/prd.md`。
