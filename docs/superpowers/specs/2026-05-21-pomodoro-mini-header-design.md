# Pomodoro Mini Header Design

**Goal:** 把当前位于主窗 `Grid.Row=1` 的常驻 Pomodoro Strip 整体迁到 `Grid.Row=0` 的右上角，与日期/进度并排显示。点击迷你区仍就地展开完整控制面板。

## Architecture

不动 Pomodoro 状态机、托盘控制、持久化。`PomodoroViewModel` 不新增公共属性，迷你区复用现有的 `CompactPhaseLabel`、`CompactToneKey`、`RemainingText`、`IsExpanded`、`ExecutePrimaryAction`。

主窗布局调整：
- `Grid.Row=0` 由 `StackPanel` 改为两列 `Grid`：左列保留日期 + 进度，右列放迷你专注 ToggleButton。
- `Grid.Row=1` 原本承载 strip + 展开层，现在只承载展开层；strip 部分整块移除。
- `Grid.Row=2` 事项列表不变。

## Tech Stack

.NET 10, WPF, xUnit, ResourceDictionary。

## File Changes

**Modified:**
- `src/Rhythm/MainWindow.xaml` — Row 0 拆成两列；Row 1 删掉 strip ToggleButton，只保留展开层 Border
- `src/Rhythm/Themes/Dark.xaml` — 新增 3 个样式：`PomodoroMiniContainerStyle`、`PomodoroMiniPhaseStyle`、`PomodoroMiniTimerStyle`
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs` — 不新增 case；保留现有 `CompactStripTexts_*` 测试做回归

**Untouched:**
- `src/Rhythm/UI/PomodoroViewModel.cs` — 不改公共表面
- `src/Rhythm/MainWindow.xaml.cs` — 现有 `OnPomodoroPrimaryActionClick`、`OnPomodoroSettingsClick` 等事件全部沿用
- 旧 strip 样式 (`PomodoroStripToggleButtonStyle`、`PomodoroStripPhaseStyle`、`PomodoroStripContextStyle`、`PomodoroStripTimerStyle`、`PomodoroStripChevronStyle`) 暂留资源字典，不主动删

## Layout Spec

### Row 0 — Header

```
┌──────────────────────────┬─────────────────────┐
│ 5月21日 星期四            │   • 准备专注        │
│ 已完成 0/2                │   25:00             │
└──────────────────────────┴─────────────────────┘
```

`Grid` 两列：
- 左列 `Width="*"`：保留现有 `<TextBlock CurrentDate>` + `<TextBlock ProgressText>` StackPanel
- 右列 `Width="Auto"`：放 ToggleButton（迷你专注），内部是两行 StackPanel：
  - 第一行：水平 StackPanel，色点（8×8 Ellipse 复用现有 4 色 DataTrigger）+ TextBlock 绑 `CompactPhaseLabel`
  - 第二行：TextBlock 绑 `RemainingText`，右对齐

ToggleButton `IsChecked` 绑 `Pomodoro.IsExpanded`（双向）。

### Row 1 — Expanded Panel

完全沿用现有展开层 Border：
- 三段式：阶段名+大倒计时+周期 / 关联事项 ComboBox / 主操作+跳过+重置+清空
- `Visibility` 绑 `IsExpanded` 经 `BoolToVisibilityConverter`
- 把原本包裹 strip + 展开层的外层 `StackPanel Grid.Row="1"` 解掉，直接让展开层 Border 作为 `Grid.Row="1"` 的根元素

### Styles

```xml
<Style x:Key="PomodoroMiniContainerStyle" TargetType="ToggleButton">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="VerticalAlignment" Value="Top" />
    <Setter Property="HorizontalAlignment" Value="Right" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ToggleButton">
                <Border
                    x:Name="MiniBorder"
                    Background="{TemplateBinding Background}"
                    CornerRadius="8"
                    Padding="{TemplateBinding Padding}"
                    SnapsToDevicePixels="True">
                    <ContentPresenter VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="MiniBorder" Property="Background" Value="{StaticResource PomodoroStripHoverBrush}" />
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="MiniBorder" Property="Background" Value="{StaticResource PomodoroStripExpandedBrush}" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="PomodoroMiniPhaseStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="FontFamily" Value="Microsoft YaHei UI, Segoe UI" />
</Style>

<Style x:Key="PomodoroMiniTimerStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="20" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="FontFamily" Value="Consolas, Microsoft YaHei UI, Segoe UI" />
    <Setter Property="HorizontalAlignment" Value="Right" />
    <Setter Property="Margin" Value="0,2,0,0" />
</Style>
```

## State Mapping

| 状态 | 顶行（色点 + 阶段名） | 底行（倒计时） |
|---|---|---|
| Idle | 灰点 + 准备专注 | 25:00（配置默认） |
| Running Focus | 蓝点 + 专注中 | 当前剩余 |
| Running ShortBreak | 绿点 + 短休息 | 当前剩余 |
| Running LongBreak | 青点 + 长休息 | 当前剩余 |
| Paused | 橙点 + 已暂停 | 当前剩余 |

关联事项名在迷你区不显示，仅在展开层 `LinkedItemText` / `AvailableItems` ComboBox 可见。

## Interaction

- 点击迷你区 → `IsExpanded` 翻转 → Row 1 展开层显示/隐藏。
- 已展开状态下迷你区底色保持 `PomodoroStripExpandedBrush`，让用户知道展开开关在哪。
- 展开层内的所有按钮（主操作、跳过、重置、清空、设置）行为不变；DataContext 不动，绑定不变。

## Testing

- 现有 `PomodoroViewModelTests` 的 `CompactStripTexts_*` 系列全部保留，作为回归。
- 不新增 VM 单元测试（无新公共表面）。
- 手动回归（Task 4 列出，含 5 个状态截屏对照）。

## Out of Scope

- 不删除旧 strip 样式资源（PR 体积控制）。
- 不为关联事项名增加 tooltip。
- 不调整主窗整体宽度（沿用 320px）。
- 不改托盘菜单与设置入口。
