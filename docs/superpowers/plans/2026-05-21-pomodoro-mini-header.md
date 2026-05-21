# Pomodoro Mini Header Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 Pomodoro Strip 从主窗 Row 1 整体迁到 Row 0 右上角，与日期/进度并排显示；展开层留在 Row 1，点击迷你区就地展开。

**Architecture:** 纯 UI/资源层改动，不动 `PomodoroViewModel` 公共表面与状态机。Row 0 由 `StackPanel` 改成两列 `Grid`，新增 3 个迷你区样式；Row 1 删掉 strip ToggleButton 与外层 `StackPanel`，把展开层 `Border` 直接挂在 Row 1。

**Tech Stack:** .NET 10, WPF, xUnit, ResourceDictionary

---

## File Structure

**Modified files:**

- `src/Rhythm/Themes/Dark.xaml` — 新增 `PomodoroMiniContainerStyle`、`PomodoroMiniPhaseStyle`、`PomodoroMiniTimerStyle`
- `src/Rhythm/MainWindow.xaml` — Row 0 拆成两列，右列放迷你 ToggleButton；Row 1 删掉旧 strip 与外层 StackPanel，只保留展开层 Border

**Untouched:**

- `src/Rhythm/UI/PomodoroViewModel.cs` — 不改公共表面
- `src/Rhythm/MainWindow.xaml.cs` — 现有事件 (`OnPomodoroPrimaryActionClick`、`OnPomodoroSettingsClick`、`OnPomodoroSkipClick`、`OnPomodoroResetClick`、`OnPomodoroClearLinkedItemClick`) 全部复用
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs` — 不新增 case，现有 `CompactStripTexts_*` 系列作为回归
- 旧 strip 样式资源 (`PomodoroStripToggleButtonStyle`、`PomodoroStripPhaseStyle`、`PomodoroStripContextStyle`、`PomodoroStripTimerStyle`、`PomodoroStripChevronStyle`) 暂留资源字典

---

### Task 1: 在主题里添加迷你专注样式

**Files:**
- Modify: `src/Rhythm/Themes/Dark.xaml`

- [ ] **Step 1: 在 `PomodoroExpandedPanelStyle` 之后插入 3 个迷你样式**

打开 `src/Rhythm/Themes/Dark.xaml`，找到 `<Style x:Key="PomodoroExpandedPanelStyle" ...>` 这一段。它结束的 `</Style>` 标签后面紧接着是 `<Style x:Key="PomodoroExpandedPhaseStyle" ...>`。在这两个 Style 之间插入：

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
                    Padding="{TemplateBinding Padding}"
                    Background="{TemplateBinding Background}"
                    CornerRadius="8"
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
    <Setter Property="VerticalAlignment" Value="Center" />
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

- [ ] **Step 2: 构建，确认 XAML 解析无误**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，无新增警告或资源解析错误。

- [ ] **Step 3: 提交**

```bash
git add src/Rhythm/Themes/Dark.xaml
git commit -m "style: add pomodoro mini header styles"
```

---

### Task 2: 把 Row 0 拆成两列，右列放迷你专注 ToggleButton

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`

- [ ] **Step 1: 把 Header 的 StackPanel 改成两列 Grid**

打开 `src/Rhythm/MainWindow.xaml`，找到这一段：

```xml
<!--  Header Section  -->
<StackPanel Grid.Row="0" Margin="0,0,0,12">
    <TextBlock Style="{StaticResource HeaderDateStyle}" Text="{Binding CurrentDate}" />
    <TextBlock Style="{StaticResource HeaderStatsStyle}" Text="{Binding ProgressText}" />
</StackPanel>
```

整块替换为：

