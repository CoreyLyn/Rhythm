# Compact Pomodoro Strip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把主窗口中的大面积番茄卡片改成顶部极简状态条，并在点击后就地展开完整控制层。

**Architecture:** 保持现有番茄钟状态机、托盘控制和持久化逻辑不变，只在 `PomodoroViewModel` 增加常驻状态条所需的投影属性与主操作入口。WPF 层把现有常驻大卡片拆成“顶部状态条 + 条下展开层”，并通过主题样式把视觉权重从主卡片降为状态槽。

**Tech Stack:** .NET 10, WPF, xUnit, DispatcherTimer, XAML ResourceDictionary

---

## File Structure

**Modified files:**

- `src/Rhythm/UI/PomodoroViewModel.cs` — 新增状态条文案投影、展开状态、主操作入口，保留现有番茄状态机和持久化流程
- `src/Rhythm/MainWindow.xaml` — 用顶部状态条和按需展开层替换现有常驻大卡片
- `src/Rhythm/MainWindow.xaml.cs` — 新增单一主按钮事件，复用现有设置、跳过、重置和清空事件
- `src/Rhythm/Themes/Dark.xaml` — 新增状态条、展开层、轻量按钮和时间文本样式，弱化旧卡片的视觉权重
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs` — 覆盖状态条文案、主按钮文案和展开状态切换

**No new files required.**

---

### Task 1: 给 `PomodoroViewModel` 增加状态条投影和主操作入口

**Files:**
- Modify: `tests/Rhythm.Tests/PomodoroViewModelTests.cs`
- Modify: `src/Rhythm/UI/PomodoroViewModel.cs`

- [ ] **Step 1: 先写失败测试，锁定状态条文案和主操作文案**

在 `tests/Rhythm.Tests/PomodoroViewModelTests.cs` 的 `Constructor_NormalizesInvalidStatusToIdle` 后面追加：

```csharp
[Fact]
public void CompactStripTexts_ShowIdleSummaryWhenSessionIsIdle()
{
    var context = CreateMainViewModel(
        pomodoroConfig: new PomodoroConfig(FocusMinutes: 30, ShortBreakMinutes: 5, LongBreakMinutes: 15, LongBreakEvery: 4, AutoStartNextPhase: true));
    var pomodoro = context.ViewModel.Pomodoro;

    Assert.Equal("准备专注", pomodoro.CompactPhaseLabel);
    Assert.Equal("可开始 30 分钟", pomodoro.CompactContextText);
    Assert.Equal("开始专注", pomodoro.PrimaryActionText);
    Assert.Equal("Idle", pomodoro.CompactToneKey);
    Assert.False(pomodoro.IsExpanded);
}

[Fact]
public void CompactStripTexts_ShowLinkedItemDuringRunningFocus()
{
    var itemId = Guid.NewGuid();
    var context = CreateMainViewModel(
        items:
        [
            new RhythmItem(itemId, "AI Architect"),
        ],
        pomodoroSession: new PomodoroSessionSnapshot(
            PomodoroStatus.Running,
            PomodoroPhaseType.Focus,
            1452,
            0,
            0,
            itemId,
            DateTimeOffset.Now.AddMinutes(-1),
            DateTimeOffset.Now.AddMinutes(-1)));
    var pomodoro = context.ViewModel.Pomodoro;

    Assert.Equal("专注中", pomodoro.CompactPhaseLabel);
    Assert.Equal("AI Architect", pomodoro.CompactContextText);
    Assert.Equal("暂停", pomodoro.PrimaryActionText);
    Assert.Equal("Focus", pomodoro.CompactToneKey);
}

