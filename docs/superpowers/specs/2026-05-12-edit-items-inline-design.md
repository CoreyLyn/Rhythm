# 编辑事项窗口：行内直接操控

## Goal

把 `EditItemsWindow` 的所有事项操作从"选中后到底部面板编辑"改成"直接在每行上操控"。事项编辑窗口下方的"选中事项"面板整体移除。

## Background

当前 `EditItemsWindow.xaml` 有三段：

1. 顶部标题。
2. ListBox（事项列表，每行显示文本 + 类型徽章）。
3. "选中事项"面板：TextBox + 重命名 + ↑ + ↓ + 删除。
4. 底部新增事项：输入框 + 每日/一次性 segment + "添加"。

操作流程是"先在列表选中一项 → 到下方编辑"。除了步骤多，列表本身的选中状态对用户来说没有其它意义。

## Requirements

- 每行支持：把手拖拽排序、双击编辑名称、点击徽章切换 Daily/OneTime、点击 × 删除。
- 移除"选中事项"面板（包括对应的所有按钮、handler、`RefreshSelectionActions` 等支持代码）。
- 移除 ↑/↓ 按钮和它们背后的 `MoveItem(delta)`，改成基于任意目标索引的 `MoveItemTo(newIndex)`。
- 底部新增事项区保留。
- 顶部标题栏保留。
- 不引入新 NuGet 依赖。

## Out of Scope

- 主窗口 `MainWindow` 的 UI 不动。
- 状态机的跨日 rollover 逻辑、持久化 schema、文件路径不动。
- 不加删除确认对话框、不加 Undo。
- 不加批量操作、多选、键盘快捷键（除 Enter/Esc 行内编辑场景外）。
- 不引入 GongSolutions.WPF.DragDrop 等第三方拖拽库。

## Design

### 行布局

```
┌─────────────────────────────────────────────────────────────┐
│ [≡]  事项文本 / TextBox                  [类型徽章]  [×]    │
│ 24px        flex (*)                       ~60px    24px    │
└─────────────────────────────────────────────────────────────┘
```

- `≡` 把手：`Cursor=Hand`，hover 时前景从 muted 变为 default。鼠标按下后位移超过系统拖拽阈值进入 `DragDrop.DoDragDrop`。
- 文本：默认 TextBlock 显示 `Text`；`ItemViewModel.IsEditing == true` 时换成 TextBox。
- 类型徽章：保留现有圆角小标签外观，外面套一个 `Button`（模板替换、无边框），`Cursor=Hand`，hover 高亮，click 翻转 kind。
- `×` 删除：24×24 `Button`，hover 背景变 `EditDangerBrush`。

去掉 ListBox 的选中态高亮样式（`<Trigger Property="IsSelected" Value="True">`），只保留 hover 视觉。

### 数据层（`RhythmState`）

新增：

```csharp
public void MoveItemTo(Guid id, int newIndex);
// 校验：未知 id → ArgumentException；newIndex 越界 [0, Items.Count) → ArgumentException；
// 当 newIndex == 当前索引 → no-op。

public void ChangeKind(Guid id, RhythmItemKind newKind);
// 校验：未知 id / 未知 kind → ArgumentException。
// 实现：_items[idx] = _items[idx] with { Kind = newKind }。
// 不修改 _completed —— 跨日时按新 kind 决定是否移除。
```

删除：

```csharp
public void MoveItem(Guid id, int delta);
```

### ViewModel 层（`MainViewModel` / `ItemViewModel`）

`ItemViewModel` 现在的 `Text` / `Kind` 是只读字段。改成可写属性 + `OnPropertyChanged`，并新增 `IsEditing`：

```csharp
public string Text { get; private set; }      // setter via UpdateText
public RhythmItemKind Kind { get; private set; }  // setter via UpdateKind
public bool IsEditing { get; set; }           // INotifyPropertyChanged

internal void UpdateText(string text);  // raises Text + (no KindLabel change)
internal void UpdateKind(RhythmItemKind kind);  // raises Kind + KindLabel
```

