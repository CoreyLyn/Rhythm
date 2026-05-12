# EditItemsWindow 行内直接操控 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 `EditItemsWindow` 的所有事项操作从"先选中再到底部编辑"改成"每行直接操控"——拖动把手排序、双击编辑名称、点击徽章切换类型、点击 × 删除；同时去掉下方"选中事项"面板。

**Architecture:** 状态层加 `MoveItemTo(id, newIndex)` 和 `ChangeKind(id, kind)` 两个原子方法（旧的 `MoveItem(delta)` 删除）。`ItemViewModel` 的 `Text` / `Kind` 改成可观察属性，新增 `IsEditing`；`MainViewModel.RenameItem` 改成原地修改而非重建 VM。XAML 重写 ItemTemplate，每行包含 4 列：把手 / 文本（与编辑 TextBox 叠层）/ 类型徽章按钮 / 删除按钮。拖拽用 WPF 内置 `DragDrop.DoDragDrop` + 自定义 `DropLineAdorner` 绘制 drop 提示线。不引入第三方拖拽库。

**Tech Stack:** .NET 10, WPF (net10.0-windows), xUnit, System.Windows.Controls.BooleanToVisibilityConverter, WPF Adorner。

参考设计：`docs/superpowers/specs/2026-05-12-edit-items-inline-design.md`。

---

## File Structure

**Modified files:**

- `src/Rhythm/State/RhythmState.cs` — 加 `MoveItemTo` / `ChangeKind`，删 `MoveItem(delta)`
- `src/Rhythm/UI/ItemViewModel.cs` — `Text` / `Kind` 改为可观察属性，新增 `IsEditing`、`UpdateText` / `UpdateKind`
- `src/Rhythm/UI/MainViewModel.cs` — `RenameItem` 原地化、`MoveItem` 改 `MoveItemTo`、新增 `ChangeKind`
- `src/Rhythm/App.xaml` — 注册 `BooleanToVisibilityConverter`
- `src/Rhythm/EditItemsWindow.xaml` — 删除底部"选中事项"面板；改 grid 行数；重写 `ItemTemplate`；ListBox 启用 drop
- `src/Rhythm/EditItemsWindow.xaml.cs` — 删除选中相关 handler、引入拖拽 / 编辑 / 切换 kind / 删除 handler
- `tests/Rhythm.Tests/RhythmStateTests.cs` — 新增 `MoveItemTo` / `ChangeKind` 测试，删除 `MoveItem_*` 测试

**Created files:**

- `src/Rhythm/UI/DropLineAdorner.cs` — Adorner 类，在 ListBox 上画一条 2px 强调色横线指示 drop 位置

---

### Task 1: 状态层添加 `RhythmState.MoveItemTo`（TDD）

**Files:**
- Modify: `tests/Rhythm.Tests/RhythmStateTests.cs`（在文件末尾追加测试）
- Modify: `src/Rhythm/State/RhythmState.cs`（在 `MoveItem` 方法下面新增）

- [ ] **Step 1: 写失败测试**

在 `tests/Rhythm.Tests/RhythmStateTests.cs` 的 `ToDocument_RoundtripsMutations` 测试之前插入：

```csharp
[Fact]
public void MoveItemTo_MovesItemToSpecifiedIndex()
{
    var state = NewState();
    var a = state.AddItem("A");
    var b = state.AddItem("B");
    var c = state.AddItem("C");

    state.MoveItemTo(c.Id, 0);
    Assert.Equal(new[] { c.Id, a.Id, b.Id }, state.Items.Select(i => i.Id).ToArray());

    state.MoveItemTo(c.Id, 2);
    Assert.Equal(new[] { a.Id, b.Id, c.Id }, state.Items.Select(i => i.Id).ToArray());
}

[Fact]
public void MoveItemTo_NoOpWhenIndexEqualsCurrent()
{
    var state = NewState();
    var a = state.AddItem("A");
    var b = state.AddItem("B");

    state.MoveItemTo(a.Id, 0);
    Assert.Equal(new[] { a.Id, b.Id }, state.Items.Select(i => i.Id).ToArray());
}

[Fact]
public void MoveItemTo_ThrowsForOutOfRange()
{
    var state = NewState();
    var a = state.AddItem("A");
    state.AddItem("B");
    Assert.Throws<ArgumentException>(() => state.MoveItemTo(a.Id, -1));
    Assert.Throws<ArgumentException>(() => state.MoveItemTo(a.Id, 2));
}

[Fact]
public void MoveItemTo_ThrowsForUnknownId()
{
    var state = NewState();
    state.AddItem("A");
    Assert.Throws<ArgumentException>(() => state.MoveItemTo(Guid.NewGuid(), 0));
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~MoveItemTo" -c Debug`
Expected: 编译失败 with "'RhythmState' does not contain a definition for 'MoveItemTo'"

- [ ] **Step 3: 实现 `MoveItemTo`**

在 `src/Rhythm/State/RhythmState.cs` 的 `MoveItem` 方法**下面**新增：

