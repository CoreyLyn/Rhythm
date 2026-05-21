# Pomodoro Expanded Panel Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修 Pomodoro 展开层 4 个视觉问题：「设置」按钮被拉伸成竖条、底部按钮太扁、圆角与主窗不协调、按钮组间距过紧。

**Architecture:** 两个文件的纯样式调整。`Dark.xaml` 改 `PomodoroButtonBaseStyle`（Padding/MinHeight/CornerRadius）和 `PomodoroGhostButtonStyle`（删冗余 Padding 覆盖）；`MainWindow.xaml` 给「设置」按钮加 `VerticalAlignment="Top"`，把底部按钮组间距 `8` 调到 `10`。VM / 测试 / 事件处理完全不动。

**Tech Stack:** WPF, XAML ResourceDictionary

---

## File Structure

**Modified files:**

- `src/Rhythm/Themes/Dark.xaml` — `PomodoroButtonBaseStyle`（Padding/MinHeight/CornerRadius）+ `PomodoroGhostButtonStyle`（删 Padding 覆盖）
- `src/Rhythm/MainWindow.xaml` — 设置按钮加 VerticalAlignment；底部 3 个 Ghost 按钮 Margin 8 → 10

**Untouched:**

- `src/Rhythm/UI/PomodoroViewModel.cs`
- `src/Rhythm/MainWindow.xaml.cs`
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs`

---

### Task 1: 把按钮基础样式从「扁」改成「厚实」并统一圆角

**Files:**
- Modify: `src/Rhythm/Themes/Dark.xaml`

- [ ] **Step 1: 改 `PomodoroButtonBaseStyle`：Padding/MinHeight/CornerRadius**

打开 `src/Rhythm/Themes/Dark.xaml`，找到 `<Style x:Key="PomodoroButtonBaseStyle" TargetType="Button">` 这一段（当前位于约 218-255 行）。整段替换为：

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

与替换前的差异（注释，不要复制进 XAML）：
- `Padding` 由 `10,7` → `12,9`
- 新增 `<Setter Property="MinHeight" Value="34" />`
- ControlTemplate 内 `Border CornerRadius="7"` → `"8"`
- 其余完全不变

- [ ] **Step 2: 删 `PomodoroGhostButtonStyle` 中冗余的 Padding 覆盖**

在同一文件向下翻，找到 `<Style x:Key="PomodoroGhostButtonStyle" ...>` （当前位于约 285-289 行）。整段替换为：

```xml
<Style x:Key="PomodoroGhostButtonStyle" BasedOn="{StaticResource PomodoroSecondaryButtonStyle}" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
</Style>
```

唯一差异是删除 `<Setter Property="Padding" Value="10,6" />` 那一行。这样 Ghost 按钮会继承 Base 的 `12,9`，与 Primary 同高。

- [ ] **Step 3: 构建，确认 XAML 解析无误**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，无新增警告或资源解析错误。

- [ ] **Step 4: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/Themes/Dark.xaml
git -C D:/Code/Personal/Rhythm commit -m "style: enlarge pomodoro buttons and unify corner radius"
```

---

### Task 2: 修「设置」按钮拉伸 + 调底部按钮组间距

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`

- [ ] **Step 1: 给「设置」按钮加 `VerticalAlignment="Top"`**

打开 `src/Rhythm/MainWindow.xaml`，找到展开层 Row 0 内右列的「设置」Button（当前位于约 122-126 行，紧跟着左侧的 `<StackPanel>` 内三个 TextBlock 之后）。当前样子：

```xml
<Button
    Grid.Column="1"
    Click="OnPomodoroSettingsClick"
    Content="设置"
    Style="{StaticResource PomodoroGhostButtonStyle}" />
```

整段替换为：

```xml
<Button
    Grid.Column="1"
    VerticalAlignment="Top"
    Click="OnPomodoroSettingsClick"
    Content="设置"
    Style="{StaticResource PomodoroGhostButtonStyle}" />