`MainViewModel.RenameItem` 改成原地修改而非重建 VM：

```csharp
public void RenameItem(ItemViewModel itemVm, string newText)
{
    _service.State.RenameItem(itemVm.Id, newText);
    itemVm.UpdateText(_service.State.Items.Single(i => i.Id == itemVm.Id).Text);
    _service.Persist();
}
```

`MainViewModel.MoveItem(delta)` 替换为：

```csharp
public void MoveItemTo(ItemViewModel itemVm, int newIndex)
{
    var idx = Items.IndexOf(itemVm);
    if (idx < 0 || newIndex == idx) return;
    _service.State.MoveItemTo(itemVm.Id, newIndex);
    Items.Move(idx, newIndex);
    _service.Persist();
}
```

新增：

```csharp
public void ChangeKind(ItemViewModel itemVm, RhythmItemKind newKind)
{
    _service.State.ChangeKind(itemVm.Id, newKind);
    itemVm.UpdateKind(newKind);
    _service.Persist();
}
```

### XAML（`EditItemsWindow.xaml`）

1. 删除 `Grid.Row="2"` 的整段"选中事项" `Border`。
2. 调整 `Grid.RowDefinitions` 为 3 行（标题 / 列表 / 新增）。
3. 重写 `ListBox.ItemTemplate`：四列 grid（把手 / 文本+TextBox 叠层 / 徽章按钮 / 删除按钮）。
4. ListBox `AllowDrop="True"`，添加 `DragOver` / `DragLeave` / `Drop` 事件 handler。
5. 把手 `Border` 添加 `PreviewMouseLeftButtonDown` / `PreviewMouseMove` handler。
6. 行 `MouseDoubleClick` handler 进入编辑。
7. TextBox `KeyDown`（Enter/Esc）和 `LostFocus` handler。
8. 徽章按钮和删除按钮各自 `Click` handler。
9. App.xaml 资源加 `BooleanToVisibilityConverter`（如不存在；项目里已有 `InverseBoolToVisibilityConverter`，按相同方式增加正向版本）。

### 拖拽实现细节

- 在 `EditItemsWindow.xaml.cs` 维护两个字段：`Point? _dragStart;` `ItemViewModel? _dragItem;`。
- `OnDragHandlePreviewMouseDown`：记录起点 + item。
- `OnDragHandlePreviewMouseMove`：如果鼠标左键按下且位移超阈值，调用 `DragDrop.DoDragDrop(handle, _dragItem, DragDropEffects.Move)`，调用完清空字段。
- `OnListBoxDragOver`：通过 `VisualTreeHelper.HitTest` 找到鼠标下的 `ListBoxItem`，根据上下半部分计算 `insertIndex`，更新 Adorner。`e.Effects = DragDropEffects.Move`。
- `OnListBoxDragLeave`：清掉 Adorner。
- `OnListBoxDrop`：根据 hit-test 结果计算最终 `newIndex` 并调 `_vm.MoveItemTo(_dragItem, newIndex)`，清 Adorner。
- Adorner：自定义 `DropLineAdorner : Adorner`，在 `OnRender` 里画一条 2px 横线（`AccentBrush`），位置由公开属性驱动。挂到 ListBox 的 `AdornerLayer`。

### 行内编辑细节