```csharp
public void MoveItemTo(Guid id, int newIndex)
{
    var idx = _items.FindIndex(i => i.Id == id);
    if (idx < 0)
        throw new ArgumentException($"Unknown item id: {id}", nameof(id));
    if (newIndex < 0 || newIndex >= _items.Count)
        throw new ArgumentException($"Index out of range: {newIndex}", nameof(newIndex));
    if (newIndex == idx)
        return;
    var item = _items[idx];
    _items.RemoveAt(idx);
    _items.Insert(newIndex, item);
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~MoveItemTo" -c Debug`
Expected: 4 passed, 0 failed

- [ ] **Step 5: 跑全部测试确保没回归**

Run: `dotnet test Rhythm.slnx -c Debug`
Expected: 全部通过

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/State/RhythmState.cs tests/Rhythm.Tests/RhythmStateTests.cs
git commit -m "feat: Add RhythmState.MoveItemTo for arbitrary reordering"
```

---

### Task 2: 状态层添加 `RhythmState.ChangeKind`（TDD）

**Files:**
- Modify: `tests/Rhythm.Tests/RhythmStateTests.cs`
- Modify: `src/Rhythm/State/RhythmState.cs`

- [ ] **Step 1: 写失败测试**

在 `tests/Rhythm.Tests/RhythmStateTests.cs` 的 `ToDocument_RoundtripsMutations` 测试之前追加：

```csharp
[Fact]
public void ChangeKind_UpdatesKindWithoutTouchingCompletion()
{
    var state = NewState();
    var a = state.AddItem("A", RhythmItemKind.Daily);
    state.ToggleItem(a.Id);

    state.ChangeKind(a.Id, RhythmItemKind.OneTime);

    var updated = state.Items.Single(i => i.Id == a.Id);
    Assert.Equal(RhythmItemKind.OneTime, updated.Kind);
    Assert.Contains(a.Id, state.CompletedToday);
}

[Fact]
public void ChangeKind_ThrowsForUnknownId()
{
    var state = NewState();
    Assert.Throws<ArgumentException>(() =>
        state.ChangeKind(Guid.NewGuid(), RhythmItemKind.Daily));
}

[Fact]
public void ChangeKind_RejectsUnknownKind()
{
    var state = NewState();
    var a = state.AddItem("A");
    Assert.Throws<ArgumentException>(() =>
        state.ChangeKind(a.Id, (RhythmItemKind)999));
}

