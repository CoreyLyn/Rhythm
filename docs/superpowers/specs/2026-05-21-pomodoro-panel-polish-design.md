# Pomodoro Expanded Panel Polish Design

**Goal:** 修复 Pomodoro 展开层的视觉问题：「设置」按钮被纵向拉伸成竖条；底部 4 个按钮高度不足看着扁；圆角与按钮组间距与主窗整体不协调。

## Architecture

纯 UI/样式调整，不动 ViewModel、状态机、事件接线。改两个文件：

- `src/Rhythm/MainWindow.xaml` — 给「设置」按钮加 `VerticalAlignment="Top"` 修拉伸；底部按钮组间距 `Margin="8,0,0,0"` → `Margin="10,0,0,0"`
- `src/Rhythm/Themes/Dark.xaml` — `PomodoroButtonBaseStyle` Padding 加大、加 MinHeight、Border CornerRadius 微调；`PomodoroGhostButtonStyle` Padding 与 Base 对齐

## Tech Stack

WPF, XAML ResourceDictionary

## File Changes

**Modified:**

- `src/Rhythm/MainWindow.xaml`
- `src/Rhythm/Themes/Dark.xaml`

**Untouched:**

- `src/Rhythm/UI/PomodoroViewModel.cs`
- `src/Rhythm/MainWindow.xaml.cs`
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs`

## Detailed Spec

### Change 1 — 修「设置」按钮拉伸

`MainWindow.xaml` 中展开层 Row 0 的「设置」Button：

```xml
<Button
    Grid.Column="1"
    Click="OnPomodoroSettingsClick"
    Content="设置"
    Style="{StaticResource PomodoroGhostButtonStyle}" />
```

改为：

```xml
<Button
    Grid.Column="1"
    VerticalAlignment="Top"
    Click="OnPomodoroSettingsClick"
    Content="设置"
    Style="{StaticResource PomodoroGhostButtonStyle}" />
```

效果：按钮不再被左侧三行 StackPanel（PhaseLabel + 大 Timer + CycleText）的高度拉伸；它会自然收成按钮自身的内容高度并贴在右上角。

### Change 2 — `PomodoroButtonBaseStyle` 加大

`Dark.xaml` 内 `PomodoroButtonBaseStyle`：

- Padding `10,7` → `12,9`
- 新增 `Setter Property="MinHeight" Value="34"`
- 内部 `<ControlTemplate>` 的 `Border CornerRadius="7"` → `CornerRadius="8"`

完整目标样式：

```xml
<Style x:Key="PomodoroButtonBaseStyle" TargetType="Button">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="FontFamily" Value="Microsoft YaHei UI, Segoe UI" />
    <Setter Property="Padding" Value="12,9" />
    <Setter Property="MinHeight" Value="34" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border
                    x:Name="ButtonBorder"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="8">
                    <ContentPresenter
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center"
                        RecognizesAccessKey="True" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="ButtonBorder" Property="Opacity" Value="0.96" />
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="ButtonBorder" Property="Opacity" Value="0.86" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Foreground" Value="{StaticResource MutedForegroundBrush}" />
                        <Setter TargetName="ButtonBorder" Property="Opacity" Value="0.48" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

效果：所有继承 Base 的按钮（Primary、Secondary、Ghost、Compact）一致变高、圆角更柔和。

### Change 3 — `PomodoroGhostButtonStyle` 与 Base 对齐

`Dark.xaml` 内 `PomodoroGhostButtonStyle` 当前显式覆盖 Padding 为 `10,6`，这会盖掉 Base 的 `12,9` 导致 Ghost 比 Primary 矮。删除该覆盖：

```xml
<Style x:Key="PomodoroGhostButtonStyle" BasedOn="{StaticResource PomodoroSecondaryButtonStyle}" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
</Style>
```

（仅删 `<Setter Property="Padding" Value="10,6" />` 那一行）

效果：Ghost 按钮（跳过 / 重置 / 清空 / 设置）与 Primary（开始专注）同高，按钮组对齐。

### Change 4 — 底部按钮组间距

`MainWindow.xaml` 中展开层 Row 2 的三个 Ghost 按钮：

- 跳过 `Margin="8,0,0,0"` → `Margin="10,0,0,0"`
- 重置 `Margin="8,0,0,0"` → `Margin="10,0,0,0"`
- 清空 `Margin="8,0,0,0"` → `Margin="10,0,0,0"`

效果：按钮之间多 2px 呼吸空间，与按钮变高后的视觉比例更协调。

## Testing

- 不改 VM 逻辑、不改测试。现有 `PomodoroViewModelTests` 64 个全部继续作为回归。
- 手动目测：
  - 展开层「设置」回归正常按钮形状，对齐到右上角
  - 底部 4 个按钮高度一致约 34px
  - 圆角 8px，与主窗 10px 接近但不抢眼
  - 按钮组水平间距更宽松

## Out of Scope

- 不改主按钮宽度（已占 `*` 剩余空间）
- 不改 ComboBox、字号、色板
- 不动 `PomodoroCompactButtonStyle`（虽然继承 Base 自然会受 Padding 影响，但它当前未在主窗被引用，影响为零）
- 不删 `Dark.xaml` 中已无引用的旧 strip 样式（另一个 PR）
