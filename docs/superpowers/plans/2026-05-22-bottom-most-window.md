# Bottom-Most Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让主窗口在 z-order 上**永远**位于桌面之上、所有其他应用之下；任何用户操作都不会让它浮到前台。

**Architecture:** 在原有 `WS_EX_TOOLWINDOW + HWND_BOTTOM + WM_WINDOWPOSCHANGING` 三件套基础上叠加三层防线：(1) `WS_EX_NOACTIVATE` 让窗口在系统层面不可激活；(2) `WM_MOUSEACTIVATE` 拦截鼠标激活，返回 `MA_NOACTIVATE`；(3) `WM_WINDOWPOSCHANGED` 兜底，覆盖某些只发 CHANGED 不发 CHANGING 的路径。同时去掉 `ShowMainWindow` 里的 `Activate()` 调用。

**Tech Stack:** WPF, .NET 10 Windows, Win32 interop (`SetWindowPos`, EXSTYLE, WndProc hook) — 与现有实现同栈，不引入新依赖。

---

## File Structure

**Modified files:**

- `src/Rhythm/Interop/Win32.cs` — 新增 4 个 Win32 常量（NOACTIVATE bit、消息号、MA_NOACTIVATE 返回值）
- `src/Rhythm/MainWindow.xaml.cs` — `OnSourceInitialized` 加 NOACTIVATE bit；`WndProc` 扩成 switch 处理 3 个消息；删除 `OnWindowKeyDown` 方法
- `src/Rhythm/MainWindow.xaml` — Window 节点删 `KeyDown="OnWindowKeyDown"` 属性
- `src/Rhythm/App.xaml.cs` — `ShowMainWindow` 删除 `_mainWindow.Activate()` 调用

**Untouched:**

- `src/Rhythm/UI/*` 所有 ViewModel
- `src/Rhythm/State/**` / `Autostart/**`
- `src/Rhythm/Themes/Dark.xaml` 与所有样式资源
- `src/Rhythm/EditItemsWindow*` / `AboutWindow*` / `PomodoroSettingsWindow*`
- `tests/Rhythm.Tests/**`

---

### Task 1: Win32 常量 + EXSTYLE 加 WS_EX_NOACTIVATE

把 4 个新常量加到 interop layer，并立即在 `OnSourceInitialized` 中使用 `WS_EX_NOACTIVATE`。两步一起 commit 是为了避免 dangling 常量（每个 commit 都自包含、可独立验证）。

**Files:**
- Modify: `src/Rhythm/Interop/Win32.cs`
- Modify: `src/Rhythm/MainWindow.xaml.cs`

- [ ] **Step 1: 在 `Interop/Win32.cs` 加 4 个常量**

打开 `src/Rhythm/Interop/Win32.cs`。当前内容（依据最新 commit `c8fc637`）包含：

```csharp
public const int GWL_EXSTYLE = -20;
public const int WS_EX_TOOLWINDOW = 0x00000080;

public static readonly IntPtr HWND_BOTTOM = new(1);

public const int WM_WINDOWPOSCHANGING = 0x0046;

public const uint SWP_NOSIZE = 0x0001;
public const uint SWP_NOMOVE = 0x0002;
public const uint SWP_NOACTIVATE = 0x0010;
```

在 `WS_EX_TOOLWINDOW` 这一行下方加一行 `WS_EX_NOACTIVATE`；在 `WM_WINDOWPOSCHANGING` 下方加 `WM_MOUSEACTIVATE` 和 `WM_WINDOWPOSCHANGED`；在所有 SWP_* 之后加 `MA_NOACTIVATE`。结果应为：

```csharp
public const int GWL_EXSTYLE = -20;
public const int WS_EX_TOOLWINDOW = 0x00000080;
public const int WS_EX_NOACTIVATE = 0x08000000;

public static readonly IntPtr HWND_BOTTOM = new(1);

public const int WM_WINDOWPOSCHANGING = 0x0046;
public const int WM_WINDOWPOSCHANGED = 0x0047;
public const int WM_MOUSEACTIVATE = 0x0021;

public const uint SWP_NOSIZE = 0x0001;
public const uint SWP_NOMOVE = 0x0002;
public const uint SWP_NOACTIVATE = 0x0010;

public const int MA_NOACTIVATE = 3;
```

