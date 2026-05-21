# Ghost Button No Border + Text Hover Design

**Goal:** 让 Pomodoro 展开层里的 4 个 Ghost 按钮（设置 / 跳过 / 重置 / 清空）去边框；hover 反馈从「按钮变底色」改成「文字变蓝点缀色」。

## Architecture

只动 `src/Rhythm/Themes/Dark.xaml` 中的 `PomodoroGhostButtonStyle`。把它的 `BasedOn` 从 `PomodoroSecondaryButtonStyle` 改回 `PomodoroButtonBaseStyle`，断掉 Secondary 的 background hover/pressed 触发器；加 `BorderThickness="0"` 去边框；加 Style.Triggers 让 hover 时 Foreground 变 AccentBrush。

VM / MainWindow.xaml / 测试 / 其他样式不动。

## Tech Stack

WPF, XAML ResourceDictionary

## File Changes

**Modified:**

- `src/Rhythm/Themes/Dark.xaml` — 仅 `PomodoroGhostButtonStyle` 一个 Style 块

**Untouched:**

- `src/Rhythm/UI/PomodoroViewModel.cs`
- `src/Rhythm/MainWindow.xaml`
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs`
- 所有其他 Style / Brush

## Detailed Spec

### 替换前（当前状态）

```xml
<Style x:Key="PomodoroGhostButtonStyle" BasedOn="{StaticResource PomodoroSecondaryButtonStyle}" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
</Style>
```

### 替换后（目标状态）

```xml
<Style x:Key="PomodoroGhostButtonStyle" BasedOn="{StaticResource PomodoroButtonBaseStyle}" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Foreground" Value="{StaticResource AccentBrush}" />
        </Trigger>
    </Style.Triggers>
</Style>
```

### 关键差异

1. **BasedOn**：`PomodoroSecondaryButtonStyle` → `PomodoroButtonBaseStyle`。Ghost 不再继承 Secondary 的 hover/pressed Background 变化，恢复"无底色"形态。
2. **新增** `BorderThickness="0"`。Base 默认 `BorderThickness="1"`，被这里覆盖为 0，所以无视边框颜色都看不见。
3. **删除** `BorderBrush` setter — 既然不画边框，BorderBrush 多余。
4. **新增** `Style.Triggers` 中的 IsMouseOver trigger，把 Foreground 设为 `AccentBrush`（#0A84FF）。Base 默认 Foreground 是 `ForegroundBrush`（#F2F2F7）。

### 状态映射

| 状态 | Background | Foreground | Border |
|---|---|---|---|
| 默认 | Transparent | #F2F2F7（白） | 0（无） |
| Hover | Transparent | #0A84FF（蓝） | 0（无） |
| Disabled | Transparent | #8E8E93（灰） | 0（无） |
| Pressed | Transparent | #0A84FF（hover 持续） | 0（无） |

Disabled 由 Base ControlTemplate 的 `Trigger IsEnabled=False` 控制（设 Foreground 为 MutedForegroundBrush），保留生效。

### 影响范围

主窗展开层中 4 个 Ghost 按钮全部受影响：设置、跳过、重置、清空。  
`PomodoroPrimaryButtonStyle` 不受影响（开始专注），它独立继承 Base，自己的蓝色 Background/BorderBrush 同色保持视觉不变。  
`PomodoroSecondaryButtonStyle` 不被任何 UI 直接引用（除 Ghost 曾基于它），但保留定义不删，留作他人扩展用。

## Testing

- 不改 VM / 不改测试。现有 66 个测试继续作为回归。
- 手动目测项：
  - 4 个 Ghost 按钮去边框
  - Hover 时背景**不变**、文字**变蓝**
  - 按钮 disabled 时文字仍为灰
  - 开始专注（Primary）外观不变

## Out of Scope

- 不改 Primary 按钮
- 不删 Secondary 样式定义
- 不动其他文件