[Fact]
public void CompactStripTexts_ShowRemainingSummaryWhenPausedWithoutLinkedItem()
{
    var context = CreateMainViewModel(
        pomodoroSession: new PomodoroSessionSnapshot(
            PomodoroStatus.Paused,
            PomodoroPhaseType.Focus,
            751,
            1,
            2,
            null,
            DateTimeOffset.Now.AddMinutes(-10),
            DateTimeOffset.Now.AddMinutes(-4)));
    var pomodoro = context.ViewModel.Pomodoro;

    Assert.Equal("已暂停", pomodoro.CompactPhaseLabel);
    Assert.Equal("剩余 12:31", pomodoro.CompactContextText);
    Assert.Equal("继续", pomodoro.PrimaryActionText);
    Assert.Equal("Paused", pomodoro.CompactToneKey);
}

[Fact]
public void SetExpanded_TogglesUiStateWithoutChangingPomodoroSession()
{
    var context = CreateMainViewModel();
    var pomodoro = context.ViewModel.Pomodoro;
    var originalSession = context.State.PomodoroSession;

    pomodoro.SetExpanded(true);
    Assert.True(pomodoro.IsExpanded);

    pomodoro.SetExpanded(false);
    Assert.False(pomodoro.IsExpanded);
    Assert.Equal(originalSession, context.State.PomodoroSession);
}

[Fact]
public void ExecutePrimaryAction_PausesWhenRunningAndStartsWhenIdle()
{
    var runningContext = CreateMainViewModel(
        pomodoroSession: new PomodoroSessionSnapshot(
            PomodoroStatus.Running,
            PomodoroPhaseType.Focus,
            900,
            0,
            0,
            null,
            DateTimeOffset.Now.AddMinutes(-10),
            DateTimeOffset.Now.AddMinutes(-10)));
    runningContext.ViewModel.Pomodoro.ExecutePrimaryAction();
    Assert.Equal(PomodoroStatus.Paused, runningContext.ViewModel.Pomodoro.Status);

    var idleContext = CreateMainViewModel();
    idleContext.ViewModel.Pomodoro.ExecutePrimaryAction();
    Assert.Equal(PomodoroStatus.Running, idleContext.ViewModel.Pomodoro.Status);
}
```

- [ ] **Step 2: 跑测试，确认这些投影属性和方法当前还不存在**

Run: `dotnet test tests/Rhythm.Tests/Rhythm.Tests.csproj --filter "FullyQualifiedName~PomodoroViewModelTests" -c Debug`

Expected: 编译失败，报错指出 `PomodoroViewModel` 缺少 `CompactPhaseLabel`、`CompactContextText`、`PrimaryActionText`、`CompactToneKey`、`IsExpanded`、`SetExpanded`、`ExecutePrimaryAction`，以及后续主窗绑定要用到的 `CanExecutePrimaryAction`。

- [ ] **Step 3: 在 `PomodoroViewModel` 实现状态条投影和 UI 展开状态**

在 `src/Rhythm/UI/PomodoroViewModel.cs` 中加入以下成员：

```csharp
private bool _isExpanded;

public string CompactPhaseLabel => Status switch
{
    PomodoroStatus.Idle => "准备专注",
    PomodoroStatus.Paused => "已暂停",
    PomodoroStatus.Running => PhaseType switch
    {
        PomodoroPhaseType.Focus => "专注中",
        PomodoroPhaseType.ShortBreak => "短休息",
        PomodoroPhaseType.LongBreak => "长休息",
        _ => "专注中",
    },
    _ => "准备专注",
};

public string CompactContextText => ResolveCompactContextText();

public string CompactToneKey => Status switch
{
    PomodoroStatus.Idle => "Idle",
    PomodoroStatus.Paused => "Paused",
    _ => PhaseType switch
    {
        PomodoroPhaseType.Focus => "Focus",
        PomodoroPhaseType.ShortBreak => "ShortBreak",
        PomodoroPhaseType.LongBreak => "LongBreak",
        _ => "Idle",
    },
};

public string PrimaryActionText => IsRunning ? "暂停" : IsPaused ? "继续" : "开始专注";

public bool CanExecutePrimaryAction => CanPause || CanStartOrResume;

