# 编辑事项添加事项默认一次性

## Goal

编辑事项窗口中新增事项的类型选项默认选中“一次性”，让临时事项添加路径更符合当前产品定位。

## Requirements

* 打开“编辑事项”窗口时，新增事项区域默认选中“一次性”。
* 用户仍然可以手动切换到“每日”后添加每日事项。
* 只改变编辑窗口新增事项区域的 UI 默认选择，不改变状态机或 ViewModel 的默认 `AddItem` 行为。

## Acceptance Criteria

* [ ] `EditItemsWindow` 新增事项区域默认选中“一次性”。
* [ ] 选择“一次性”后添加的事项仍保存为 `RhythmItemKind.OneTime`。
* [ ] 手动选择“每日”后添加的事项仍保存为 `RhythmItemKind.Daily`。
* [ ] 项目构建和测试通过。

## Out of Scope

* 不修改已有事项类型。
* 不修改主窗口、状态机跨日逻辑、持久化 schema 或 README 文案。

## Research References

* 无。该任务为现有 WPF 表单默认值调整，技术路径可由仓库代码直接确定。