- 进入：`ListBoxItem.MouseDoubleClick` → 判定 `OriginalSource` 不在把手 / 徽章 / 删除按钮内 → 找到 DataContext 的 `ItemViewModel` → `IsEditing = true`。
- Focus：XAML 里给 TextBox 加 `Loaded` handler，IsEditing 为 true 时 `BeginInvoke` 调 `Focus()` + `SelectAll()`；或在 code-behind 中显式找该 TextBox。简单做法是直接在 code-behind 切换 `IsEditing` 之后用 `Dispatcher.BeginInvoke(DispatcherPriority.Input)` 拿到 container 内的 TextBox 调 Focus。
- 提交：`CommitEdit(itemVm, textBox)`——Trim 后空 → 还原 TextBox.Text 为原值并 `IsEditing=false`；非空且变化 → `_vm.RenameItem`；最后 `IsEditing=false`。
- 取消：Esc 键 → `TextBox.Text = itemVm.Text`；`IsEditing=false`。
- `LostFocus` 走 commit；`Enter` 走 commit；`Esc` 走 cancel。

### 类型切换 / 删除细节

- 徽章按钮 `Click` handler：从 `(sender as Button).DataContext` 取 `ItemViewModel`，根据当前 `Kind` 决定 `newKind`，调 `_vm.ChangeKind(itemVm, newKind)`。
- 删除按钮 `Click` handler：从 DataContext 取 `ItemViewModel`，调 `_vm.RemoveItem(itemVm)`。

## Testing

### 单元测试（`RhythmStateTests.cs`）

**新增**：

- `MoveItemTo_MovesItemToSpecifiedIndex` —— 三项列表中把 c 移到位置 0。
- `MoveItemTo_NoOpWhenIndexEqualsCurrent`。
- `MoveItemTo_ThrowsForOutOfRange` —— -1 和 `Items.Count`。
- `MoveItemTo_ThrowsForUnknownId`。
- `ChangeKind_UpdatesKindWithoutTouchingCompletion` —— 已完成的 Daily 改 OneTime，`_completed` 不变。
- `ChangeKind_ThrowsForUnknownId`。
- `ChangeKind_RejectsUnknownKind` —— `(RhythmItemKind)999`。
- `ChangeKind_FollowedByRolloverRemovesIfCompleted` —— 已完成 Daily 改 OneTime 后跨日，应被移除（验证语义一致性）。

**删除**：

- `MoveItem_UpAndDownReorders`、`MoveItem_ClampsAtEdges`。

### 手动验证

- 拖动一项到列表最上方、最下方、中间位置；释放后顺序正确，重启应用顺序仍正确。
- 双击文本 → 出现 TextBox 且全选；Enter 保存、Esc 还原、点空白处保存、清空后失焦还原原值。
- 点击徽章 Daily ↔ OneTime 切换，重启后类型保留。
- 点击 × 删除，列表立即更新。
- 拖拽时把手图标显示拖拽光标；松开后 drop 线消失。
- 编辑过程中按 ≡ 拖拽：编辑应先 commit（LostFocus）然后拖拽。
- 不勾选透明、不勾选透明 + 解锁开机自启的组合不出错（不会因为 InitializeComponent 顺序改变）。

## Risks

- **Adorner 在透明窗口里的渲染**：WPF `AllowsTransparency=True` + Adorner 通常没问题，但需要在透明 / 不透明两种模式下都人工试一遍。如果 Adorner 异常，退路是直接在 ListBox 的目标 `ListBoxItem` 上临时改 `BorderBrush`。
- **`ItemViewModel.Text` 改成可变**：当前 `RenameItem` 重建 VM 是为了让绑定刷新；改成原地更新后必须确保 `OnPropertyChanged(nameof(Text))` 真的触发。否则 UI 不刷新。
- **新增 BooleanToVisibilityConverter 与已有 InverseBoolToVisibilityConverter 命名冲突**：先 grep 确认；项目里如果已经有内置 `BooleanToVisibilityConverter` 就直接用 WPF 自带的（`<BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>`）。
- **拖拽和 `Border.MouseLeftButtonDown` 的窗口拖动冲突**：当前 `OnWindowChromeMouseLeftButtonDown` 在 `Border` 上 `DragMove`，但已经排除了 `ListBoxItem` 内的事件源。新加把手是在 `ListBoxItem` 内，应该不会冲突；要在实现时验证。
