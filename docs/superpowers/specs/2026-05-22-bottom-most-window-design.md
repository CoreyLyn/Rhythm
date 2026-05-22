# Bottom-Most Window Design

**Goal:** 主窗口在 z-order 上**永远**位于桌面之上、所有其他应用之下。任何用户操作（点击、拖动、双击托盘、其他应用激活、Alt-Tab）都不会让它浮到前台。

## Background

`README.md:5` 与现有实现已声明"桌面固定"，使用 `WS_EX_TOOLWINDOW + HWND_BOTTOM + WM_WINDOWPOSCHANGING` 三件套（`MainWindow.xaml.cs:45-57, 106-115`）。实际使用中观察到窗口偶尔会跑到其他应用上面，本设计加固该不变式。

## Architecture

只动四个文件：

- `src/Rhythm/Interop/Win32.cs` — 新增 4 个常量
- `src/Rhythm/MainWindow.xaml.cs` — 扩展 EXSTYLE 与 WndProc；删除 Esc KeyDown 方法
- `src/Rhythm/MainWindow.xaml` — 删除 `KeyDown` 属性
- `src/Rhythm/App.xaml.cs` — `ShowMainWindow` 去掉 `Activate()`

策略：在原有"事后拦截 z-order"基础上叠加"事前不让窗口被激活"。具体三层防线：

1. **EXSTYLE 加 `WS_EX_NOACTIVATE`** — 系统层面声明窗口不可激活
2. **WndProc 拦截 `WM_MOUSEACTIVATE`** — 鼠标点击主窗口或子控件时返回 `MA_NOACTIVATE`，避免点击触发激活
3. **WndProc 加 `WM_WINDOWPOSCHANGED` 兜底** — 某些路径只发 CHANGED 不发 CHANGING（例如 `Show()` 后某些时序），补一次 SetWindowPos to BOTTOM

去 `Activate()` 是关键修复：双击托盘场景目前必然弹起。

## Tech Stack

WPF / .NET 10 / Win32 interop（已使用，本设计不引入新依赖）。

## File Changes

**Modified:**

- `src/Rhythm/Interop/Win32.cs` — 加 4 个常量
- `src/Rhythm/MainWindow.xaml.cs` — `OnSourceInitialized` 改 EXSTYLE；`WndProc` 扩 3 个消息；删 `OnWindowKeyDown` 方法
- `src/Rhythm/MainWindow.xaml` — Window 节点删 `KeyDown="OnWindowKeyDown"`
- `src/Rhythm/App.xaml.cs:ShowMainWindow` — 删 `_mainWindow.Activate()`

**Untouched:**

- `src/Rhythm/UI/MainViewModel.cs` 与所有 ViewModel
- `src/Rhythm/EditItemsWindow*` / `AboutWindow*` / `PomodoroSettingsWindow*` — modal dialog，由 Owner 决定行为
- `src/Rhythm/Themes/Dark.xaml` 与所有样式资源
- `src/Rhythm/State/**` / `Autostart/**`
- 测试

## Detailed Spec

### 1. `Interop/Win32.cs` 新增常量

```csharp
public const int WS_EX_NOACTIVATE = 0x08000000;
public const int WM_MOUSEACTIVATE = 0x0021;
public const int WM_WINDOWPOSCHANGED = 0x0047;
public const int MA_NOACTIVATE = 3;
```

### 2. `MainWindow.xaml.cs:OnSourceInitialized` — 加 NOACTIVATE bit

**当前：**

```csharp
var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex | Win32.WS_EX_TOOLWINDOW));
```

**改为：**

```csharp
var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE,
    new IntPtr(ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE));
```

### 3. `MainWindow.xaml.cs:WndProc` — 扩成 3 个消息

**当前：**

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

**改为：**

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

WM_MOUSEACTIVATE 拦截在 `WS_EX_NOACTIVATE` 已存在时是冗余的双保险，但对子控件点击场景更稳。

### 4. `App.xaml.cs:ShowMainWindow` — 去掉 Activate