[Fact]
public void ChangeKind_FollowedByRolloverRemovesIfCompleted()
{
    var state = NewState();
    var a = state.AddItem("A", RhythmItemKind.Daily);
    state.ToggleItem(a.Id);
    state.ChangeKind(a.Id, RhythmItemKind.OneTime);

    state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

    Assert.DoesNotContain(state.Items, i => i.Id == a.Id);
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~ChangeKind" -c Debug`
Expected: 编译失败 with "'RhythmState' does not contain a definition for 'ChangeKind'"

- [ ] **Step 3: 实现 `ChangeKind`**

在 `src/Rhythm/State/RhythmState.cs` 的 `MoveItemTo` 方法**下面**新增：

```csharp
public void ChangeKind(Guid id, RhythmItemKind newKind)
{
    if (!Enum.IsDefined(newKind))
        throw new ArgumentException($"Unknown item kind: {newKind}", nameof(newKind));
    var idx = _items.FindIndex(i => i.Id == id);
    if (idx < 0)
        throw new ArgumentException($"Unknown item id: {id}", nameof(id));
    _items[idx] = _items[idx] with { Kind = newKind };
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~ChangeKind" -c Debug`
Expected: 4 passed

- [ ] **Step 5: 跑全部测试**

Run: `dotnet test Rhythm.slnx -c Debug`
Expected: 全部通过

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/State/RhythmState.cs tests/Rhythm.Tests/RhythmStateTests.cs
git commit -m "feat: Add RhythmState.ChangeKind for in-place kind toggle"
```

---

### Task 3: 删除旧的 `RhythmState.MoveItem(delta)` 及其测试

**Files:**
- Modify: `src/Rhythm/State/RhythmState.cs`
- Modify: `tests/Rhythm.Tests/RhythmStateTests.cs`

- [ ] **Step 1: 检查所有调用点**

Run: `git grep -n "\\.MoveItem(" -- src/Rhythm tests`
Expected: 只剩 `MainViewModel.cs:105 _service.State.MoveItem(...)` 在主代码中调用，测试里的 `MoveItem_UpAndDownReorders` 和 `MoveItem_ClampsAtEdges` 在测试中调用。

注意：`MainViewModel.MoveItem` 在 Task 5 才会移除。这个 Task 只删除 `RhythmState.MoveItem(delta)`，会让 `MainViewModel` 编译失败——所以这里要**先**把 `MainViewModel._service.State.MoveItem(...)` 那一行也删掉，否则 build 不过。

- [ ] **Step 2: 删除 `RhythmState.MoveItem`**

在 `src/Rhythm/State/RhythmState.cs` 删除整个 `public void MoveItem(Guid id, int delta) { ... }` 方法。

- [ ] **Step 3: 暂时删掉 `MainViewModel.MoveItem` 里对旧 state 方法的调用**

在 `src/Rhythm/UI/MainViewModel.cs` 找到 `public void MoveItem(ItemViewModel itemVm, int delta)`，**整个方法删除**（在 Task 5 会重新加一个 `MoveItemTo`）。同时检查 `EditItemsWindow.xaml.cs:83-99` 的 `OnMoveUp`/`OnMoveDown` 引用 `_vm.MoveItem`——这两个 handler 在 Task 5 删除整个底部面板时一并处理，但为了让本 Task 之后能 build，先把它们改成**编译占位**：

`src/Rhythm/EditItemsWindow.xaml.cs` 的 `OnMoveUp` 改成：

```csharp
private void OnMoveUp(object sender, RoutedEventArgs e)
{
    // Replaced in Task 5
}
```

`OnMoveDown` 改成：

```csharp
private void OnMoveDown(object sender, RoutedEventArgs e)
{
    // Replaced in Task 5
}
```

- [ ] **Step 4: 删除旧的两个测试**

在 `tests/Rhythm.Tests/RhythmStateTests.cs` 删除整段 `MoveItem_UpAndDownReorders` 和整段 `MoveItem_ClampsAtEdges`。

- [ ] **Step 5: 跑全部测试与构建**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet test Rhythm.slnx -c Debug`
Expected: 构建成功；所有剩余测试通过。

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/State/RhythmState.cs src/Rhythm/UI/MainViewModel.cs src/Rhythm/EditItemsWindow.xaml.cs tests/Rhythm.Tests/RhythmStateTests.cs
git commit -m "refactor: Remove RhythmState.MoveItem(delta) in favor of MoveItemTo"
```

---

### Task 4: `ItemViewModel` 改为可观察的 Text/Kind + IsEditing

**Files:**
- Modify: `src/Rhythm/UI/ItemViewModel.cs`

- [ ] **Step 1: 替换 `ItemViewModel` 的字段和属性**

把 `src/Rhythm/UI/ItemViewModel.cs` 的内容**整体替换**为：

```csharp
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class ItemViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;
    private string _text;
    private RhythmItemKind _kind;
    private bool _isCompleted;
    private bool _isEditing;

    public Guid Id { get; }

    public string Text
    {
        get => _text;
        private set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public RhythmItemKind Kind
    {
        get => _kind;
        private set
        {
            if (_kind == value) return;
            _kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KindLabel));
        }
    }

    public string KindLabel => Kind == RhythmItemKind.OneTime ? "一次性" : "每日";

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value) return;
            _isCompleted = value;
            _service.State.ToggleItem(Id);
            _service.Persist();
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
        }
    }

    public ItemViewModel(RhythmStateService service, RhythmItem item)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(item);
        _service = service;
        Id = item.Id;
        _text = item.Text;
        _kind = item.Kind;
        _isCompleted = service.State.CompletedToday.Contains(item.Id);
    }

    internal void UpdateText(string text) => Text = text;
    internal void UpdateKind(RhythmItemKind kind) => Kind = kind;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: 构建并跑测试**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet test Rhythm.slnx -c Debug`
Expected: 构建成功；测试全部通过（state 测试不直接依赖 VM，但 VM 编译失败会影响整个 solution 构建）。

- [ ] **Step 3: 提交**

```bash
git add src/Rhythm/UI/ItemViewModel.cs
git commit -m "refactor: Make ItemViewModel Text/Kind observable, add IsEditing"
```

---

### Task 5: 重写 `MainViewModel` 三个方法 + 删除底部"选中事项"面板

这个 Task 改动较大但绑在一起做才能保证 commit 后整窗口仍能正确打开、新增事项仍能工作。

**Files:**
- Modify: `src/Rhythm/UI/MainViewModel.cs`
- Modify: `src/Rhythm/EditItemsWindow.xaml`
- Modify: `src/Rhythm/EditItemsWindow.xaml.cs`

- [ ] **Step 1: 改 `MainViewModel.RenameItem` 为原地更新；删除占位 `MoveItem`；新增 `MoveItemTo` 和 `ChangeKind`**

把 `src/Rhythm/UI/MainViewModel.cs` 中：

旧 `RenameItem`：

```csharp
public void RenameItem(ItemViewModel itemVm, string newText)
{
    var idx = Items.IndexOf(itemVm);
    if (idx < 0) return;
    _service.State.RenameItem(itemVm.Id, newText);
    var renamed = _service.State.Items[idx];
    itemVm.PropertyChanged -= OnItemPropertyChanged;
    var newVm = new ItemViewModel(_service, renamed);
    newVm.PropertyChanged += OnItemPropertyChanged;
    Items[idx] = newVm;
    _service.Persist();
}
```

替换为：

```csharp
public void RenameItem(ItemViewModel itemVm, string newText)
{
    _service.State.RenameItem(itemVm.Id, newText);
    var renamed = _service.State.Items.Single(i => i.Id == itemVm.Id);
    itemVm.UpdateText(renamed.Text);
    _service.Persist();
}
```

在 `RenameItem` 下面新增：

```csharp
public void MoveItemTo(ItemViewModel itemVm, int newIndex)
{
    var idx = Items.IndexOf(itemVm);
    if (idx < 0) return;
    if (newIndex < 0 || newIndex >= Items.Count) return;
    if (newIndex == idx) return;
    _service.State.MoveItemTo(itemVm.Id, newIndex);
    Items.Move(idx, newIndex);
    _service.Persist();
}