```xml
<!--  Header Section  -->
<Grid Grid.Row="0" Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>

    <StackPanel Grid.Column="0">
        <TextBlock Style="{StaticResource HeaderDateStyle}" Text="{Binding CurrentDate}" />
        <TextBlock Style="{StaticResource HeaderStatsStyle}" Text="{Binding ProgressText}" />
    </StackPanel>

    <ToggleButton
        Grid.Column="1"
        IsChecked="{Binding Pomodoro.IsExpanded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
        Style="{StaticResource PomodoroMiniContainerStyle}">
        <StackPanel>
            <StackPanel HorizontalAlignment="Right" Orientation="Horizontal">
                <Ellipse
                    Width="8"
                    Height="8"
                    Margin="0,0,6,0"
                    VerticalAlignment="Center">
                    <Ellipse.Style>
                        <Style TargetType="Ellipse">
                            <Setter Property="Fill" Value="{StaticResource PomodoroIdleDotBrush}" />
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding Pomodoro.CompactToneKey}" Value="Focus">
                                    <Setter Property="Fill" Value="{StaticResource PomodoroFocusDotBrush}" />
                                </DataTrigger>
                                <DataTrigger Binding="{Binding Pomodoro.CompactToneKey}" Value="ShortBreak">
                                    <Setter Property="Fill" Value="{StaticResource PomodoroShortBreakDotBrush}" />
                                </DataTrigger>
                                <DataTrigger Binding="{Binding Pomodoro.CompactToneKey}" Value="LongBreak">
                                    <Setter Property="Fill" Value="{StaticResource PomodoroLongBreakDotBrush}" />
                                </DataTrigger>
                                <DataTrigger Binding="{Binding Pomodoro.CompactToneKey}" Value="Paused">
                                    <Setter Property="Fill" Value="{StaticResource PomodoroPausedDotBrush}" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Ellipse.Style>
                </Ellipse>
                <TextBlock Style="{StaticResource PomodoroMiniPhaseStyle}" Text="{Binding Pomodoro.CompactPhaseLabel}" />
            </StackPanel>

            <TextBlock Style="{StaticResource PomodoroMiniTimerStyle}" Text="{Binding Pomodoro.RemainingText}" />
        </StackPanel>
    </ToggleButton>
</Grid>
```

注意：这一步先**不动** Row 1 的旧 strip。中间过渡态会同时显示两个 toggle（顶部新迷你 + 中部老 strip），都绑定 `IsExpanded`，点哪个都能 toggle 展开层。这是有意为之，方便单独验证新迷你区。

- [ ] **Step 2: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 3: 启动应用，目测新迷你区**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

Expected：

- 右上角出现两行迷你区：顶行 `• 准备专注`（灰色点 + 13pt 阶段名），底行 `25:00`（20pt mono Bold，右对齐）
- 鼠标悬停迷你区，背景变浅（hover）
- 点击迷你区，下方展开层出现；迷你区背景变 expanded 色
- 再点击迷你区，展开层收起；迷你区恢复透明背景
- 中部老 strip 仍然显示，与迷你区共用 `IsExpanded` 状态

确认后关闭应用。

- [ ] **Step 4: 提交**

```bash
git add src/Rhythm/MainWindow.xaml
git commit -m "feat: add pomodoro mini toggle to header right slot"
```

---

### Task 3: 移除 Row 1 的旧 strip，把展开层挂到 Row 1 根部

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`

- [ ] **Step 1: 删掉旧 strip 与外层 StackPanel，提升展开层 Border 为 Row 1 根元素**

打开 `src/Rhythm/MainWindow.xaml`，找到这一段：

```xml
<!--  Pomodoro Section  -->
<StackPanel Grid.Row="1" Margin="0,0,0,12">
    <ToggleButton
        IsChecked="{Binding Pomodoro.IsExpanded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
        Style="{StaticResource PomodoroStripToggleButtonStyle}">
        <!-- ... strip 内 Grid ... -->
    </ToggleButton>

    <Border
        Margin="0,8,0,0"
        Style="{StaticResource PomodoroExpandedPanelStyle}"
        Visibility="{Binding Pomodoro.IsExpanded, Converter={StaticResource BoolToVisibilityConverter}}">
        <Grid>
            <!-- ... 展开层三段式 ... -->
        </Grid>
    </Border>
