# Rhythm — Windows 桌面便签 v1

## Goal

在 Windows 桌面上常驻一个**透明紧致**的便签窗口，显示当天需要重复完成的事项 (recurring to-dos)。用户勾选完成项；到下一天 0 点（本地时区）自动重置为未完成；事项列表可由用户自行编辑。开机自启动，纯本地运行，无网络无账号。

## What I already know

- 平台：仅 Windows（用户明确）。
- 技术栈类型：Windows 原生（用户明确，排除 Electron/Tauri 等 web wrapper）。
- 视觉风格：透明 + 紧致感。
- 必须开机启动。
- 仓库现状：`.trellis/spec/{backend,frontend,guides}` 三层均为空，无现有 convention 约束；本任务可自由建立。
- 任务目录：`.trellis/tasks/05-08-rhythm-windows-v1`。

## Assumptions（待你确认前我不会落实）

- 单用户、本地存储，无网络、无云同步、无账户。
- 数据落 `%APPDATA%\Rhythm\state.json`（`System.Text.Json`）。
- 日切按**本地时区 0:00** 触发重置；启动时若发现 `last_reset_date < today`，立即补一次重置。
- v1 只一个清单（不支持多 list / 多 profile）。
- 不需要密码 / 加密。
- 不需要历史 / 统计 / 连续打卡。

## Open Questions（仅 Blocking / Preference）

按问答顺序：

1. ~~技术栈与桌面固定模式~~ ✅ **已定**：WPF + .NET 10 + 模式 C (HWND_BOTTOM + WS_EX_TOOLWINDOW)
2. ~~每日重置触发时机~~ ✅ **已定**：启动时检查 + 后台 0:00 定时器叠加
3. ~~托盘图标 + 关闭行为~~ ✅ **已定**：关闭按钮 = 隐藏到托盘；托盘右键菜单含"显示窗口 / 编辑事项 / 关于 / 退出"
4. ~~窗口位置策略~~ ✅ **已定**：默认右上角偏下；用户可拖动；位置写入 `state.json`，下次启动恢复；分辨率变化导致越界时回退默认。
5. ~~视觉风格~~ ✅ **已定**：暗色单主题；半透明深灰底 (~RGBA(28,28,30,0.65))，浅色文字；Win11 Mica Dark backdrop；字体 = 系统 UI；圆角 ~10px。

## Requirements（随 brainstorm 演化）

### v1 Confirmed

- 一个透明紧致的桌面窗口。
- 显示今日 recurring checklist，每项可勾选 / 取消勾选。
- 跨日自动把所有项重置为未完成（具体触发时机待确认）。
- 用户可编辑 recurring 条目（增 / 删 / 改顺序 / 重命名）。
- 开机启动。
- 本地 JSON 存储，无网络。
- **技术栈**：WPF + .NET 10（C#），单文件 self-contained exe。
- **桌面固定**：模式 C — `WM_WINDOWPOSCHANGING` 强制 `HWND_BOTTOM` + `WS_EX_TOOLWINDOW`，不进 Alt-Tab / 任务栏。
- **每日重置**：启动时检查 + 后台 `DispatcherTimer` 0:00 触发；写回时更新 `last_reset_date = today`。
- **关闭行为**：标题栏关闭按钮 = 隐藏窗口（不退出进程）；托盘右键菜单 = "显示窗口 / 编辑事项 / 关于 / 退出"；仅"退出"真终止进程。
- **窗口位置**：默认右上角偏下；用户可拖动；位置 (x, y, screen_id) 写入 state.json；启动时恢复，越界则回退默认。
- **视觉**：单一主题（暗色：~RGBA(28,28,30,0.65) 深灰底 + 浅色文字）；Win11 启用 Mica Dark backdrop（WPF-UI），Win10 回退纯色半透明；字体 = 系统默认 UI 字体；圆角 ~10px。

### Pending

- 上述 Open Questions 的具体选择。

## Acceptance Criteria（演化）

### 视觉与窗口
- [ ] 窗口在桌面上以暗色半透明背景渲染（Win11 显示 Mica Dark；Win10 显示纯色半透明），不显示标准标题栏；圆角 ~10px。
- [ ] 不进 Alt-Tab、不进任务栏（`WS_EX_TOOLWINDOW` + `ShowInTaskbar=False`）。
- [ ] 窗口默认显示在主屏右上角偏下；用户拖动后位置写入 state.json，下次启动恢复；屏幕分辨率变化导致位置越界时回退默认。

### Checklist 行为
- [ ] 启动后默认显示当天清单，未勾选项与已勾选项视觉可区分（如灰度 + 删除线）。
- [ ] 勾选/取消勾选立即写回 state.json；同一天内重启不丢失。
- [ ] 跨过本地午夜后启动 → `last_reset_date < today` 触发重置 → 全部项变未勾选。
- [ ] App 常驻运行跨过 0:00 → `DispatcherTimer` 触发同一个重置函数；即便定时器漏触发，下次启动也会补偿。

