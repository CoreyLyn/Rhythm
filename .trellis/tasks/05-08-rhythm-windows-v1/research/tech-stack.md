# Rhythm 技术栈选型调研（精简稿）

> **来源说明**：本文档由主 agent 撰写，理由是 brainstorm 期间 `trellis-research` / `general-purpose` sub-agent 在中转 API 上连续 4 次 500 panic。内容基于训练数据（截止 2026-01），**未做 fresh web 核验**。如 sub-agent 通道恢复，建议让 `trellis-research` 重新核验一次（特别是 .NET 8 EOL、WinAppSDK 当前版本细节）。

应用场景：Windows 10/11 桌面便签，透明紧致 UI，常驻桌面，每日 checklist 跨日重置，托盘 + 开机自启，本地 JSON 存储，单用户无网络。

---

## 1. 候选栈对比

| 栈 | 状态 | 透明 / Acrylic / Mica | 桌面固定 | 托盘 + 自启 | 打包 | 运行时 |
|---|---|---|---|---|---|---|
| **WinUI 3 (Windows App SDK) + C#** | 主推（Win11） | Mica/Acrylic 一等公民（`SystemBackdrop`） | 抽象隐藏 HWND，需手动 P/Invoke 才能 reparent | 托盘需第三方；自启 MSIX 原生 | MSIX 推荐，单文件 exe 受限 | 重，依赖 WinAppSDK Bootstrap |
| **WPF + .NET 8 + C#** | 长期支持，社区成熟 | `AllowsTransparency=True` 直接透明；Mica 通过 DwmExtendFrameIntoClientArea + Win11 API（社区库 WPF-UI） | 容易：HWND 暴露，可 reparent 到 WorkerW，或 SetWindowPos HWND_BOTTOM | 托盘：H.NotifyIcon；自启：HKCU\Run 一行注册表 | 单文件 self-contained exe（`PublishSingleFile=true`） | 自包含 ~70MB；framework-dependent ~1MB + .NET 8 Desktop Runtime |
| **WinForms + .NET 8 + C#** | LTS（仅维护，新功能停滞） | `TransparencyKey` 矩形局部透明；Acrylic/Mica 无原生支持 | 同 WPF 容易 | 同 WPF | 同 WPF | 同 WPF |
| **Win32 + DirectComposition + C++** | 永远在 | 完全控制，DirectComposition 实现任意透明 + DWM Mica | 最容易（直接 user32） | 完全手写 | 单文件 exe，几百 KB | 最小（无 .NET runtime） |
| **.NET MAUI on Windows** | 跨平台主推 | 内部即 WinUI 3，多一层抽象 | 同 WinUI 3 但更难下钻 | 跨平台库覆盖不全 | 同 WinUI 3 | 同 WinUI 3 + 抽象层 |

### 一句话定位

- **WinUI 3**：现代视觉最强；但便签"贴桌面"场景与 WinAppSDK 的"应用沙盒"模型摩擦较大。
- **WPF**：便签场景的甜点。视觉够现代（透明 + Mica），HWND 自由，单文件 exe 简单。
- **WinForms**：透明效果差，便签美学不达标。**淘汰**。
- **Win32 C++**：极致小、极致控制；便签这种 UI 量小、需求快迭代的项目开发周期不划算。
- **MAUI on Windows**：单 Windows 平台用它属于自找麻烦。**淘汰**。

---

## 2. 桌面固定 / Pin to Desktop 模式

### 模式 A — Always-on-bottom / 桌面层 reparenting（"真贴桌面"）

实现：SendMessageTimeout 给 Progman 发 `0x052C` 让其触发 WorkerW 创建，EnumWindows 找出 WorkerW，再 `SetParent(myHwnd, workerW)` 把窗口塞到桌面图标层之下、壁纸之上。

- 优点：Win+D "Show Desktop" 不会盖掉；视觉上是真"贴在桌面"。
- 缺点：代码复杂；多显示器、DPI 切换、WorkerW 复活等边界要单独处理。
- 谁这么干：**Rainmeter** 桌面级挂件即用此法。

### 模式 B — Always-on-top（最简）

实现：`Topmost=true`（WPF）或 SetWindowPos HWND_TOPMOST。

- 优点：5 行代码。
- 缺点：会盖在所有窗口之上，全屏游戏/视频会被遮挡。UX 不"便签"。

### 模式 C — Always-on-bottom（不 reparent）

