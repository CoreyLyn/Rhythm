# WPF 桌面常驻 App 集成约定

适用：Windows 10 / 11 x64，单进程 WPF + 托盘的常驻型小工具（便签 / 计时器 / 状态指示器）。

## Scenario: 透明 + 桌面固定 + 托盘 + 自启

### 1. Scope / Trigger

触发本 spec 的特征任一项即触发：
- "贴在桌面"的窗口（不抢焦点 / 不进 Alt-Tab / 不进任务栏）
- 透明 + 圆角的"便签"美学
- 启动后常驻托盘
- 开机自启

属于 infra integration（Win32 P/Invoke + DWM + Registry），按 7-section 写。

### 2. Signatures

#### P/Invoke 必备（`net*-windows` TFM 项目）

```csharp
[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

[StructLayout(LayoutKind.Sequential)]
public struct WINDOWPOS
{
    public IntPtr hwnd;
    public IntPtr hwndInsertAfter;
    public int x, y, cx, cy;
    public uint flags;
}
```

#### Win32 常量

| 常量 | 值 | 用途 |
|---|---|---|
| `GWL_EXSTYLE` | -20 | SetWindowLongPtr 索引：扩展样式 |
| `WS_EX_TOOLWINDOW` | 0x00000080 | 工具窗口（不进 Alt-Tab / 任务栏） |
| `HWND_BOTTOM` | 1 (IntPtr) | SetWindowPos hwndInsertAfter：z-order 最底 |
| `WM_WINDOWPOSCHANGING` | 0x0046 | 窗口位置变化前消息（拦截维持 HWND_BOTTOM 必需） |
| `SWP_NOMOVE` | 0x0002 | SetWindowPos 标志：不移动 |
| `SWP_NOSIZE` | 0x0001 | SetWindowPos 标志：不缩放 |
| `SWP_NOACTIVATE` | 0x0010 | SetWindowPos 标志：不激活 |

#### 注册表项（HKCU\Run 自启）

```
路径: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
值名: <AppName>
类型: REG_SZ
值: "C:\path\to\App.exe"  ← 双引号包裹完整路径
```

### 3. Contracts

#### XAML 窗口约定

```xml
<Window
    WindowStyle="None"
    AllowsTransparency="True"
    Background="Transparent"
    ShowInTaskbar="False"
    ResizeMode="NoResize"
    SizeToContent="Height"
    WindowStartupLocation="Manual"
    Title="<AppName>">
  <Border Background="{StaticResource ...}" CornerRadius="10" ...>
    <!-- 内容 -->
  </Border>
</Window>
```

#### Code-Behind 时序

**`SourceInitialized`**（hwnd 已创建，layout 未完）：
- 加 `WS_EX_TOOLWINDOW` 到扩展样式
- 调一次 `SetWindowPos(HWND_BOTTOM, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`
- `HwndSource.AddHook(WndProc)`，在 `WM_WINDOWPOSCHANGING` 中强制 `wp.hwndInsertAfter = HWND_BOTTOM`

**`Loaded`**（layout 已完）：
- 设 `Window.Left` / `Top`（**不要在 SourceInitialized 设**，见 §7）
- 立即 `Persist`（不依赖后续 LocationChanged 的 debounce timer）

#### Application 约定

- `App.xaml`: `ShutdownMode="OnExplicitShutdown"`（避免关闭主窗口时整 app 退出）
- 主窗口关闭按钮 = `Hide()`（托盘 "退出" 才调 `Application.Shutdown()`）

#### Autostart 接口

```csharp
public static void Set(bool enabled)
{
    using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
    if (enabled)
    {
        var exePath = Environment.ProcessPath  // ← 关键：不是 Process.MainModule.FileName
            ?? throw new InvalidOperationException("Environment.ProcessPath is null");
        key!.SetValue(AppName, $"\"{exePath}\"");
    }
    else key!.DeleteValue(AppName, throwOnMissingValue: false);
}
```

### 4. Validation & Error Matrix

| 条件 | 结果 |
|---|---|
| `Environment.ProcessPath == null` | autostart 注册抛 InvalidOperationException（理论上 self-contained exe 不会发生） |
| HKCU\Run 写失败（系统策略 / 权限） | UI 层 catch + MessageBox + 把 menuItem.IsChecked 恢复为 `IsEnabled()` 实际值 |
| 多显示器消失导致 windowPos 越界 | `IsOnScreen(pos)` 返回 false → 回退默认位置 |
| state.json 损坏 / missing | StateStore.Load catch JsonException → 写 stderr → 返回 default 文档 |

### 5. Good / Base / Bad

- **Good**：托盘菜单 "开机启动" 切换 → 立即写 HKCU\Run → 重启 Windows 验证自启
- **Base**：app 启动 → SourceInitialized 装 ToolWindow + HWND_BOTTOM + Hook → Loaded 恢复位置 → 显示
- **Bad**：跳过 `WM_WINDOWPOSCHANGING` 拦截 → 用户切换 app 焦点时 OS 把窗口带回 z-order 中部，模式 C 失效

### 6. Tests Required

| 路径 | 覆盖方式 | 关键 assertion |
|---|---|---|
| 状态机（日切、勾选、增删改移） | xUnit 单测，纯 C# 无 WPF | `RolloverIfNeeded` 幂等；`Toggle/Add/Remove/Rename/Move` 边界；clamp at edges |
| 持久化 round-trip / corrupt / atomic write | xUnit 单测 + 临时文件路径参数 | `Save → Load` 等价；JSON 损坏返回 default；`.tmp` 文件 Save 后不残留 |
| 启动不崩 | Bash smoke test：launch + sleep + tasklist + taskkill | 进程在 sleep 后仍存活 |
| 默认窗口位置 | Bash：rm state.json → launch → cat state.json | `windowPos.x ≈ WorkArea.Width - Width - 30` |
| 越界回退 | Bash：写 windowPos=(-99999, -99999) → launch → cat state.json | `windowPos` 被覆写为默认 |
| 视觉（透明 / 圆角 / Mica / 托盘 / 编辑面板 / 关闭=隐藏 / 自启） | 用户手动验收清单 | 主 agent 不声称已验 |