public void ChangeKind(ItemViewModel itemVm, RhythmItemKind newKind)
{
    _service.State.ChangeKind(itemVm.Id, newKind);
    itemVm.UpdateKind(newKind);
    _service.Persist();
}
```

- [ ] **Step 2: 删除底部"选中事项"面板的 XAML**

在 `src/Rhythm/EditItemsWindow.xaml`：

**改 `Grid.RowDefinitions`**——把这一段：

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

改成：

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

**删除整个 `<Border Grid.Row="2" ...>`**（行号约 317–391，从 "选中事项" 那段开始，到 `</Border>` 结束的整个 Border）。

**修改最底部新增事项的 Grid.Row**——把：

```xml
<Grid Grid.Row="3" Margin="0,12,0,0">
```

改成：

```xml
<Grid Grid.Row="2" Margin="0,12,0,0">
```

**移除 ListBox 的 SelectionChanged 引用**——把：

```xml
<ListBox
    x:Name="ItemsList"
    SelectionChanged="OnSelectionChanged"
    Style="{StaticResource EditorListBoxStyle}">
```

改成：

```xml
<ListBox
    x:Name="ItemsList"
    Style="{StaticResource EditorListBoxStyle}">
```

- [ ] **Step 3: 删除 `EditItemsWindow.xaml.cs` 中所有底部面板相关的成员**

打开 `src/Rhythm/EditItemsWindow.xaml.cs`，删除以下成员：

- `private ItemViewModel? Selected => ItemsList.SelectedItem as ItemViewModel;`
- `OnSelectionChanged`
- `OnRename`
- `OnMoveUp`（之前 Task 3 占位的版本）
- `OnMoveDown`（同上）
- `OnRemove`
- `OnEditItemKeyDown`
- `RefreshSelectionActions`

修改 `DoAdd` 方法，删掉 `EditTextBox.Focus()` / `EditTextBox.SelectAll()` / `RefreshSelectionActions()` 这几行。最终 `DoAdd` 应为：

```csharp
private void DoAdd()
{
    var text = NewItemText.Text;
    if (string.IsNullOrWhiteSpace(text)) return;
    var kind = OneTimeItemRadio.IsChecked == true ? RhythmItemKind.OneTime : RhythmItemKind.Daily;
    var added = _vm.AddItem(text, kind);
    NewItemText.Clear();
    ItemsList.SelectedItem = added;
    ItemsList.ScrollIntoView(added);
}
```

- [ ] **Step 4: 构建并跑测试**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet test Rhythm.slnx -c Debug`
Expected: 构建成功，所有 state 测试通过。

- [ ] **Step 5: 手动 smoke test**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

打开编辑窗口（托盘右键 → 编辑事项），验证：
- 列表显示当前所有事项（含每日/一次性徽章）。
- 底部"选中事项"面板**完全消失**，只剩列表和"+ 添加"那一行。
- 在底部输入框输入新事项 + 选 Daily/OneTime + 点添加，列表立即出现新事项。
- 关闭窗口正常。

无 UI 异常即可。

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/UI/MainViewModel.cs src/Rhythm/EditItemsWindow.xaml src/Rhythm/EditItemsWindow.xaml.cs
git commit -m "refactor: Wire MainViewModel to new state API, drop selection panel"
```

---

### Task 6: App.xaml 注册 `BooleanToVisibilityConverter`

**Files:**
- Modify: `src/Rhythm/App.xaml`

- [ ] **Step 1: 在 App.xaml 资源里加正向 converter**

把 `src/Rhythm/App.xaml` 替换为：

```xml
<Application x:Class="Rhythm.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Rhythm"
             xmlns:swc="clr-namespace:System.Windows.Controls;assembly=PresentationFramework"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Dark.xaml"/>
            </ResourceDictionary.MergedDictionaries>
            <local:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibilityConverter"/>
            <swc:BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: 构建确认**

Run: `dotnet build Rhythm.slnx -c Debug`
Expected: 构建成功。

- [ ] **Step 3: 提交**

```bash
git add src/Rhythm/App.xaml
git commit -m "feat: Register BooleanToVisibilityConverter as BoolToVisibilityConverter"
```

---

### Task 7: 重写 ItemTemplate（把手 + 文本 + 徽章按钮 + 删除按钮）+ 点击徽章切换 kind + 点击 × 删除

**Files:**
- Modify: `src/Rhythm/EditItemsWindow.xaml`
- Modify: `src/Rhythm/EditItemsWindow.xaml.cs`

- [ ] **Step 1: 添加新的两个 Button Style 到 `Window.Resources`**

在 `src/Rhythm/EditItemsWindow.xaml` 的 `<Window.Resources>` 中，紧跟在 `EditorListBoxStyle`**之前**新增：