实现：处理 WM_WINDOWPOSCHANGING 把 hwndInsertAfter 强制设为 HWND_BOTTOM；加 WS_EX_TOOLWINDOW 不进 Alt-Tab。

- 优点：比 A 简单数倍。
- 缺点：Win+D 仍会触发 minimize 行为，需要单独处理；不像真"贴桌面"那样在桌面图标之下。

### 主流应用怎么干

- **Microsoft Sticky Notes (UWP)**：模式 B 变体——普通 top-level 窗口，不强制 topmost，用户自己摆。
- **Rainmeter**：可配置，默认含模式 A。
- **Stardock Fences、桌面便签类小工具**：常见模式 A。

---

## 3. 自启动方式

| 方式 | 适用 | 备注 |
|---|---|---|
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | 单文件 exe 最佳 | 用户级，无管理员；登录后 5–10s 内拉起；最简单 |
| Shell:Startup 启动文件夹 .lnk | 也行 | .lnk 易被用户误删，编程化处理不优雅 |
| MSIX `StartupTask` extension | 仅当 MSIX 打包 | Win10 1809+；需要 Package.appxmanifest 声明并由系统授权对话框启用 |

**便签类单文件 exe → 选 HKCU\Run。** 三行代码、零运维。

---

## 4. 推荐组合

### 组合 1：**WPF + .NET 8 + 单文件 exe + HKCU\Run**（推荐）

- 视觉：`AllowsTransparency=True` + `WindowStyle=None` + 圆角 Border；Win11 叠加 Mica（WPF-UI 库或社区代码）。
- HWND：可 reparent 到 WorkerW 走"桌面层"，或先用 HWND_BOTTOM 的 v1。
- 托盘：H.NotifyIcon。
- 数据：System.Text.Json 写 `%APPDATA%\Rhythm\state.json`。
- 自启：写 HKCU\Run。
- 打包：`dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true`。
- **Pros**：开发最快、生态最全、便签美学达标、HWND 控制自由。
- **Cons**：Mica 接入比 WinUI 3 多一步；自包含包体 ~70MB（framework-dependent 可缩到 ~1MB + 用户机器需有 .NET 8 Desktop Runtime）。

### 组合 2：**WinUI 3 + C# + MSIX**

- 视觉：原生 Mica/Acrylic 最美。
- **Pros**：Win11 视觉一等公民。
- **Cons**：MSIX 部署对个人小工具偏重；reparent 到 WorkerW 比 WPF 难；自启需 StartupTask manifest 而非 HKCU\Run。

### 组合 3：**Win32 + DirectComposition + C++**

- **Pros**：单 exe 几百 KB；启动 <100ms。
- **Cons**：写两周 vs WPF 写一晚；后续迭代慢；checklist 这种数据驱动 UI 需要自己造轮子。

---

## 5. 这个 app 的最终推荐

**WPF + .NET 8 + 单文件 self-contained exe + HKCU\Run 自启 + 模式 C (HWND_BOTTOM + WS_EX_TOOLWINDOW) 作为 v1 桌面固定**。

理由：

1. UI 量极小（一个窗口、一个 Checklist、一个设置面板），WPF 一晚能搭出 v1，Mica/透明/圆角一应俱全。
2. WPF 的 HWND 自由度远高于 WinUI 3，"贴桌面"P/Invoke 直接落地；后续若升级到 WorkerW reparenting（模式 A）是同一个 HWND 切路径。
3. 单文件 exe 双击即用，符合"便签"的"轻"心智；HKCU\Run 三行代码完成自启。
4. .NET 8 Desktop Runtime 长期支持到 2026-11，覆盖项目周期。
5. 包体敏感可切 framework-dependent；个人小工具一般不挑剔。

> 如坚持 WinUI 3 视觉一等公民，建议 v1 范围内"reparent 到 WorkerW"放到 v2，先用 always-on-top 起步。

---

## 参考方向（非 fresh check）

- Microsoft Learn — Windows App SDK / WinUI 3 / WPF 文档（learn.microsoft.com）
- WPF-UI（GitHub: lepoco/wpfui）— Mica + Fluent 风格组件库
- H.NotifyIcon（GitHub: HavenDV/H.NotifyIcon）— WPF/WinForms 托盘
- Rainmeter（GitHub: rainmeter/rainmeter）— WorkerW reparenting 参考实现
- Raymond Chen "The Old New Thing" — Win32 窗口分层与 Show Desktop 行为