### 7. Wrong vs Correct

#### Wrong：在 `SourceInitialized` 设 `Window.Left` / `Top`

```csharp
private void OnSourceInitialized(...)
{
    // ToolWindow + HWND_BOTTOM ...
    Left = SystemParameters.WorkArea.Width - Width - 30;
    Top = 80;
}
```

实测 WPF 在 Show() 后窗口仍出现在 OS default cascade 位置（如 49, 49），SourceInitialized 设的值被 layout / show 流程覆盖。

#### Correct：在 `Loaded` 设位置

```csharp
private void OnLoaded(...)
{
    if (vm.WindowPos is { } pos && IsOnScreen(pos)) { Left = pos.X; Top = pos.Y; }
    else { Left = SystemParameters.WorkArea.Width - Width - 30; Top = 80; }
    _isReady = true;
    vm.UpdateWindowPos(...); vm.PersistWindowPos();
}
```

视觉上有一瞬"OS default → jump 到目标"。可接受。彻底无 jump 需要 Show 前隐藏 Window，v2 优化。

---

#### Wrong：用 `Process.MainModule.FileName` 写 HKCU\Run

```csharp
var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
key.SetValue("App", $"\"{exePath}\"");
```

dev 时 `dotnet run` 启动，MainModule.FileName 指向 dotnet host（C:\Program Files\dotnet\dotnet.exe），自启动会拉起 dotnet 而非 app。

#### Correct：`Environment.ProcessPath`

```csharp
var exePath = Environment.ProcessPath ?? throw ...;
key.SetValue("App", $"\"{exePath}\"");
```

返回"启动当前进程的可执行文件路径"。published self-contained exe 中正确指向 App.exe。

---

#### Wrong：尝试 WPF 透明 + DWM Mica

```xml
<Window AllowsTransparency="True" .../>
```
```csharp
DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref DWMSBT_MAINWINDOW, 4);
```

DWM Mica 要求 hwnd 由 DWM 复合（无 per-pixel alpha 旗），WPF `AllowsTransparency=True` 的窗口由 WPF 自渲染（per-pixel alpha）。**两者互斥。Mica 不会生效。**

#### Correct：二选一

- 要圆角 + 全窗透明 → `AllowsTransparency=True` + 软件半透明 RGBA（跨 Win10/11 一致）
- 要真 Mica → `AllowsTransparency=False` + WindowChrome / hwnd region 自绘圆角（参考 WPF-UI FluentWindow，工程量大，多在 v2）

## Convention: 状态机与持久化分层

**What**：状态机（如 `RhythmState`）纯 C# 不依赖 WPF；持久化（如 `StateStore`）独立，atomic write 经 temp + File.Move(overwrite)，失败时回退默认 + log 到 stderr。

**Why**：
- 状态机可单测（无 STA / dispatcher / WPF runtime 依赖）
- 损坏的 JSON 不让 app 起不来
- atomic write 防止半写入文件让下次启动读到不完整 JSON

**Example**：

```csharp
public static void Save(StateDocument doc, string? path = null)
{
    var actualPath = path ?? AppPaths.StateFilePath;
    var tmpPath = actualPath + ".tmp";
    File.WriteAllText(tmpPath, JsonSerializer.Serialize(doc, Options));
    File.Move(tmpPath, actualPath, overwrite: true);
}
```

## Convention: 关闭 = 隐藏 + 托盘真退出

**What**：托盘类常驻 app 的主窗口"关闭按钮"应 `Hide()`，不退出进程；仅托盘菜单 "退出" 触发 `Application.Shutdown()`。

**Why**：用户对便签的预期是"贴桌面常驻"。X 按钮真退出会让用户感觉割裂。

**Example**：

```csharp
// MainWindow.xaml.cs
private void OnCloseClick(...) => Hide();
private void OnClosing(object? sender, CancelEventArgs e)
{
    if (Application.Current is App app && !app.IsShuttingDown)
    {
        e.Cancel = true;
        Hide();
    }
}

// App.xaml.cs
public bool IsShuttingDown { get; private set; }
private void ShutdownApp()
{
    IsShuttingDown = true;
    _trayIcon?.Dispose();
    Shutdown();
}
```

`App.xaml` 必须设 `ShutdownMode="OnExplicitShutdown"`，否则 `Shutdown()` 路径与 OnMainWindowClose 路径相互冲突。

## Convention: 菜单分隔符必须覆盖 MenuItem.SeparatorStyleKey

**What**：右键菜单 / 托盘菜单里的分隔线不要只依赖普通 `Style TargetType="Separator"`；必须同时定义 `x:Key="{x:Static MenuItem.SeparatorStyleKey}"` 的 `Separator` 样式。

**Why**：WPF 菜单内部的 separator 使用菜单专用样式 key。只改普通 `Separator` 样式时，菜单保留左侧图标 gutter，视觉上会出现分隔线左侧缺一段。

**Example**：

```xml
<Style x:Key="{x:Static MenuItem.SeparatorStyleKey}" TargetType="Separator">
    <Setter Property="Height" Value="1"/>
    <Setter Property="Margin" Value="0,4"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Separator">
                <Border Height="1"
                        Background="{StaticResource DividerBrush}"
                        Margin="0"/>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```