### 编辑事项
- [ ] 提供"编辑事项"入口（托盘菜单 + 主窗口某处按钮）；可新增 / 删除 / 重命名 / 上下移动条目；保存后 state.json 即时持久化。

### 进程生命周期
- [ ] 标题栏关闭按钮 = 隐藏窗口；进程仍然运行；托盘图标常驻。
- [ ] 托盘右键菜单含"显示窗口 / 编辑事项 / 关于 / 退出"；仅"退出"真终止进程。
- [ ] 系统重启后 Rhythm 自动启动（HKCU\Run），不需要管理员权限。

### 卸载干净
- [ ] 删除 exe + 删除 HKCU\Run 表项 + 删除 `%APPDATA%\Rhythm` → 系统无残留。

## Definition of Done

- 实现 + 单元测试覆盖关键状态机（特别是日切重置逻辑、checklist 增删改）。
- Lint / typecheck 通过；Release 配置编译产出单文件 exe。
- README 描述：安装、自启、卸载、数据路径、已知限制。
- 真实 Win11 机器手动验收：启动 / 勾选 / 跨日 / 自启 / 卸载五条路径。

## Out of Scope（v1 明确不做）

- 多设备同步 / 云存储 / 登录账户。
- 提醒 / 通知 / 定时弹窗。
- 多 list / 多 profile / 标签分组。
- 历史完成记录 / 热力图 / 连续打卡统计。
- iOS / Android / macOS / Linux 任意支持。
- 国际化（v1 仅中文 UI）。
- 跟随系统暗 / 浅主题切换（v1 锁定单主题）。
- 全局快捷键唤出。
- 字体 / 圆角 / 透明度等用户可配置项（v1 全部硬编码 sensible defaults）。

## Technical Approach