```xml
<Style x:Key="KindBadgeButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource EditInputBackgroundBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource DividerBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Padding" Value="7,2" />
    <Setter Property="Height" Value="22" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border
                    x:Name="BadgeBorder"
                    Padding="{TemplateBinding Padding}"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="6">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="BadgeBorder" Property="Background" Value="{StaticResource EditCardHoverBrush}" />
                        <Setter TargetName="BadgeBorder" Property="BorderBrush" Value="{StaticResource AccentBrush}" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="DeleteButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Foreground" Value="{StaticResource MutedForegroundBrush}" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="FontWeight" Value="Medium" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border
                    x:Name="DeleteBorder"
                    Background="{TemplateBinding Background}"
                    CornerRadius="6">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="DeleteBorder" Property="Background" Value="{StaticResource EditDangerBrush}" />
                        <Setter Property="Foreground" Value="White" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="DragHandleStyle" TargetType="Border">
    <Setter Property="Width" Value="24" />
    <Setter Property="Height" Value="24" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Cursor" Value="SizeAll" />
</Style>
```

- [ ] **Step 2: 在 `EditorListBoxStyle` 里去掉选中态的高亮，并在 ItemContainerStyle 加 `MouseDoubleClick` 事件**

在同一个文件里找到 `EditorListBoxStyle` 中的 `ItemContainerStyle`，把里面 `ControlTemplate.Triggers` 改成（去掉 `IsSelected` 那个 Trigger）：

```xml
<ControlTemplate.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
        <Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource EditCardHoverBrush}" />
    </Trigger>
</ControlTemplate.Triggers>
```

并在同一个 `ItemContainerStyle` 的 `<Style TargetType="ListBoxItem">` 内加一个 `EventSetter`（紧跟在 `<Setter Property="HorizontalContentAlignment" Value="Stretch" />` 之后）：

```xml
<EventSetter Event="MouseDoubleClick" Handler="OnItemMouseDoubleClick" />
```

- [ ] **Step 3: 重写 `ListBox.ItemTemplate`**

把整个 `<ListBox.ItemTemplate>` 段替换为：

```xml
<ListBox.ItemTemplate>
    <DataTemplate>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <Border
                Grid.Column="0"
                Margin="0,0,8,0"
                Style="{StaticResource DragHandleStyle}">
                <TextBlock
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    FontSize="16"
                    Foreground="{StaticResource MutedForegroundBrush}"
                    Text="≡" />
            </Border>

            <TextBlock
                Grid.Column="1"
                VerticalAlignment="Center"
                Style="{StaticResource EditorItemTextStyle}"
                Text="{Binding Text}" />

            <Button
                Grid.Column="2"
                Margin="10,0,0,0"
                Click="OnKindBadgeClick"
                Style="{StaticResource KindBadgeButtonStyle}">
                <TextBlock
                    Style="{StaticResource EditorKindLabelStyle}"
                    Text="{Binding KindLabel}" />
            </Button>

            <Button
                Grid.Column="3"
                Width="24"
                Height="24"
                Margin="6,0,0,0"
                Padding="0"
                Click="OnDeleteClick"
                Content="×"
                Style="{StaticResource DeleteButtonStyle}" />
        </Grid>
    </DataTemplate>
</ListBox.ItemTemplate>
```

（行内编辑 TextBox、把手的拖拽事件都放到后续 Task。本 Task 仅完成静态视觉 + 切换/删除两个 click handler。`OnItemMouseDoubleClick` 在 Task 8 实现，但事件 setter 现在加上是允许的——XAML 编译期会要求 method 存在，所以本 Task 同时在 code-behind 留空 stub。）

- [ ] **Step 4: 在 `EditItemsWindow.xaml.cs` 加四个 handler**

在 `EditItemsWindow.xaml.cs` 末尾（`FindAncestor` 方法之前）新增：

```csharp
private void OnKindBadgeClick(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
    {
        var newKind = itemVm.Kind == RhythmItemKind.Daily
            ? RhythmItemKind.OneTime
            : RhythmItemKind.Daily;
        _vm.ChangeKind(itemVm, newKind);
    }
}

private void OnDeleteClick(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
    {
        _vm.RemoveItem(itemVm);
    }
}

private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    // Implemented in Task 8 (inline editing).
}
```

确保 `using Rhythm.State;` 已存在（应该已有）。

- [ ] **Step 5: 构建与手动验证**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