```

唯一差异是新增一行 `VerticalAlignment="Top"`。

效果：按钮不再被左侧 StackPanel（包含 PhaseLabel、大 Timer、CycleText 三行）的高度拉伸；它会自然收成自身按钮内容高度并贴在右上角。

- [ ] **Step 2: 把底部按钮组的水平间距从 8 调到 10**

在同一文件继续向下，找到展开层 Row 2 的按钮组（当前位于约 165-184 行）。该组里有 4 个 Button：开始专注（无 Margin）、跳过、重置、清空（各自 `Margin="8,0,0,0"`）。

把 跳过、重置、清空 这 3 个 Button 的 `Margin="8,0,0,0"` 改为 `Margin="10,0,0,0"`。这一步可以用 Edit 工具的 `replace_all` 完成，但要确认替换前后 `Margin="8,0,0,0"` 在该文件中只出现这 3 处（属于这 3 个按钮）。

确认方法：在编辑前先用 Grep 跑一次确认次数。

Run: `grep -n 'Margin="8,0,0,0"' D:/Code/Personal/Rhythm/src/Rhythm/MainWindow.xaml`

Expected: 恰好输出 3 行，都位于 Row 2 按钮组中。

如果 grep 返回 ≠ 3 行，停下来报告，不要直接 replace_all。

如果 grep 返回 = 3 行，用 Edit 工具 `replace_all` 把 `Margin="8,0,0,0"` 全部替换为 `Margin="10,0,0,0"`。

- [ ] **Step 3: 构建**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 4: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/MainWindow.xaml
git -C D:/Code/Personal/Rhythm commit -m "fix: stop settings button from stretching and widen button gap"
```

---

### Task 3: 回归验证

**Files:**
- No file changes required

- [ ] **Step 1: 跑整套单测**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed: 0`（与 polish 前一致：64 passed）。

- [ ] **Step 2: 检查工作区干净**

Run: `git -C D:/Code/Personal/Rhythm status --short`

Expected: 空输出。

- [ ] **Step 3: 手动目测**

**SKIP for the subagent runner — 这一步由控制器（人类用户）执行。**

控制器手动执行：

```
dotnet run --project src/Rhythm/Rhythm.csproj -c Debug
```

期望目测项：

- 「设置」按钮回到右上角，文字大小自然，不再纵向拉伸
- 底部 4 个按钮（开始专注 / 跳过 / 重置 / 清空）等高，约 34px
- 按钮圆角更柔和（8px），整体与主窗 10px 圆角更协调
- 按钮组水平间距比之前略宽（8 → 10）
- 点击各按钮行为不变（开始专注启动 → 蓝点亮起 / 暂停 / 跳过 / 重置 / 清空 / 设置打开设置弹窗）

---

## Self-Review

**Spec coverage:**

- 设置按钮加 VerticalAlignment="Top" 修拉伸：Task 2 Step 1
- PomodoroButtonBaseStyle Padding 10,7 → 12,9：Task 1 Step 1
- 新增 MinHeight=34：Task 1 Step 1
- 删 PomodoroGhostButtonStyle 的 Padding 10,6 覆盖：Task 1 Step 2
- Border CornerRadius 7 → 8：Task 1 Step 1
- 底部按钮 Margin 8 → 10：Task 2 Step 2
- 不动 VM / 测试 / 事件：Tasks 1-3 全部不触碰这些文件，Task 3 Step 1 跑单测确认无回归

无缺项。

**Placeholder scan:**

- 无 TODO / TBD / implement later
- 每个代码步骤给了完整 XAML 替换
- Task 2 Step 2 的 `replace_all` 前置 grep 验证次数的步骤是必要的安全检查，不是 placeholder

**Type consistency:**

- 引用的样式键名：`PomodoroButtonBaseStyle`、`PomodoroGhostButtonStyle`、`PomodoroSecondaryButtonStyle` — 全部使用 Dark.xaml 已存在的命名
- 引用的画刷键名：`ForegroundBrush`、`MutedForegroundBrush`、`PomodoroStripBorderBrush` — 全部已存在
- 引用的事件处理器：`OnPomodoroSettingsClick` — Task 2 Step 1 保持未变

命名一致。