public bool IsExpanded
{
    get => _isExpanded;
    set
    {
        if (_isExpanded == value)
            return;

        _isExpanded = value;
        OnPropertyChanged();
    }
}

public void SetExpanded(bool isExpanded) => IsExpanded = isExpanded;

public void ExecutePrimaryAction()
{
    if (IsRunning)
        Pause();
    else
        StartOrResume();
}
```

并在同一文件底部加入状态条上下文文案解析：

```csharp
private string ResolveCompactContextText()
{
    if (Status == PomodoroStatus.Idle)
        return $"可开始 {Config.FocusMinutes} 分钟";

    if (_machine.Session.LinkedItemId is { } linkedItemId)
    {
        var linkedItem = AvailableItems.FirstOrDefault(candidate => candidate.Id == linkedItemId);
        if (linkedItem is not null)
            return linkedItem.Text;
    }

    if (Status == PomodoroStatus.Paused)
        return $"剩余 {RemainingText}";

    return PhaseType switch
    {
        PomodoroPhaseType.ShortBreak => "上一轮已完成",
        PomodoroPhaseType.LongBreak => $"已完成 {Math.Max(1, Config.LongBreakEvery)} 轮",
        _ => "未关联事项",
    };
}
```

这一步不要改动现有 `PhaseLabel`、`CycleText`、`LinkedItemText` 的语义，展开层仍复用它们。

- [ ] **Step 4: 跑测试确认 VM 新投影通过**

Run: `dotnet test tests/Rhythm.Tests/Rhythm.Tests.csproj --filter "FullyQualifiedName~PomodoroViewModelTests" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 5: 提交**

```bash
git add tests/Rhythm.Tests/PomodoroViewModelTests.cs src/Rhythm/UI/PomodoroViewModel.cs
git commit -m "feat: add compact pomodoro strip view state"
```

---

### Task 2: 把主窗番茄区改成“状态条 + 展开层”

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`
- Modify: `src/Rhythm/MainWindow.xaml.cs`

- [ ] **Step 1: 用状态条替换现有常驻大卡片的顶层结构**

把 `src/Rhythm/MainWindow.xaml` 里当前 `<!--  Pomodoro Section  -->` 的整个 `Border` 替换为：

```xml
<StackPanel Grid.Row="1" Margin="0,0,0,12">
    <ToggleButton
        IsChecked="{Binding Pomodoro.IsExpanded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
        Style="{StaticResource PomodoroStripToggleButtonStyle}">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <Ellipse Width="8" Height="8" Margin="0,0,10,0">
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
                <TextBlock Style="{StaticResource PomodoroStripPhaseStyle}" Text="{Binding Pomodoro.CompactPhaseLabel}" />
            </StackPanel>

            <TextBlock
                Grid.Column="1"
                Margin="12,0,12,0"
                Style="{StaticResource PomodoroStripContextStyle}"
                Text="{Binding Pomodoro.CompactContextText}" />

            <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Style="{StaticResource PomodoroStripTimerStyle}" Text="{Binding Pomodoro.RemainingText}" />
                <TextBlock Margin="10,0,0,0" Style="{StaticResource PomodoroStripChevronStyle}" Text="›" />
            </StackPanel>
        </Grid>
    </ToggleButton>

    <Border
        Margin="0,8,0,0"
        Style="{StaticResource PomodoroExpandedPanelStyle}"
        Visibility="{Binding Pomodoro.IsExpanded, Converter={StaticResource BoolToVisibilityConverter}}">
        <!-- expanded content added in next step -->
    </Border>