打开编辑窗口验证：
- 每行从左到右显示：≡ 把手 / 事项文本 / 类型徽章按钮 / × 删除按钮。
- 鼠标 hover 徽章按钮，背景变深、边框变蓝色（AccentBrush）。
- 点击徽章：Daily ↔ OneTime 切换，立即生效；关闭再打开编辑窗口，类型仍正确。
- 鼠标 hover × 按钮，背景变红色（EditDangerBrush）、× 变白。
- 点击 × 按钮，对应行立即从列表消失；关闭再打开，确认删除已持久化。
- 把手 / 文本区域目前点击没反应（拖拽和双击编辑在后续 Task 实现）。

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/EditItemsWindow.xaml src/Rhythm/EditItemsWindow.xaml.cs
git commit -m "feat: New row template with kind toggle and inline delete"
```

---

### Task 8: 双击行内编辑

**Files:**
- Modify: `src/Rhythm/EditItemsWindow.xaml`
- Modify: `src/Rhythm/EditItemsWindow.xaml.cs`

- [ ] **Step 1: 在 ItemTemplate 的文本列加一个 TextBox 叠层**

把 Task 7 中 `Grid.Column="1"` 那个 TextBlock**替换**为：

```xml
<Grid Grid.Column="1" VerticalAlignment="Center">
    <TextBlock
        Style="{StaticResource EditorItemTextStyle}"
        Text="{Binding Text}"
        Visibility="{Binding IsEditing, Converter={StaticResource InverseBoolToVisibilityConverter}}" />
    <TextBox
        x:Name="InlineEditTextBox"
        KeyDown="OnEditTextBoxKeyDown"
        LostFocus="OnEditTextBoxLostFocus"
        Loaded="OnEditTextBoxLoaded"
        Style="{StaticResource DialogTextBoxStyle}"
        Text="{Binding Text, Mode=OneWay}"
        Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisibilityConverter}}" />
</Grid>
```

注意 `x:Name` 在 `DataTemplate` 内只是模板局部名，不参与 code-behind 直接引用（不要在 code-behind 里写 `InlineEditTextBox.Foo`）；handler 通过 `sender as TextBox` 拿到具体实例。

- [ ] **Step 2: 在 `EditItemsWindow.xaml.cs` 替换 stub `OnItemMouseDoubleClick` 为真实实现**

把 Task 7 留下的 stub 改成：

```csharp
private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (e.OriginalSource is not DependencyObject source) return;
    if (FindAncestor<Button>(source) != null) return;
    if (sender is not ListBoxItem listItem) return;
    if (listItem.DataContext is not ItemViewModel itemVm) return;
    if (itemVm.IsEditing) return;

    itemVm.IsEditing = true;
    // OnEditTextBoxLoaded 会在 TextBox Visible 后接管 Focus/SelectAll。
    e.Handled = true;
}
```

- [ ] **Step 3: 在同文件加 Loaded / KeyDown / LostFocus / CommitEdit 四个 handler**

紧接着上一步的方法之后新增：

```csharp
private void OnEditTextBoxLoaded(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox tb) return;
    if (tb.DataContext is not ItemViewModel itemVm) return;
    if (!itemVm.IsEditing) return;
    // TextBox 第一次显示出来时进入编辑——Focus + SelectAll。
    tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
    {
        tb.Focus();
        tb.SelectAll();
    });
}

private void OnEditTextBoxKeyDown(object sender, KeyEventArgs e)
{
    if (sender is not TextBox tb) return;
    if (tb.DataContext is not ItemViewModel itemVm) return;

    if (e.Key == Key.Enter)
    {
        CommitInlineEdit(itemVm, tb);
        e.Handled = true;
    }
    else if (e.Key == Key.Escape)
    {
        tb.Text = itemVm.Text;
        itemVm.IsEditing = false;
        e.Handled = true;
    }
}

private void OnEditTextBoxLostFocus(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox tb) return;
    if (tb.DataContext is not ItemViewModel itemVm) return;
    if (!itemVm.IsEditing) return;
    CommitInlineEdit(itemVm, tb);
}

private void CommitInlineEdit(ItemViewModel itemVm, TextBox tb)
{
    var newText = tb.Text?.Trim() ?? string.Empty;
    if (!string.IsNullOrEmpty(newText) && newText != itemVm.Text)
    {
        _vm.RenameItem(itemVm, newText);
    }
    else
    {
        tb.Text = itemVm.Text;
    }
    itemVm.IsEditing = false;
}
```

注意 `OnEditTextBoxLoaded` 里用了 `tb.Dispatcher.BeginInvoke` + `DispatcherPriority.Input` —— 这是因为 Visibility 从 Collapsed 变成 Visible 时 Loaded 会被触发（容器复用场景），需要等一拍布局稳定后再 Focus，否则 Focus 可能无效。

- [ ] **Step 4: 构建与手动验证**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

打开编辑窗口验证：
- 双击事项文本（不要点到把手/徽章/×）→ 文本变成 TextBox，光标在内、文字全选。
- 输入新文本 + Enter → 文本更新，TextBox 消失。
- 双击进入编辑，输入修改后按 Esc → 还原原值，TextBox 消失。
- 双击进入编辑，输入修改后点击窗口其他空白处（让 TextBox 失焦）→ 修改保存。
- 双击进入编辑，清空 TextBox + 失焦 → 原值不变，TextBox 消失。
- 双击进入编辑，点击右侧徽章按钮 → 失焦保存当前值，徽章切换 kind（顺序：commit → kind toggle）。

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/EditItemsWindow.xaml src/Rhythm/EditItemsWindow.xaml.cs
git commit -m "feat: Double-click inline edit for item text"
```

---

### Task 9: 拖拽排序（`DropLineAdorner` + 拖拽 handler）