</StackPanel>
```

整块替换为（**保留**展开层 Border 内部的 Grid 完全不变）：

```xml
<!--  Pomodoro Expanded Panel  -->
<Border
    Grid.Row="1"
    Margin="0,0,0,12"
    Style="{StaticResource PomodoroExpandedPanelStyle}"
    Visibility="{Binding Pomodoro.IsExpanded, Converter={StaticResource BoolToVisibilityConverter}}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <StackPanel>
                <TextBlock Style="{StaticResource PomodoroExpandedPhaseStyle}" Text="{Binding Pomodoro.PhaseLabel}" />
                <TextBlock Style="{StaticResource PomodoroExpandedTimerStyle}" Text="{Binding Pomodoro.RemainingText}" />
                <TextBlock Style="{StaticResource PomodoroMetaStyle}" Text="{Binding Pomodoro.CycleText}" />
            </StackPanel>

            <Button
                Grid.Column="1"
                Click="OnPomodoroSettingsClick"
                Content="设置"
                Style="{StaticResource PomodoroGhostButtonStyle}" />
        </Grid>

        <Grid Grid.Row="1" Margin="0,12,0,0">
            <ComboBox
                DisplayMemberPath="Text"
                IsEnabled="{Binding Pomodoro.IsIdle}"
                ItemsSource="{Binding Pomodoro.AvailableItems}"
                SelectedValue="{Binding Pomodoro.SelectedLinkedItemId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                SelectedValuePath="Id"
                Style="{StaticResource PomodoroComboBoxStyle}"
                Visibility="{Binding Pomodoro.IsIdle, Converter={StaticResource BoolToVisibilityConverter}}" />

            <TextBlock
                Style="{StaticResource PomodoroMetaStyle}"
                Text="{Binding Pomodoro.LinkedItemText}"
                Visibility="{Binding Pomodoro.IsIdle, Converter={StaticResource InverseBoolToVisibilityConverter}}" />
        </Grid>

        <Grid Grid.Row="2" Margin="0,14,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <Button
                Grid.Column="0"
                Click="OnPomodoroPrimaryActionClick"
                Content="{Binding Pomodoro.PrimaryActionText}"
                IsEnabled="{Binding Pomodoro.CanExecutePrimaryAction}"
                Style="{StaticResource PomodoroPrimaryButtonStyle}" />

            <Button
                Grid.Column="1"
                Margin="8,0,0,0"
                Click="OnPomodoroSkipClick"
                Content="跳过"
                IsEnabled="{Binding Pomodoro.CanSkip}"
                Style="{StaticResource PomodoroGhostButtonStyle}" />

            <Button
                Grid.Column="2"
                Margin="8,0,0,0"
                Click="OnPomodoroResetClick"
                Content="重置"
                IsEnabled="{Binding Pomodoro.CanReset}"
                Style="{StaticResource PomodoroGhostButtonStyle}" />

            <Button
                Grid.Column="3"
                Margin="8,0,0,0"
                Click="OnPomodoroClearLinkedItemClick"
                Content="清空"
                IsEnabled="{Binding Pomodoro.IsIdle}"
                Style="{StaticResource PomodoroGhostButtonStyle}"
                Visibility="{Binding Pomodoro.IsIdle, Converter={StaticResource BoolToVisibilityConverter}}" />
        </Grid>
    </Grid>