**当前：**

```csharp
private void ShowMainWindow()
{
    if (_mainWindow == null) return;
    if (!_mainWindow.IsVisible) _mainWindow.Show();
    _mainWindow.Activate();
}
```

**改为：**

```csharp
private void ShowMainWindow()
{
    if (_mainWindow == null) return;
    if (!_mainWindow.IsVisible) _mainWindow.Show();
    // 不再 Activate()：avoid bringing the window to foreground.
    // WS_EX_NOACTIVATE + WM_WINDOWPOSCHANGED hook 会保证 Show() 后窗口仍沉底。
}
```

### 5. 删除 Esc KeyDown（已确认接受）

`MainWindow.xaml`：

```xml
<Window ... KeyDown="OnWindowKeyDown" ... >
```

删 `KeyDown="OnWindowKeyDown"`。

`MainWindow.xaml.cs`：

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

删整个方法。`WS_EX_NOACTIVATE` 后窗口不接收键盘焦点，该处理器永远不会被触发；保留是死代码。同时 `using System.Windows.Input;` 在删除后是否仍需要（`MouseButton`/`MouseButtonState` 仍在 `OnBorderMouseLeftButtonDown` 中使用），保留。

### DragMove 处理

`OnBorderMouseLeftButtonDown` 中的 `DragMove()` 调用保持不变。实现阶段验证：

- **如果 DragMove 正常工作**（最可能） — 收工
- **如果 DragMove 抛 InvalidOperationException 或拖动无反应** — 回退方案：替换为手动拖动（捕获鼠标、跟踪 mouse move、调 SetWindowPos 移动窗口）。手动拖动实现可参考 WPF 文档的 `WM_NCLBUTTONDOWN` 发送方式

回退方案的具体写法到实现阶段再定，spec 阶段标注为已知风险。

## Testing

### 自动化

不改测试，现有 66 个测试作为回归。

### 手动测试矩阵

| 场景 | 期望 |
|---|---|
| 启动 | 窗口可见，z-order 在所有正常应用之下 |
| 双击托盘"显示窗口" | 窗口可见，**不弹到前台** |
| 点击 checkbox | 切换状态，主窗口不浮起 |
| 点击 Pomodoro 按钮（开始/暂停/跳过/重置/清空/设置） | 行为正常，主窗口不浮起 |
| 右键打开 ContextMenu | 菜单弹出，主窗口不浮起 |
| 拖动主窗口 | 可拖动，主窗口不浮起 |
| 其他应用激活 / Alt-Tab / Win+Tab | 主窗口不上来；不出现在 Alt-Tab 列表 |
| 透明度切换（`RecreateMainWindow`） | 新窗口也沉底 |
| 全屏应用退出 / 屏幕休眠唤醒 | 主窗口仍在底 |
| Esc 键 | 不再隐藏窗口（已接受） |
| 编辑事项 / 番茄钟设置 / 关于 dialog | 仍正常以 modal 形式弹出（dialog 不受 NOACTIVATE 影响，Owner 是主窗口） |

### 不在测试矩阵内

- Win+D / "显示桌面"按钮 — 仍会被桌面遮挡，本设计不解决
- 多显示器 z-order — 现有行为，不动

## Out of Scope

- SetParent 到 Progman/WorkerW（Win+D 也不遮的"桌面壁纸"语义）
- 全局快捷键
- 主题、视觉、布局调整
- 多显示器适配

## Risks

1. **DragMove 可能失效** — 实现阶段验证；失效则回退到手动拖动。失败概率：中。回退成本：低。
2. **ContextMenu 行为** — WPF ContextMenu 是独立 Popup，理论上不受主窗口 NOACTIVATE 影响。失败概率：低。回退方案：用自定义 Popup 替代。
3. **`Activated` 事件依赖** — 代码未使用 `Window.Activated`，无影响。
4. **IME / 焦点 / 键盘输入** — 主窗口无文本输入控件，无影响。
