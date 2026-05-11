# Rhythm

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Rhythm 是一个 Windows 桌面常驻便签，用来放每天重复执行的小清单：勾选今天完成的事项，跨过本地午夜后自动重置，第二天继续从同一份清单开始。它固定在桌面底层，不进入任务栏或 Alt-Tab，适合放在屏幕角落当作轻量提醒。

## 适用环境

- Windows 10 / 11 x64
- 用户运行发布版时无需预装 .NET runtime：`publish/Rhythm.exe` 是 win-x64 self-contained 单文件发布目标
- 开发需要在 Windows 上安装 .NET 10 SDK；项目 TargetFramework 为 `net10.0-windows`

## 核心功能

- 桌面便签：暗色、圆角、半透明深灰的紧凑窗口
- 桌面固定：窗口保持在 z-order 最底，不抢焦点、不进 Alt-Tab、不进任务栏
- 每日清单：勾选完成项，本地午夜自动把当天完成状态清空
- 事项编辑：新增、删除、重命名、上下排序
- 托盘常驻：双击托盘图标显示窗口，右键菜单提供显示、隐藏、编辑、开机启动、关于和退出
- 关闭即隐藏：窗口关闭按钮只隐藏到托盘，只有托盘菜单的 "退出" 会终止进程
- 位置记忆：窗口可拖动，保存位置；屏幕变化导致位置越界时回退到默认位置
- 开机启动：托盘菜单可切换，写入当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## 用户运行

发布产物放在 `publish/` 后，直接双击：

```powershell
.\publish\Rhythm.exe
```

首次启动会创建本地状态文件，并预填 3 个示例事项：

- 早起冥想 5 分钟
- 运动 20 分钟
- 读书 30 分钟

可以在托盘菜单 "编辑事项..." 中替换这些示例。

## 本地数据

Rhythm 的清单与窗口状态只保存在本机用户目录：

```text
%APPDATA%\Rhythm\state.json
```

状态文件包含：

- `schemaVersion`
- `items[]`
- `completedToday[]`
- `lastResetDate`
- `windowPos`

如果状态文件缺失或 JSON 损坏，应用会回退到默认状态；保存采用临时文件替换的方式，降低半写入导致下次启动失败的风险。

## 隐私与安全

- 应用本地运行，不需要账号
- 不做网络请求
- 不上传遥测、清单内容或使用记录
- 开机启动只写当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- 安全问题报告方式见 [`SECURITY.md`](SECURITY.md)

## 卸载

1. 如果启用过开机启动，先在托盘菜单取消勾选 "开机启动"。
2. 在托盘菜单选择 "退出"。
3. 删除发布目录中的 `Rhythm.exe`。
4. 如需删除所有本地数据，删除 `%APPDATA%\Rhythm\`。

## 开发

解决方案文件是 `Rhythm.slnx`，WPF 应用项目是 `src/Rhythm/Rhythm.csproj`，测试项目是 `tests/Rhythm.Tests/Rhythm.Tests.csproj`。

```powershell
# Debug 构建
dotnet build Rhythm.slnx -c Debug

# Debug 运行
dotnet run --project src/Rhythm/Rhythm.csproj -c Debug

# Release 构建
dotnet build Rhythm.slnx -c Release

# 单元测试
dotnet test Rhythm.slnx -c Release
```

发布 win-x64 self-contained 单文件 exe 到 `publish/`：

```powershell
dotnet publish src/Rhythm/Rhythm.csproj `
  -c Release `
  -r win-x64 `
  -p:PublishSingleFile=true `
  -p:SelfContained=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

发布后运行：

```powershell
.\publish\Rhythm.exe
```

## 架构

| 层 | 职责 | 关键文件 |
|---|---|---|
| 应用启动与托盘 | 初始化状态、创建主窗口、构建托盘菜单、安排午夜重置 | `src/Rhythm/App.xaml.cs` |
| 状态机 | 每日重置、勾选、增删改移等纯 C# 逻辑，可单测 | `src/Rhythm/State/RhythmState.cs` |
| 状态结构 | `state.json` 的 schema 与记录类型 | `src/Rhythm/State/StateSchema.cs` |
| 持久化 | System.Text.Json 读写，损坏回退默认，保存使用 temp + replace | `src/Rhythm/State/StateStore.cs` |
| 路径 | `%APPDATA%\Rhythm\state.json` 等本地路径 | `src/Rhythm/AppPaths.cs` |
| Win32 互操作 | `HWND_BOTTOM`、`WS_EX_TOOLWINDOW`、`WM_WINDOWPOSCHANGING` 等桌面固定能力 | `src/Rhythm/Interop/Win32.cs` |
| 自启动 | 读写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | `src/Rhythm/Autostart/AutostartManager.cs` |
| ViewModel | `INotifyPropertyChanged`、`ObservableCollection`、UI 操作封装 | `src/Rhythm/UI/MainViewModel.cs` |
| 视图 | 主便签窗口、编辑窗口、关于窗口和主题资源 | `src/Rhythm/MainWindow.xaml`、`src/Rhythm/EditItemsWindow.xaml`、`src/Rhythm/AboutWindow.xaml`、`src/Rhythm/Themes/Dark.xaml` |
| 测试 | 状态机与持久化的 xUnit 覆盖 | `tests/Rhythm.Tests/` |

## 已知限制

- 不支持多设备同步、云存储或账号
- 不支持提醒、通知或弹窗
- 不支持多 list、多 profile 或标签分组
- 不记录历史完成记录、热力图或连续打卡统计
- v1 固定暗色外观，不跟随系统暗 / 浅主题切换
- 不支持全局快捷键
- 不提供字体、圆角、透明度等用户配置项
- 不使用 Mica 背景效果：WPF `AllowsTransparency=True` 与 DWM Mica 互斥，v1 优先保留圆角和全窗透明
- 不支持 macOS、Linux 或移动端

任务规划与决策记录见 `.trellis/tasks/05-08-rhythm-windows-v1/prd.md`。