其余内容（`GetWindowLongPtr`、`SetWindowLongPtr`、`SetWindowPos`、`CreateRoundRectRgn`、`SetWindowRgn`、`DeleteObject`、`WINDOWPOS` struct）保持不动。

如果文件中常量顺序与上方不完全相同（例如 `HWND_BOTTOM` 出现在 `WS_EX_TOOLWINDOW` 之上），按现有顺序就近插入即可，只要 4 个新常量都被定义。如果存在同名常量（不会发生但作为安全闸），停下来报告 BLOCKED 不要猜。

- [ ] **Step 2: 在 `OnSourceInitialized` 中加 `WS_EX_NOACTIVATE` bit**

打开 `src/Rhythm/MainWindow.xaml.cs`。找到方法 `OnSourceInitialized`（约 line 45-57），当前内容：

```csharp
private void OnSourceInitialized(object? sender, EventArgs e)
{
    var hwnd = new WindowInteropHelper(this).Handle;

    var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
    Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex | Win32.WS_EX_TOOLWINDOW));

    Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    var src = HwndSource.FromHwnd(hwnd);
    src?.AddHook(WndProc);
}
```

把 `SetWindowLongPtr` 一行的第三个参数从 `new IntPtr(ex | Win32.WS_EX_TOOLWINDOW)` 改为 `new IntPtr(ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE)`。改完后应为：

```csharp
private void OnSourceInitialized(object? sender, EventArgs e)
{
    var hwnd = new WindowInteropHelper(this).Handle;

    var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
    Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE,
        new IntPtr(ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE));

    Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

    var src = HwndSource.FromHwnd(hwnd);
    src?.AddHook(WndProc);
}
```

WndProc 这次不动（Task 2 处理）。

- [ ] **Step 3: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，0 errors, 0 new warnings。

- [ ] **Step 4: 跑测试套件确认无回归**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed:     0` （目前应为 66 passed，本任务不改测试，数字应保持）

- [ ] **Step 5: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/Interop/Win32.cs src/Rhythm/MainWindow.xaml.cs
git -C D:/Code/Personal/Rhythm commit -m "feat: add WS_EX_NOACTIVATE to main window EXSTYLE"
```

---

### Task 2: 扩展 WndProc 加 WM_WINDOWPOSCHANGED 兜底 + WM_MOUSEACTIVATE 拦截

把 WndProc 从单分支 `if` 重构成 `switch`，并加两个新 case。

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml.cs`

- [ ] **Step 1: 替换 `WndProc` 方法**

打开 `src/Rhythm/MainWindow.xaml.cs`。找到方法 `WndProc`（约 line 106-115），当前内容：

```csharp
private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == Win32.WM_WINDOWPOSCHANGING)
    {
        var wp = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
        wp.hwndInsertAfter = Win32.HWND_BOTTOM;
        Marshal.StructureToPtr(wp, lParam, fDeleteOld: false);
    }
    return IntPtr.Zero;
}
```

整段替换为：

```csharp
private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    switch (msg)
    {
        case Win32.WM_WINDOWPOSCHANGING:
        {
            var wp = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
            wp.hwndInsertAfter = Win32.HWND_BOTTOM;
            Marshal.StructureToPtr(wp, lParam, fDeleteOld: false);
            break;
        }
        case Win32.WM_WINDOWPOSCHANGED:
        {
            Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            break;
        }
        case Win32.WM_MOUSEACTIVATE:
        {
            handled = true;
            return new IntPtr(Win32.MA_NOACTIVATE);
        }
    }
    return IntPtr.Zero;
}
```

三点设计意图（不要写进代码注释）：

- `WM_WINDOWPOSCHANGING` 保持原行为（修改即将到来的位置数据）
- `WM_WINDOWPOSCHANGED` 是兜底层：某些路径只发 CHANGED 不发 CHANGING，这里主动 SetWindowPos 一次，`SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE` 保证只动 z-order
- `WM_MOUSEACTIVATE` 在子控件被点击时由窗口接收，返回 `MA_NOACTIVATE` (3) 表示"激活但不浮起" — 与 `WS_EX_NOACTIVATE` 双保险

- [ ] **Step 2: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，0 errors。

- [ ] **Step 3: 跑测试套件确认无回归**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed:     0`。