**Files:**
- Create: `src/Rhythm/UI/DropLineAdorner.cs`
- Modify: `src/Rhythm/EditItemsWindow.xaml`
- Modify: `src/Rhythm/EditItemsWindow.xaml.cs`

- [ ] **Step 1: 创建 `DropLineAdorner`**

新建 `src/Rhythm/UI/DropLineAdorner.cs`：

```csharp
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rhythm.UI;

public sealed class DropLineAdorner : Adorner
{
    private readonly Pen _pen;
    private double _y;
    private bool _visible;

    public DropLineAdorner(UIElement adornedElement, Brush brush) : base(adornedElement)
    {
        _pen = new Pen(brush, 2.0);
        _pen.Freeze();
        IsHitTestVisible = false;
    }

    public void Show(double y)
    {
        _y = y;
        _visible = true;
        InvalidateVisual();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!_visible) return;
        var width = AdornedElement.RenderSize.Width;
        drawingContext.DrawLine(_pen, new Point(0, _y), new Point(width, _y));
    }
}
```

- [ ] **Step 2: 给拖拽把手挂事件、给 ListBox 启用 drop**

修改 `src/Rhythm/EditItemsWindow.xaml`：

**ItemTemplate 中把手 Border**（`Grid.Column="0"`）改为带 PreviewMouse 事件：

```xml
<Border
    Grid.Column="0"
    Margin="0,0,8,0"
    PreviewMouseLeftButtonDown="OnDragHandlePreviewMouseDown"
    PreviewMouseMove="OnDragHandlePreviewMouseMove"
    Style="{StaticResource DragHandleStyle}">
    <TextBlock
        HorizontalAlignment="Center"
        VerticalAlignment="Center"
        FontSize="16"
        Foreground="{StaticResource MutedForegroundBrush}"
        Text="≡" />
</Border>
```

**ListBox 启用 drop**：

```xml
<ListBox
    x:Name="ItemsList"
    AllowDrop="True"
    DragLeave="OnListBoxDragLeave"
    DragOver="OnListBoxDragOver"
    Drop="OnListBoxDrop"
    Style="{StaticResource EditorListBoxStyle}">
```

- [ ] **Step 3: 在 `EditItemsWindow.xaml.cs` 加拖拽 handler 与辅助方法**

在文件顶部 using 区追加（若尚无）：

```csharp
using System;
using System.Windows.Documents;
```

确认 `using Rhythm.UI;` 存在（用于 `DropLineAdorner`）；若 `EditItemsWindow.xaml.cs` 已经位于 `namespace Rhythm`，需要在 using 里显式引入：

```csharp
using Rhythm.UI;
```

在类的字段段（构造函数上方）加：

```csharp
private Point? _dragStart;
private ItemViewModel? _dragItem;
private DropLineAdorner? _dropAdorner;
```

在类末尾（`FindAncestor` 之前）追加：

```csharp
private void OnDragHandlePreviewMouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton != MouseButton.Left) return;
    if (sender is not FrameworkElement fe) return;
    if (fe.DataContext is not ItemViewModel itemVm) return;
    _dragStart = e.GetPosition(null);
    _dragItem = itemVm;
}

private void OnDragHandlePreviewMouseMove(object sender, MouseEventArgs e)
{
    if (_dragStart is not { } start || _dragItem is null) return;
    if (e.LeftButton != MouseButtonState.Pressed)
    {
        _dragStart = null;
        _dragItem = null;
        return;
    }

    var pos = e.GetPosition(null);
    if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
        Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        return;

    var item = _dragItem;
    var data = new DataObject(typeof(ItemViewModel), item);
    DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);

    _dragStart = null;
    _dragItem = null;
    _dropAdorner?.Hide();
}

private void OnListBoxDragOver(object sender, DragEventArgs e)
{
    if (!e.Data.GetDataPresent(typeof(ItemViewModel)))
    {
        e.Effects = DragDropEffects.None;
        return;
    }
    e.Effects = DragDropEffects.Move;

    EnsureDropAdorner();
    var y = ComputeDropLineY(e);
    _dropAdorner?.Show(y);
    e.Handled = true;
}

private void OnListBoxDragLeave(object sender, DragEventArgs e)
{
    _dropAdorner?.Hide();
}

private void OnListBoxDrop(object sender, DragEventArgs e)
{
    _dropAdorner?.Hide();
    if (e.Data.GetData(typeof(ItemViewModel)) is not ItemViewModel dragged) return;
    var newIndex = ComputeDropIndex(e, dragged);
    if (newIndex < 0) return;
    _vm.MoveItemTo(dragged, newIndex);
    e.Handled = true;
}

private void EnsureDropAdorner()
{
    if (_dropAdorner != null) return;
    var layer = AdornerLayer.GetAdornerLayer(ItemsList);
    if (layer == null) return;
    _dropAdorner = new DropLineAdorner(ItemsList, (Brush)FindResource("AccentBrush"));
    layer.Add(_dropAdorner);
}

private int ComputeDropIndex(DragEventArgs e, ItemViewModel dragged)
{
    var target = FindListBoxItemUnderMouse(e);
    int draggedIdx = _vm.Items.IndexOf(dragged);
    if (draggedIdx < 0) return -1;

    if (target is null || target.DataContext is not ItemViewModel targetVm)
    {
        // 鼠标在所有 item 下面 —— 放到最后
        return _vm.Items.Count - 1;
    }

    int targetIdx = _vm.Items.IndexOf(targetVm);
    if (targetIdx < 0) return -1;

    var pos = e.GetPosition(target);
    bool insertAfter = pos.Y > target.ActualHeight / 2;
    int insertIdx = insertAfter ? targetIdx + 1 : targetIdx;
    if (draggedIdx < insertIdx) insertIdx--;
    if (insertIdx < 0) insertIdx = 0;
    if (insertIdx >= _vm.Items.Count) insertIdx = _vm.Items.Count - 1;
    return insertIdx;
}

private double ComputeDropLineY(DragEventArgs e)
{
    var target = FindListBoxItemUnderMouse(e);
    if (target is null)
    {
        var lastIdx = _vm.Items.Count - 1;
        if (lastIdx < 0) return 0;
        if (ItemsList.ItemContainerGenerator.ContainerFromIndex(lastIdx) is not ListBoxItem last) return 0;
        return last.TranslatePoint(new Point(0, last.ActualHeight), ItemsList).Y;
    }
    var pos = e.GetPosition(target);
    bool insertAfter = pos.Y > target.ActualHeight / 2;
    return target.TranslatePoint(new Point(0, insertAfter ? target.ActualHeight : 0), ItemsList).Y;
}

private ListBoxItem? FindListBoxItemUnderMouse(DragEventArgs e)
{
    if (ItemsList.InputHitTest(e.GetPosition(ItemsList)) is not DependencyObject hit) return null;
    return FindAncestor<ListBoxItem>(hit);
}
```

