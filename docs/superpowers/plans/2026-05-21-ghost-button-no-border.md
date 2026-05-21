# Ghost Button No Border + Text Hover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 4 个 Ghost 按钮（设置 / 跳过 / 重置 / 清空）的边框去掉；hover 反馈从按钮底色改成文字变蓝点缀色。

**Architecture:** 仅替换 `src/Rhythm/Themes/Dark.xaml` 中 `PomodoroGhostButtonStyle` 一个 Style 块。改 BasedOn 从 Secondary 到 Base，加 BorderThickness=0，加 IsMouseOver → Foreground=AccentBrush 触发器，删 BorderBrush setter。不动 VM、不动 MainWindow.xaml、不动测试。

**Tech Stack:** WPF, XAML ResourceDictionary

---

## File Structure

**Modified files:**

- `src/Rhythm/Themes/Dark.xaml` — 仅 `PomodoroGhostButtonStyle` 一个 Style 块替换

**Untouched:**

- `src/Rhythm/UI/PomodoroViewModel.cs`
- `src/Rhythm/MainWindow.xaml`
- `tests/Rhythm.Tests/PomodoroViewModelTests.cs`
- 所有其他 Style 与 Brush 资源

---

### Task 1: 替换 `PomodoroGhostButtonStyle`

**Files:**
- Modify: `src/Rhythm/Themes/Dark.xaml`

- [ ] **Step 1: 把 `PomodoroGhostButtonStyle` 整块替换**

打开 `src/Rhythm/Themes/Dark.xaml`，找到 `PomodoroGhostButtonStyle`（当前内容，依据最新 commit 状态）：

```xml
<Style
    x:Key="PomodoroGhostButtonStyle"
    BasedOn="{StaticResource PomodoroSecondaryButtonStyle}"
    TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />
</Style>
```

整段替换为：

```xml
<Style
    x:Key="PomodoroGhostButtonStyle"
    BasedOn="{StaticResource PomodoroButtonBaseStyle}"
    TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Foreground" Value="{StaticResource AccentBrush}" />
        </Trigger>
    </Style.Triggers>
</Style>
```

注意（不要复制进 XAML）四处差异：
- `BasedOn` 从 `PomodoroSecondaryButtonStyle` 改为 `PomodoroButtonBaseStyle`
- 新增 `<Setter Property="BorderThickness" Value="0" />`
- 删除 `<Setter Property="BorderBrush" Value="{StaticResource PomodoroStripBorderBrush}" />`
- 新增 `Style.Triggers` 块，含一个 IsMouseOver → `Foreground` = `AccentBrush` 触发器

如果文件中 `PomodoroGhostButtonStyle` 出现的格式不完全匹配上面"替换前"的多行属性形式（例如单行 `<Style x:Key="PomodoroGhostButtonStyle" BasedOn="..." TargetType="Button">`），按当前实际行宽匹配，但所有 Setter / 内部内容必须保留替换前的语义（`Background=Transparent`、`BorderBrush=PomodoroStripBorderBrush`）。如果实际内容偏离上面预设以致无法确认匹配，停下来报告 BLOCKED，不要猜。

- [ ] **Step 2: 构建，确认 XAML 解析无误**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`，无新增警告或资源解析错误。

- [ ] **Step 3: 提交**

```bash
git -C D:/Code/Personal/Rhythm add src/Rhythm/Themes/Dark.xaml
git -C D:/Code/Personal/Rhythm commit -m "style: drop border on ghost buttons and use accent text hover"
```

---

### Task 2: 回归验证

**Files:**
- No file changes required

- [ ] **Step 1: 跑整套单测**

Run: `dotnet test Rhythm.slnx -c Release`

Expected: `Passed!  - Failed: 0`（与之前一致：66 passed）。

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

- 4 个 Ghost 按钮（设置、跳过、重置、清空）**无边框**
- 鼠标悬停其中任一 Ghost 按钮，**底色不变**，**文字变蓝**（与「开始专注」主按钮同蓝色系）
- 鼠标移开，文字恢复白
- Ghost 按钮 disabled 状态（例如 Idle 态下「跳过」「重置」「清空」可能被禁用），文字仍为灰
- 「开始专注」(Primary) 外观完全不变

---

## Self-Review

**Spec coverage:**

- BasedOn Secondary → Base：Task 1 Step 1
- BorderThickness=0：Task 1 Step 1
- 删 BorderBrush setter：Task 1 Step 1
- IsMouseOver → AccentBrush 触发器：Task 1 Step 1
- 不改 VM / 测试 / 其他文件：Task 1 + Task 2 没有任何这些文件的改动；Task 2 Step 1 跑全套单测确认无回归
- Disabled 仍为 muted：依赖 Base 的 ControlTemplate IsEnabled=False 触发器（未改动，行为继承）

无缺项。

**Placeholder scan:**

- 无 TODO / TBD
- 每个步骤给了完整 XAML / 命令 / 预期输出
- Task 1 Step 1 包含"格式不匹配则报告 BLOCKED"作为安全闸，不是 placeholder

**Type consistency:**

- 引用的 Style 键：`PomodoroGhostButtonStyle`、`PomodoroButtonBaseStyle` — 文件中已存在，命名一致
- 引用的 Brush 键：`AccentBrush` — Dark.xaml 已有（line 11-13 `SolidColorBrush x:Key="AccentBrush" Color="#0A84FF"`）
- 引用的 Property：`Background`、`BorderThickness`、`Foreground`、`IsMouseOver` — WPF 内建，正确