</StackPanel>
```

这一步只先完成状态条替换，不要把展开层内容一次性塞进去。

- [ ] **Step 2: 把展开层填成三段结构，并保留现有功能入口**

把上一步 `Border` 内部补成：

```xml
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
```

然后确认 `Paused` 分支下状态条文字仍然可读，不需要再给整条状态槽单独上高饱和背景。

- [ ] **Step 3: 在 code-behind 加一个统一主按钮入口**

在 `src/Rhythm/MainWindow.xaml.cs` 中、现有 `OnPomodoroStartOrResumeClick` 上方插入：

```csharp
private void OnPomodoroPrimaryActionClick(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.Pomodoro.ExecutePrimaryAction();
}
```

保留现有 `OnPomodoroStartOrResumeClick`、`OnPomodoroPauseClick` 等方法不删，先保证托盘和现有手动入口不受影响；这一轮只让主窗改走新的单一主按钮。

- [ ] **Step 4: 构建，确认 XAML 和 code-behind 接线正确**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/MainWindow.xaml src/Rhythm/MainWindow.xaml.cs
git commit -m "feat: replace pomodoro card with compact strip layout"
```

---

### Task 3: 把番茄主题样式从“大卡片”收成“状态槽”

**Files:**
- Modify: `src/Rhythm/Themes/Dark.xaml`

- [ ] **Step 1: 新增状态条和展开层的专用画刷**

在 `src/Rhythm/Themes/Dark.xaml` 的番茄画刷区域后面追加：

```xml
<SolidColorBrush x:Key="PomodoroStripBrush" Color="#22272D" />
<SolidColorBrush x:Key="PomodoroStripBorderBrush" Color="#31404F" />
<SolidColorBrush x:Key="PomodoroStripHoverBrush" Color="#27303A" />
<SolidColorBrush x:Key="PomodoroStripExpandedBrush" Color="#2A3440" />
<SolidColorBrush x:Key="PomodoroChevronBrush" Color="#7E8CA0" />

<SolidColorBrush x:Key="PomodoroIdleDotBrush" Color="#6E8195" />
<SolidColorBrush x:Key="PomodoroFocusDotBrush" Color="#2890FF" />
<SolidColorBrush x:Key="PomodoroShortBreakDotBrush" Color="#49B67A" />
<SolidColorBrush x:Key="PomodoroLongBreakDotBrush" Color="#3DB8C5" />
<SolidColorBrush x:Key="PomodoroPausedDotBrush" Color="#C6965A" />
```

- [ ] **Step 2: 给状态条、时间和轻量按钮增加专用样式**

在 `PomodoroCardStyle` 附近追加：

```xml
<Style x:Key="PomodoroStripToggleButtonStyle" TargetType="ToggleButton">
    <Setter Property="Background" Value="{StaticResource PomodoroStripBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="14,10" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ToggleButton">
                <Border
                    x:Name="StripBorder"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="10"
                    SnapsToDevicePixels="True">
                    <ContentPresenter Margin="{TemplateBinding Padding}" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="StripBorder" Property="Background" Value="{StaticResource PomodoroStripHoverBrush}" />
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="StripBorder" Property="Background" Value="{StaticResource PomodoroStripExpandedBrush}" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="PomodoroStripPhaseStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="17" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="FontFamily" Value="Microsoft YaHei UI, Segoe UI" />
</Style>

<Style x:Key="PomodoroStripContextStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource PomodoroMutedBrush}" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="TextTrimming" Value="CharacterEllipsis" />
</Style>

<Style x:Key="PomodoroStripTimerStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="26" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="FontFamily" Value="Consolas, Microsoft YaHei UI, Segoe UI" />
    <Setter Property="VerticalAlignment" Value="Center" />
</Style>

<Style x:Key="PomodoroStripChevronStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource PomodoroChevronBrush}" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="VerticalAlignment" Value="Center" />
</Style>

<Style x:Key="PomodoroExpandedPanelStyle" BasedOn="{StaticResource PomodoroCardStyle}" TargetType="Border">
    <Setter Property="Padding" Value="14" />
    <Setter Property="Margin" Value="0,8,0,0" />
</Style>

<Style x:Key="PomodoroExpandedPhaseStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource PomodoroMutedBrush}" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="FontWeight" Value="SemiBold" />
</Style>

<Style x:Key="PomodoroExpandedTimerStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="34" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="FontFamily" Value="Consolas, Microsoft YaHei UI, Segoe UI" />
    <Setter Property="Margin" Value="0,8,0,0" />
</Style>

<Style x:Key="PomodoroGhostButtonStyle" BasedOn="{StaticResource PomodoroSecondaryButtonStyle}" TargetType="Button">
    <Setter Property="Padding" Value="10,6" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
</Style>
```