- [ ] **Step 4: 构建与手动验证**

Run: `dotnet build Rhythm.slnx -c Debug && dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

打开编辑窗口，加 3-4 个事项，验证：
- 按住 ≡ 把手往上/下拖动 → 出现一条蓝色 2px 横线指示目标插入位置（上半行 → 该项之前，下半行 → 该项之后）。
- 拖到列表所有项之下放手 → 项目落到最后。
- 释放 → 列表立即重排，蓝线消失；关闭窗口再打开顺序仍正确（持久化生效）。
- 拖动到原位释放 → 列表不变。
- 编辑状态（双击进入）下点击把手 → TextBox 失焦 commit 后才进入拖拽阶段（鼠标在把手按下时 TextBox 已失焦）。
- 关闭/重新打开窗口、切换透明/不透明模式（如果有方便切换的入口），拖拽都正常。

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/UI/DropLineAdorner.cs src/Rhythm/EditItemsWindow.xaml src/Rhythm/EditItemsWindow.xaml.cs
git commit -m "feat: Drag-and-drop reordering via grip handle with drop indicator"
```

---

## Self-Review

**Spec coverage:**

- 把手拖拽排序：Task 9 ✓
- 双击编辑名称：Task 8 ✓
- 点击徽章切换：Task 7 ✓
- 删除按钮：Task 7 ✓
- 移除底部"选中事项"面板：Task 5 ✓
- 不引入第三方拖拽库：Task 9 用内置 `DragDrop` + 自定义 Adorner ✓
- 状态机新增 `MoveItemTo` / `ChangeKind`、删除 `MoveItem(delta)`：Task 1–3 ✓
- `ItemViewModel` Text/Kind 可观察 + IsEditing：Task 4 ✓
- `MainViewModel` RenameItem 原地化、MoveItemTo、ChangeKind：Task 5 ✓
- BooleanToVisibilityConverter 注册：Task 6 ✓
- 单元测试覆盖（MoveItemTo + ChangeKind 全部场景）：Task 1, 2 ✓
- 删除旧 MoveItem 测试：Task 3 ✓
- 手动验证清单（拖拽 / 双击 / 切换 / 删除）：Task 5, 7, 8, 9 ✓

无缺漏。

**Placeholder scan:** 已通读全文，无 TBD / TODO / 占位描述。所有代码块完整可用。Task 7 中 `OnItemMouseDoubleClick` 是有意的 stub（Task 8 替换），有注释说明。

**Type consistency:**

- `MoveItemTo(Guid id, int newIndex)` 在 Task 1, 5 一致。
- `ChangeKind(Guid id, RhythmItemKind newKind)` 在 Task 2, 5 一致。
- `MainViewModel.MoveItemTo(ItemViewModel itemVm, int newIndex)` 在 Task 5, 9 一致。
- `MainViewModel.ChangeKind(ItemViewModel itemVm, RhythmItemKind newKind)` 在 Task 5, 7 一致。
- `ItemViewModel.UpdateText` / `UpdateKind` 在 Task 4 定义，Task 5 调用。
- `ItemViewModel.IsEditing` setter 公开（Task 4），在 Task 8 外部赋值。
- `DropLineAdorner` 构造参数 `(UIElement, Brush)`，调用方 Task 9 `new DropLineAdorner(ItemsList, (Brush)FindResource("AccentBrush"))` 一致。