- **栈**：WPF + .NET 10（C#），SDK-style csproj，`<UseWPF>true</UseWPF>`。
- **窗口**：`AllowsTransparency=True` + `WindowStyle=None` + `ShowInTaskbar=False` + `WS_EX_TOOLWINDOW`；圆角用 Border + CornerRadius；Win11 上 Mica 通过 `DwmExtendFrameIntoClientArea` + Win11 `SystemBackdropType` API（或社区库 WPF-UI）。
- **桌面固定**：模式 C — 在 WndProc 处理 `WM_WINDOWPOSCHANGING`，把 `hwndInsertAfter` 强制为 `HWND_BOTTOM`；初次显示时再调一次 `SetWindowPos(HWND_BOTTOM)`；Win+D "Show Desktop" 触发的 minimize 在 v1 接受，后续若改进再升级。
- **托盘**：H.NotifyIcon NuGet（显示 / 隐藏 / 编辑事项 / 退出 / 关于）。
- **数据**：`System.Text.Json` 写 `%APPDATA%\Rhythm\state.json`；schema 含 `items[]`、`completed_today[]`、`last_reset_date`、`window_pos`、`schema_version`。
- **重置状态机**：纯 C# `RhythmState.RolloverIfNeeded(DateOnly today)` —— 当 `last_reset_date < today` 时清空 `completed_today` 并写回；启动调用一次，`DispatcherTimer` 在午夜调用一次（含轻微 jitter 容错），中间任意时刻调用幂等。
- **自启动**：`Microsoft.Win32.Registry` 写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`；卸载脚本清理。
- **架构**：MVVM（`INotifyPropertyChanged` + `ICommand`）；纯 C# 状态机 `RhythmState` 不依赖 WPF，便于单测。
- **打包**：`dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true`。

## Decision (ADR-lite)

### D1 — 技术栈与桌面固定模式

- **Context**：Windows 原生 + 透明紧致 + 开机启动；UI 量小（一窗口 + checklist + 设置）；开发者熟悉 C#。
- **Decision**：WPF + .NET 10（C#）+ 模式 C 桌面固定（`HWND_BOTTOM` + `WS_EX_TOOLWINDOW`）。
- **Consequences**：
  - 开发周期最短，HWND 控制自由。
  - 单文件 self-contained exe ~70MB；如需缩小可切 framework-dependent。
  - Win+D "Show Desktop" 仍会最小化窗口，v1 接受。后续如要真"贴桌面"可演进到模式 A（reparent 到 WorkerW），HWND 路径不变。
  - 放弃 WinUI 3 原生 Mica 一等公民优势；用 WPF-UI 等社区库达成可接受视觉。
  - **运行时调整**：本机仅装 .NET 10 SDK（无 .NET 8 SDK），实施使用 `net10.0-windows` TargetFramework；功能与 .NET 8 等价，且 .NET 10 是当前 LTS。`research/tech-stack.md` 里的 ".NET 8" 描述视为同义。

### D2 — 每日重置机制

- **Context**：必须保证用户跨日看到"未完成"清单；App 可能 24h 常驻或频繁重启，两种场景都得对。
- **Decision**：启动时检查 `last_reset_date < today` 即重置 + 后台 `DispatcherTimer` 在本地 0:00 触发同一个 `RolloverIfNeeded` 函数，叠加。
- **Consequences**：
  - 函数幂等，重复调用安全。
  - 即使 App 在午夜被睡眠 / 杀进程，下次启动也会补偿。
  - 时区或系统时间被手动改动时，行为以"本地 today"为准；不处理夏令时跳过 0:00 的极端情况（中国大陆无 DST，可忽略）。

### D3 — UI 与交互

- **Context**：视觉风格"透明紧致"+ 单用户工具属性 → 不需要可配置项膨胀。
- **Decision**：
  - 暗色单主题（半透明深灰）+ Win11 Mica（WPF-UI），Win10 回退纯色半透明。
  - 字体 / 圆角 / 透明度全硬编码 sensible defaults，v1 不暴露给用户。
  - 关闭按钮 = 隐藏窗口；托盘菜单负责真退出。
  - 用户可拖动窗口，位置记忆；不做全局快捷键、不做主题跟随。
- **Consequences**：
  - 配置面 ≈ 仅 "事项列表"，UI 极简。
  - 后续若用户想要浅色 / 跟随系统主题 / 多主题，需要 v2 加配置项与主题资源字典；v1 的硬编码不留扩展点（YAGNI）。

### D4 — Mica 在 v1 取消

- **Context**：M5 实现阶段发现 WPF `AllowsTransparency=True` 与 DWM Mica backdrop 互斥——Mica 要求 hwnd 由 DWM 渲染（非 per-pixel alpha 窗口），而圆角 + 全窗透明依赖 WPF 自渲染。两者只能取一个。
- **Decision**：v1 保留 `AllowsTransparency=True` + 圆角 + RGBA(28,28,30, 0.78) 半透明深灰，**跳过 Mica**。Win10/Win11 视觉一致。
- **Consequences**：
  - 视觉退化为纯软件半透明，没有 Win11 系统级风格 blend；但便签场景下肉眼差异不大。
  - 后续若坚持 Mica，需要 v2 重构窗口模型（参照 WPF-UI FluentWindow，放弃 AllowsTransparency 用 hwnd region 裁剪圆角），代价是工程量翻倍。
  - PRD 此前的 "Win11 启用 Mica，Win10 纯色半透明" 表述以本决定为准。

## Implementation Plan (small commits)

里程碑划分（个人项目，按 commit 而非 PR）：

- **M1 — 脚手架 + 状态机**：解决方案与 csproj、`%APPDATA%\Rhythm` 路径常量、`state.json` schema + 序列化、`RhythmState`（含 `RolloverIfNeeded`、`Toggle`、`AddItem` 等）+ 单元测试。
- **M2 — 主窗口 UI**：WPF MainWindow（透明 + WindowStyle=None + 圆角 + ToolWindow）+ checklist binding + 暗色主题资源字典；先用 always-on-top 跑通 UI loop（非 v1 终态）。
- **M3 — 桌面固定 + 拖动 + 位置记忆**：替换为模式 C（WM_WINDOWPOSCHANGING + HWND_BOTTOM）；窗口拖动；位置写回 state.json + 启动恢复 + 越界回退。
- **M4 — 编辑事项 + 托盘**：编辑面板（增 / 删 / 改 / 排序）；H.NotifyIcon 托盘 + 右键菜单；关闭=隐藏到托盘；DispatcherTimer 0:00 触发重置。
- **M5 — Mica + 自启 + 打包 + 验收**：Win11 Mica 接入（WPF-UI 或 P/Invoke）；HKCU\Run 自启实现 + 卸载清理脚本；`PublishSingleFile` 单 exe；README；真机走五条验收路径。

## Research References

- [`research/tech-stack.md`](research/tech-stack.md) — Windows 原生 UI 栈对比；推荐 WPF + .NET 10 + 单文件 exe + HKCU\Run 自启 + HWND_BOTTOM 模式 C。

## Technical Notes

- `.trellis/spec/{backend,frontend,guides}` 当前为空，本任务可自由建立 convention。
- Sub-agent 通道在 brainstorm 期间出现连续 500 panic（Calcium-Ion/new-api 中转），research 由主 agent 基于训练数据（2026-01）写就，未做 fresh web 核验；中转恢复后建议让 `trellis-research` 复核。