- [ ] **Step 4: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/MainWindow.xaml.cs
git -C D:/Code/Personal/Rhythm commit -m "feat: harden WndProc with WM_WINDOWPOSCHANGED and WM_MOUSEACTIVATE"
```

---

### Task 3: 去掉 `ShowMainWindow` 中的 `Activate()`

**Files:**
- Modify: `src/Rhythm/App.xaml.cs`

- [ ] **Step 1: 替换 `ShowMainWindow` 方法**

打开 `src/Rhythm/App.xaml.cs`。找到方法 `ShowMainWindow`（约 line 283-288），当前内容：

```csharp
private void ShowMainWindow()
{
    if (_mainWindow == null) return;
    if (!_mainWindow.IsVisible) _mainWindow.Show();
    _mainWindow.Activate();
}
```

替换为：

```csharp
private void ShowMainWindow()
{
    if (_mainWindow == null) return;
    if (!_mainWindow.IsVisible) _mainWindow.Show();
}
```

意图：双击托盘"显示窗口"时只确保可见，不再调用 `Activate()` 把窗口短暂提到前台。WS_EX_NOACTIVATE + WM_WINDOWPOSCHANGED hook 会保证 Show 后窗口仍沉底。

- [ ] **Step 2: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，0 errors。

- [ ] **Step 3: 跑测试套件确认无回归**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed:     0`。

- [ ] **Step 4: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/App.xaml.cs
git -C D:/Code/Personal/Rhythm commit -m "fix: drop Activate() in ShowMainWindow to keep window bottom-most"
```

---

### Task 4: 删除 Esc KeyDown（已确认接受失效）

`WS_EX_NOACTIVATE` 后主窗口不接收键盘焦点，`OnWindowKeyDown` 永远不会触发。删 XAML 上的事件绑定与 C# 方法。

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`
- Modify: `src/Rhythm/MainWindow.xaml.cs`

- [ ] **Step 1: 删 `MainWindow.xaml` 上的 `KeyDown` 属性**

打开 `src/Rhythm/MainWindow.xaml`。Window 节点（line 1-16）当前是：

```xml
<Window
    x:Class="Rhythm.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:Rhythm"
    Title="Rhythm"
    Width="320"
    MinHeight="80"
    KeyDown="OnWindowKeyDown"
    ResizeMode="NoResize"
    ShowInTaskbar="False"
    SizeToContent="Height"
    SnapsToDevicePixels="True"
    UseLayoutRounding="True"
    WindowStartupLocation="Manual"
    WindowStyle="None">
```

删 `KeyDown="OnWindowKeyDown"` 整行。结果：

```xml
<Window
    x:Class="Rhythm.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:Rhythm"
    Title="Rhythm"
    Width="320"
    MinHeight="80"
    ResizeMode="NoResize"
    ShowInTaskbar="False"
    SizeToContent="Height"
    SnapsToDevicePixels="True"
    UseLayoutRounding="True"
    WindowStartupLocation="Manual"
    WindowStyle="None">
```

- [ ] **Step 2: 删 `MainWindow.xaml.cs` 中的 `OnWindowKeyDown` 方法**

打开 `src/Rhythm/MainWindow.xaml.cs`。找到方法 `OnWindowKeyDown`（约 line 209-216）：

```csharp
private void OnWindowKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape)
    {
        Hide();
        e.Handled = true;
    }
}
```

整段删除（含上下空行调整保持文件干净）。

`using System.Windows.Input;` 保留 — `OnBorderMouseLeftButtonDown` 仍用到 `MouseButton` / `MouseButtonState`。

- [ ] **Step 3: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，0 errors，无新增警告（XAML 现在不再引用 `OnWindowKeyDown`，C# 也删除了方法，应该无悬挂引用）。

- [ ] **Step 4: 跑测试套件确认无回归**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed:     0`。

- [ ] **Step 5: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/MainWindow.xaml src/Rhythm/MainWindow.xaml.cs
git -C D:/Code/Personal/Rhythm commit -m "chore: remove dead Esc KeyDown handler (NOACTIVATE makes it unreachable)"
```

---

### Task 5: 工作区状态检查 + 手动验收

到此自动化改动完成。这一步是控制器（人类用户）操作的手动验收。

**Files:**
- No file changes required

- [ ] **Step 1: 检查工作区干净**

Run: `git -C D:/Code/Personal/Rhythm status --short`

Expected: 空输出。

Run: `git -C D:/Code/Personal/Rhythm log --oneline -6`

Expected: 看到 Task 1-4 的 4 个新 commit（外加之前的 `c8fc637`）。