</Border>
```

关键差异：
- 删掉外层 `<StackPanel Grid.Row="1" Margin="0,0,0,12">`
- 删掉外层 StackPanel 里的整个旧 strip `<ToggleButton ... Style="PomodoroStripToggleButtonStyle">...</ToggleButton>`
- 展开层 `<Border>` 升为 `Grid.Row="1"` 根元素，`Margin` 由 `0,8,0,0` 改为 `0,0,0,12`
- 注释 `<!--  Pomodoro Section  -->` 改为 `<!--  Pomodoro Expanded Panel  -->`

- [ ] **Step 2: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 3: 启动应用，确认布局正确**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

Expected：

- 主窗顶部：左 = 日期 + `已完成 0/2`，右 = `• 准备专注` + `25:00`，二者基线大致对齐
- 中部不再有任何 strip，迷你区下方直接是事项列表（折叠态）
- 点击右上角迷你区 → 展开层在 Row 0 与事项列表之间弹出
- 再点击迷你区 → 展开层收起，事项列表上移回原位
- 展开层内部所有按钮（开始专注/暂停/继续、跳过、重置、清空、设置）可点，行为与改造前一致
- 关联事项 ComboBox 在空闲态可选，运行态只读

确认后关闭应用。

- [ ] **Step 4: 提交**

```bash
git add src/Rhythm/MainWindow.xaml
git commit -m "refactor: remove pomodoro strip from row 1"
```

---

### Task 4: 回归验证

**Files:**
- No file changes required

- [ ] **Step 1: 跑 PomodoroViewModelTests，确认 VM 投影未回归**

Run: `dotnet test tests/Rhythm.Tests/Rhythm.Tests.csproj --filter "FullyQualifiedName~PomodoroViewModelTests" -c Debug`

Expected: `Passed!  - Failed: 0`，所有 `CompactStripTexts_*` 和 `SetExpanded_`、`ExecutePrimaryAction_` 系列通过。

- [ ] **Step 2: 跑整套单测**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 3: 手动遍历五个状态，目测迷你区文案与色点**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

按以下顺序操作并目测：

| 操作 | 期望迷你区 |
|---|---|
| 启动（Idle） | 灰点 + `准备专注`，时间 = 配置默认（默认 25:00） |
| 点击迷你 → 展开 → 选关联事项 → 点 `开始专注` → 收起展开 | 蓝点 + `专注中`，时间倒数 |
| 点击迷你 → 展开 → 点 `暂停` → 收起展开 | 橙点 + `已暂停`，时间停在当前剩余 |
| 点击迷你 → 展开 → 点 `继续` → 点 `跳过` → 收起展开 | 绿点 + `短休息`，时间 = 短休息配置（默认 5:00） |
| 连续 `跳过` 直到进入长休息阶段 → 收起展开 | 青点 + `长休息`，时间 = 长休息配置（默认 15:00） |
| 在 Idle 态点击迷你区展开 → 点 `重置` | 回到 Idle，灰点 + `准备专注` |

确认后关闭应用。

- [ ] **Step 4: 确认事项列表区域净增了高度**

继续在同一次运行里目测：

- 折叠态下：主窗高度比改造前明显减少（不再有常驻 strip 占的那一行）
- 事项较多时（≥ 5 项），可见列表项数 ≥ 改造前
- 展开态下：主窗高度与改造前展开态基本一致

- [ ] **Step 5: 检查工作区干净**

Run: `git status --short`

Expected: 工作区干净，无意外改动。

---

## Self-Review

**Spec coverage:**

- 把 Pomodoro Strip 整体迁到 Row 0 右上角：Task 2
- Row 1 仅保留展开层：Task 3
- 不改 PomodoroViewModel 公共表面：Tasks 1-3 全部仅改 XAML 与 ResourceDictionary，验证步骤包含 Step 1 跑 VM 测试
- 新增 3 个迷你样式：Task 1
- 复用现有 `IsExpanded` toggle 行为：Task 2 Step 1 直接绑 `Pomodoro.IsExpanded`
- 迷你区不显示关联事项名：Task 2 Step 1 的 XAML 仅绑定 `CompactPhaseLabel` 与 `RemainingText`，无 `CompactContextText`
- 状态色点 4 色 DataTrigger：Task 2 Step 1 完整保留 Focus/ShortBreak/LongBreak/Paused 4 个 DataTrigger
- 旧 strip 样式资源暂留：Task 3 只删 XAML 引用，不删 `Dark.xaml` 中的样式键
- 手动遍历 5 个状态：Task 4 Step 3

无缺项。

**Placeholder scan:**

- 无 `TODO`、`TBD`、`implement later`
- 每个代码步骤都给了完整 XAML / 命令
- 每个验证步骤都给了明确命令和预期结果
- Task 3 Step 1 的 XAML 展开层内容是从现有代码完整复制保留，未省略

**Type consistency:**

- 迷你样式键统一命名：`PomodoroMiniContainerStyle`、`PomodoroMiniPhaseStyle`、`PomodoroMiniTimerStyle`（Task 1 定义，Task 2 引用，全一致）
- Binding 路径统一：`Pomodoro.IsExpanded`、`Pomodoro.CompactPhaseLabel`、`Pomodoro.CompactToneKey`、`Pomodoro.RemainingText`
- DataTrigger 值统一：`"Focus"`、`"ShortBreak"`、`"LongBreak"`、`"Paused"`（与 VM 中 `CompactToneKey` 返回值一致）
- 事件处理器命名沿用既有：`OnPomodoroSettingsClick`、`OnPomodoroPrimaryActionClick`、`OnPomodoroSkipClick`、`OnPomodoroResetClick`、`OnPomodoroClearLinkedItemClick`（Task 3 Step 1 复制保留）

命名一致。