- [ ] **Step 3: 跑构建，确认所有样式键都已补齐**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，且不再报找不到 `PomodoroStripToggleButtonStyle`、`PomodoroExpandedPanelStyle`、`PomodoroGhostButtonStyle` 等资源键。

- [ ] **Step 4: 提交**

```bash
git add src/Rhythm/Themes/Dark.xaml
git commit -m "style: add compact pomodoro strip theme"
```

---

### Task 4: 做一轮回归，确认专注模块不再长期霸占主窗

**Files:**
- No file changes required

- [ ] **Step 1: 跑单测，确认 VM 文案和现有番茄逻辑没回归**

Run: `dotnet test tests/Rhythm.Tests/Rhythm.Tests.csproj --filter "FullyQualifiedName~Pomodoro" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 2: 运行应用，按状态验证顶部状态条**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

Expected: 应用正常启动，番茄区默认只显示一条状态条，不再常驻完整大卡片。

手动检查：

- 空闲态：看到 `准备专注 | 可开始 25 分钟 | 25:00`
- 点击状态条后：展开层出现大号时间、设置按钮、事项区和控制区
- 再点状态条：展开层收起，主窗恢复紧凑
- 开始后：状态条左侧切到 `专注中`，中间切到关联事项或 `未关联事项`
- 暂停后：状态条切到 `已暂停`，中间没有事项时显示 `剩余 mm:ss`
- 短休息与长休息：色点和中间文案分别切到 `上一轮已完成` / `已完成 4 轮`

- [ ] **Step 3: 重点检查展开层里的主次关系**

继续在同一次运行里确认：

- 主按钮只保留一个，文案在 `开始专注` / `暂停` / `继续` 之间切换
- `跳过`、`重置`、`清空` 都退成次级按钮
- `设置` 不再出现在常驻状态条里
- 空闲态可以选择和清空关联事项；运行态默认只显示当前关联摘要
- 下方清单比改造前多出明显可见面积，主窗不会因为番茄区长期展开而显著增高

- [ ] **Step 4: 提交最终验证结论**

```bash
git status --short
```

Expected: 工作区干净，或者只剩你明确决定继续处理的文件；没有意外改动。

---

## Self-Review

**Spec coverage:**

- 顶部只占一小块区域：Task 2, Task 3
- 常驻区不直接操作：Task 2
- 点击后就地展开完整控制层：Task 2
- 三段式状态条信息：Task 1, Task 2
- 中间信息按状态切换：Task 1
- 设置移出常驻区：Task 2
- 空闲态低存在感、运行态强化时间：Task 1, Task 3
- 不改动状态机与持久化：Task 1 明确保留，Task 2-4 只改 UI 层

无缺项。

**Placeholder scan:**

- 没有 `TODO`、`TBD`、`implement later`
- 每个代码步骤都给了具体属性、方法、XAML 结构或样式定义
- 每个验证步骤都给了明确命令和预期结果

**Type consistency:**

- ViewModel 新入口统一命名为 `CompactPhaseLabel`、`CompactContextText`、`CompactToneKey`、`PrimaryActionText`、`CanExecutePrimaryAction`、`IsExpanded`、`ExecutePrimaryAction`
- 主窗统一绑定 `Pomodoro.IsExpanded` 控制展开层
- 主按钮统一调用 `OnPomodoroPrimaryActionClick` -> `Pomodoro.ExecutePrimaryAction()`

命名一致。