- [ ] **Step 2: 启动应用**

**SKIP for the subagent runner — 这一步由控制器（人类用户）执行。**

控制器手动执行：

```
dotnet run --project src/Rhythm/Rhythm.csproj -c Debug
```

应用启动后窗口应在屏幕角落可见、半透明、暗色。

- [ ] **Step 3: 走完手动测试矩阵**

**SKIP for the subagent runner — 这一步由控制器（人类用户）执行。**

依次验证下列 10 项：

| # | 场景 | 期望 |
|---|---|---|
| 1 | 启动后 | 窗口可见，z-order 在所有正常应用之下 |
| 2 | 双击托盘"显示窗口" | 窗口可见，**不弹到前台**、不抢焦点 |
| 3 | 点击 checkbox | 切换 IsCompleted，主窗口不浮起 |
| 4 | 点击 Pomodoro 按钮（开始/暂停/跳过/重置/清空/设置） | 行为正常，主窗口不浮起 |
| 5 | 右键打开 ContextMenu | 菜单弹出，主窗口不浮起，菜单项可点 |
| 6 | 拖动主窗口（按 `OnBorderMouseLeftButtonDown` 拖） | 可拖动；主窗口不浮起；松开后位置保存 |
| 7 | 切到其他应用 / Alt-Tab / Win+Tab | 主窗口不出现在 Alt-Tab / Win+Tab 列表；其他应用激活时主窗口仍在底 |
| 8 | 透明度切换（托盘菜单"透明效果"） | 新窗口也沉底（`RecreateMainWindow` 走相同 ctor 应自动继承） |
| 9 | 全屏应用退出 / 屏幕休眠唤醒 | 主窗口仍在底 |
| 10 | Esc 键 | 不再隐藏窗口（已接受） |

附加：编辑事项 / 番茄钟设置 / 关于对话框打开时，仍正常以 modal 弹出在前台（dialog 不受主窗 NOACTIVATE 影响）。

- [ ] **Step 4: 如有任何 #1-9 不符合期望，BLOCKED**

特别注意 #6 拖动。若 `DragMove()` 抛 `InvalidOperationException` 或拖动无反应，进入 Task 6（DragMove 回退实现）。其他失败项一律 BLOCKED 报告，不要尝试就地修复。

- [ ] **Step 5: 全部通过 → 关闭应用，工作完成**

控制器关闭应用进程（托盘 → 退出）。

```bash
git -C D:/Code/Personal/Rhythm status --short
```

Expected: 空输出。

---

### Task 6 (Conditional): DragMove 失效回退

**仅在 Task 5 Step 3 #6 拖动失效时执行。** 如果拖动正常工作，本任务跳过。

WPF 的 `DragMove()` 内部走 `WM_NCLBUTTONDOWN`，理论上不需要窗口处于激活状态，但实测可能与 `WS_EX_NOACTIVATE` 冲突。回退方案：用手动鼠标捕获 + `SetWindowPos` 实现拖动。

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml.cs`

- [ ] **Step 1: 加私有字段**

打开 `src/Rhythm/MainWindow.xaml.cs`。在已有字段附近（约 line 17-19）：

```csharp
private bool _isReady;
private DispatcherTimer? _saveDebounce;
private readonly bool _enableTransparency;
```

加 2 个字段：

```csharp
private bool _isReady;
private DispatcherTimer? _saveDebounce;
private readonly bool _enableTransparency;

private bool _isDragging;
private System.Windows.Point _dragStartScreen;
```

并确保文件顶部已 `using System.Windows;`（应已存在）。

- [ ] **Step 2: 替换 `OnBorderMouseLeftButtonDown` 方法**

当前实现（约 line 218-223）：

```csharp
private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left) return;
    if (e.ButtonState == MouseButtonState.Pressed)
        DragMove();
}
```

整段替换为：

```csharp
private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left) return;
    if (e.ButtonState != MouseButtonState.Pressed) return;
    if (sender is not System.Windows.IInputElement element) return;

    _isDragging = true;
    _dragStartScreen = PointToScreen(e.GetPosition(this));
    element.CaptureMouse();

    RootBorder.MouseMove += OnRootBorderMouseMoveDuringDrag;
    RootBorder.MouseLeftButtonUp += OnRootBorderMouseUpDuringDrag;
    e.Handled = true;
}

private void OnRootBorderMouseMoveDuringDrag(object sender, MouseEventArgs e)
{
    if (!_isDragging) return;

    var current = PointToScreen(e.GetPosition(this));
    var dx = current.X - _dragStartScreen.X;
    var dy = current.Y - _dragStartScreen.Y;
    if (dx == 0 && dy == 0) return;

    Left += dx;
    Top += dy;
    _dragStartScreen = current;
}

private void OnRootBorderMouseUpDuringDrag(object sender, MouseButtonEventArgs e)
{
    if (!_isDragging) return;
    _isDragging = false;
    RootBorder.MouseMove -= OnRootBorderMouseMoveDuringDrag;
    RootBorder.MouseLeftButtonUp -= OnRootBorderMouseUpDuringDrag;
    if (sender is System.Windows.IInputElement element)
        element.ReleaseMouseCapture();
    e.Handled = true;
}
```

意图：

- 不调用 `DragMove()`，改用鼠标捕获跟踪屏幕坐标差，直接改 `Left` / `Top`（这条路径走 `WM_WINDOWPOSCHANGING` 钩子，被强制 BOTTOM，不会浮起）
- 用屏幕坐标差而非窗口内坐标差，避免窗口移动期间窗口内坐标系跟着动导致抖
- 一次拖动只 hook 一次 `MouseMove` / `MouseLeftButtonUp`；松开时解绑

`PointToScreen` 在 `System.Windows.Window` 上是已有方法，`CaptureMouse` / `ReleaseMouseCapture` 在 `IInputElement` 上是已有方法。

- [ ] **Step 3: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，0 errors。

- [ ] **Step 4: 跑测试套件**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed:     0`。

- [ ] **Step 5: 手动验证拖动**

**SKIP for the subagent runner — 控制器执行。**

```
dotnet run --project src/Rhythm/Rhythm.csproj -c Debug
```

- 拖动窗口边框：跟手、无抖动、无 lag
- 拖动期间主窗口不浮起，仍在所有应用之下
- 松开后位置正确（不跳变）
- 重启应用后位置仍是松开时的位置（`PersistWindowPos` 经 `OnLocationOrSizeChanged` 触发 debounce 保存）

- [ ] **Step 6: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/MainWindow.xaml.cs
git -C D:/Code/Personal/Rhythm commit -m "fix: replace DragMove with manual drag to avoid NOACTIVATE conflict"
```

---

## Self-Review

**Spec coverage:**

| Spec 条目 | 任务覆盖 |
|---|---|
| 加 4 个 Win32 常量 | Task 1 Step 1 |
| OnSourceInitialized 加 WS_EX_NOACTIVATE | Task 1 Step 2 |
| WndProc 加 WM_WINDOWPOSCHANGED 兜底 | Task 2 Step 1 |
| WndProc 加 WM_MOUSEACTIVATE 拦截 | Task 2 Step 1 |
| ShowMainWindow 删 Activate | Task 3 Step 1 |
| 删 KeyDown 属性 (XAML) | Task 4 Step 1 |
| 删 OnWindowKeyDown 方法 (cs) | Task 4 Step 2 |
| 手动测试矩阵 (10 项) | Task 5 Step 3 |
| DragMove 失效风险与回退 | Task 6（条件性） |

无缺项。

**Placeholder scan:**

- 无 "TBD" / "TODO" / "implement later"
- 每个代码 step 都有完整代码块
- 每个命令 step 都有 expected output
- Task 6 是条件性任务但写出完整代码，不是 placeholder
- "如有失败则 BLOCKED" 是安全闸，不是 placeholder

**Type consistency:**

- Win32 常量名：`WS_EX_NOACTIVATE` / `WM_MOUSEACTIVATE` / `WM_WINDOWPOSCHANGED` / `MA_NOACTIVATE` — Task 1 定义、Task 2 引用，一致
- `Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE` — 已在 Win32.cs 中存在
- `Win32.HWND_BOTTOM` — 已存在
- `Win32.WINDOWPOS` struct + `hwndInsertAfter` field — 已存在
- `RootBorder` 控件名（Task 6） — `MainWindow.xaml:19` 定义 `x:Name="RootBorder"`，一致
- `PersistWindowPos` / `OnLocationOrSizeChanged` 引用（Task 6 描述） — `MainWindow.xaml.cs:117-127` 已存在

无类型/命名不一致。
