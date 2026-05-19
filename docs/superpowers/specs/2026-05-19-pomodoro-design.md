# 番茄钟融入 Rhythm 设计

## Goal

在不破坏 Rhythm 现有“桌面角落常驻、低打扰、轻量清单”定位的前提下，为产品增加一个可配置的番茄钟模块。该模块以独立专注模式运行，默认不依赖事项，但允许用户为当前专注段可选绑定一个现有事项。

## Background

Rhythm 当前是一个固定在桌面底层的轻量清单工具，主窗口只展示日期、完成进度和事项列表。应用层已有的定时能力只有跨午夜的 rollover；状态文件只保存事项、完成状态、窗口位置和透明配置，不包含秒级计时、提醒、会话或统计模型。

如果直接把番茄钟作为一个临时倒计时塞进现有 ViewModel，会把专注、休息、暂停、跳过、自动切段、恢复等行为混入现有清单状态，后续很难维护。因此需要把番茄钟作为主窗口里的第二核心模块来设计，但仍保持 UI 克制和交互低打扰。

## Requirements

- 提供独立专注模式，番茄钟可在没有事项绑定的情况下直接启动。
- 支持可选绑定事项。用户开始一个专注段时可以关联到现有事项，但不强制。
- 支持可配置循环：专注时长、短休息时长、长休息时长、每几轮进入长休息、是否自动进入下一阶段。
- 主窗口常驻显示番茄钟模块，不做独立番茄钟窗口。
- 阶段结束时只做窗口内状态切换和托盘提示，不播放声音，不弹模态框。
- 在窗口隐藏时仍可通过托盘快速控制番茄钟。
- 关闭应用后重新打开时，番茄钟状态应尽可能恢复到“此刻应处的真实阶段”。
- 不改变事项完成语义。完成一个番茄不应自动勾选事项。

## Out of Scope

- 不做番茄历史统计、热力图、每日目标、连续专注 streak。
- 不做多任务并发专注，不做多个计时器。
- 不接入系统通知中心、声音提醒、全屏遮罩或强制打断。
- 不引入账号、同步、云存储或跨设备状态共享。
- 不在 v1 中改变现有清单编辑流和事项数据结构语义。

## Approaches Considered

### 方案 A：在现有主窗口上直接叠加一个轻量倒计时

把剩余时间、开始/暂停按钮和少量状态直接塞进 `MainViewModel` 与 `MainWindow.xaml`。

优点：

- 改动面小，上线快。

缺点：

- 专注/休息/暂停/自动切段/恢复逻辑会迅速膨胀。
- 计时行为与清单行为耦合过深，后续扩展配置与提醒时会变脆。

### 方案 B：独立番茄钟状态域，主窗口组合展示

新增番茄钟配置、会话状态机和计时驱动，主窗口只负责把它与现有清单拼接呈现。

优点：

- 领域边界清晰，最适合“独立运行 + 可选绑定事项 + 主窗常驻”的目标。
- 状态机和恢复逻辑可在纯逻辑层测试。
- 后续增加统计或更多提醒时，不必重写清单层。

缺点：

- 首次设计与接线工作比方案 A 多。

### 方案 C：番茄钟做成独立小窗

保留现有主窗口结构不动，把专注模块放到第二个窗口。

优点：

- 对主窗口侵入最小。

缺点：

- 与“主窗常驻模块”的产品方向冲突。
- 割裂 Rhythm 作为单一桌面部件的整体感。

## Recommendation

采用方案 B：独立番茄钟状态域，主窗口组合展示。

这是唯一同时满足以下条件的方案：

- 番茄钟是主功能之一，而不是边角小工具。
- 仍保持 Rhythm 现有轻量、常驻、低打扰的产品气质。
- 计时与提醒逻辑可在状态机层单测，不把复杂性压到 WPF 事件里。

## Design

### 界面结构

主窗口改为上下两段：

1. 上方为“专注卡片”。
2. 下方保留现有清单与进度展示。

专注卡片默认常驻，但高度保持克制。默认展示：

- 当前阶段：专注 / 短休息 / 长休息 / 暂停 / 未开始
- 大号剩余时间
- 当前循环进度，例如“第 2 个专注段”
- 今日已完成番茄数或当前循环内已完成专注数
- 当前绑定事项摘要；未绑定时显示“未关联事项”
- 核心操作：`开始/继续`、`暂停`、`跳过当前阶段`、`重置`

交互约束：

- 未开始时，用户可以直接开始，不必先选事项。
- 事项绑定是辅助信息，不改变事项是否完成。
- 阶段切换只更新卡片状态，不弹模态对话框。
- 清单区域不引入复杂专注操作，只保留未来可扩展的“关联当前专注项”轻入口。

### 番茄钟状态模型

新增独立状态域，不复用现有 `CompletedToday` 或事项完成状态。

建议新增的数据结构：

```csharp
public enum PomodoroPhaseType
{
    Focus = 0,
    ShortBreak = 1,
    LongBreak = 2,
}

public enum PomodoroStatus
{
    Idle = 0,
    Running = 1,
    Paused = 2,
}

public sealed class PomodoroConfig
{
    public int FocusMinutes { get; init; } = 25;
    public int ShortBreakMinutes { get; init; } = 5;
    public int LongBreakMinutes { get; init; } = 15;
    public int LongBreakEvery { get; init; } = 4;
    public bool AutoStartNextPhase { get; init; } = true;
}

public sealed class PomodoroSessionState
{
    public PomodoroStatus Status { get; init; } = PomodoroStatus.Idle;
    public PomodoroPhaseType PhaseType { get; init; } = PomodoroPhaseType.Focus;
    public int RemainingSeconds { get; init; }
    public int CompletedFocusCountInCycle { get; init; }
    public int CompletedFocusCountToday { get; init; }
    public Guid? LinkedItemId { get; init; }
    public DateTimeOffset? PhaseStartedAt { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
}
```

说明：

- `Status` 表示当前是否在运行、暂停或空闲。
- `PhaseType` 表示当前阶段属于专注、短休息还是长休息。
- `RemainingSeconds` 只用于快照与恢复，不单独作为真实时间来源。
- `CompletedFocusCountInCycle` 用于决定何时进入长休息。
- `CompletedFocusCountToday` 用于 UI 展示“今日已完成番茄数”。
- `LinkedItemId` 可空，表示本段专注是否关联到某个事项。
- `PhaseStartedAt` 与 `LastUpdatedAt` 用于重启后的时间回推与状态恢复。

### 状态机行为

状态机至少支持以下操作：

- `Start(optional linkedItemId)`
- `Pause()`
- `Resume()`
- `SkipCurrentPhase()`
- `Reset()`
- `Tick(now)`
- `Restore(now)`

核心规则：

- `Idle -> Start`：进入 `Running + Focus`，剩余时间设为配置中的专注分钟数。
- `Running + Focus` 结束：`CompletedFocusCountInCycle + 1`，若达到 `LongBreakEvery` 则进入 `LongBreak`，否则进入 `ShortBreak`。
- `Running + Break` 结束：进入下一段 `Focus`。
- `Paused` 不自动流逝时间。
- `SkipCurrentPhase` 立即切入下一阶段，但不补记额外完成数，只有完整结束的专注段才记为完成番茄。
- `Reset` 回到 `Idle + Focus` 的默认预备状态，并清空当前段的事项绑定。

### 持久化与恢复策略

状态文件 schema 需要扩展番茄钟配置与运行快照。建议新增：

```csharp
public sealed class StateDocument
{
    // existing fields...
    public PomodoroConfig PomodoroConfig { get; init; } = new();
    public PomodoroSessionState PomodoroSession { get; init; } = new();
}
```

恢复规则：

- 若关闭前是 `Paused`，重启后保持暂停和剩余时间不变。
- 若关闭前是 `Running`，重启时根据 `LastUpdatedAt` 或 `PhaseStartedAt` 与当前时间差，回推当前应处于哪个阶段。
- 如果应用关闭期间已跨过多个阶段，直接落到“此刻真实阶段”，不回放中间所有提示。
- 如果状态文件缺字段或损坏，番茄钟回退到默认空闲状态，不影响事项列表加载。

这样做的原因是：Rhythm 是桌面常驻工具，用户并不保证窗口一直可见。重启后恢复到“现实中的当前阶段”，比恢复到一个过期倒计时更符合产品预期。

### 计时驱动

现有应用层只有午夜 rollover 定时器，需要新增一个秒级或接近秒级的 UI 计时驱动，例如 `DispatcherTimer`。

职责边界：

- 定时器只负责周期性调用 `Tick(now)`。
- 业务决策全部在番茄钟状态机中完成。
- ViewModel 订阅状态变化并刷新 UI。
- 阶段切换时，由应用层桥接托盘提示。

这样可以避免把阶段推进写在窗口 code-behind 中，也避免 `MainViewModel` 承担过多时间计算责任。

### ViewModel 与 UI 分层

建议新增番茄钟专属 ViewModel，而不是继续把逻辑堆进现有 `MainViewModel`。

推荐结构：

- `PomodoroState` 或 `PomodoroSessionStateMachine`：纯逻辑层
- `PomodoroService`：封装持久化、时间推进、提醒桥接
- `PomodoroViewModel`：专注卡片的 UI 状态
- `MainViewModel`：组合 `PomodoroViewModel` 与现有清单数据

`MainViewModel` 只保留组合职责，例如：

- 暴露 `Pomodoro` 子 ViewModel 给主窗口绑定
- 保持原有事项列表、进度、窗口配置职责不变

### 事项绑定策略

番茄钟与事项是弱耦合但可关联：

- 开始专注时允许选一个事项，也允许不选。
- 绑定仅用于上下文展示，例如“当前专注：读书 30 分钟”。
- 阶段结束时不自动勾选事项。
- 切换到休息阶段时保留绑定摘要用于回顾，但下一段新的专注开始时允许重新选择或清空。

这样既满足你需要的“可选绑定”，也不扭曲事项完成语义。

### 托盘提醒与快捷控制

阶段切换时使用托盘做非阻塞提示，例如：

- `专注结束，开始短休息`
- `休息结束，开始下一轮专注`
- `第 4 个专注段完成，开始长休息`

不做：

- 声音
- 模态框
- 强制前台唤起窗口

托盘菜单建议补充以下快捷动作：

- `开始/暂停专注`
- `跳过当前阶段`
- `重置番茄钟`

这样在主窗口被隐藏时，用户仍可以从托盘控制专注流。

### 配置入口

v1 不需要完整设置页，采用轻量入口即可，例如：

- 在编辑事项窗口中增加一个“专注设置”区域，或
- 新增一个简洁的设置弹窗，由托盘菜单或专注卡片入口打开

配置项仅包含：

- 专注时长
- 短休息时长
- 长休息时长
- 每几轮进入长休息
- 是否自动进入下一阶段

避免在 v1 加入高级选项，防止把 Rhythm 推成完整生产力套件。

## Testing

### 单元测试

新增番茄钟纯逻辑测试，至少覆盖：

- 开始专注后剩余时间初始化正确
- 暂停后时间冻结，继续后恢复推进
- 专注结束后正确进入短休息
- 达到配置轮次后正确进入长休息
- `SkipCurrentPhase` 在专注段和休息段上的行为都正确
- `Reset` 可回到空闲初始状态
- 运行中关闭并在未来时间恢复，可正确跳到当前真实阶段
- 跨过多个阶段恢复时，不重复累加番茄完成数
- 状态文件缺失番茄字段或字段损坏时，安全回退到默认空闲状态

### 手动验证

- 主窗口显示专注卡片，尺寸仍适合桌面角落常驻。
- 无绑定事项时可以正常开始、暂停、继续、跳过、重置。
- 绑定事项后卡片能展示事项名称，但不会自动勾选事项。
- 隐藏主窗口后，通过托盘菜单仍可完成开始/暂停/跳过/重置。
- 阶段切换时出现托盘提示，但不会弹模态框或抢焦点。
- 关闭应用后在不同时间点重新打开，番茄钟能恢复到合理阶段。
- 跨午夜后，事项 rollover 与番茄钟计时互不干扰。

## Risks

- **主窗口高度膨胀**：番茄钟常驻后，窗口可能失去当前紧凑感。需要严格控制卡片高度和按钮数量。
- **状态恢复歧义**：如果只存剩余时间而不存时间戳，重启后会恢复错误。必须存 `LastUpdatedAt` 或等价时间锚点。
- **应用层职责滑坡**：如果把阶段推进写到 `App.xaml.cs` 或 code-behind，会快速失控。需要坚持“定时驱动与业务决策分离”。
- **事项绑定语义误用**：如果自动勾选事项，会把“完成一个番茄”和“完成一个任务”混为一谈，必须避免。
- **透明窗口与托盘提示协作**：透明模式、窗口隐藏和托盘提示之间要做一次人工联调，确认不会出现前台抢焦点或视觉闪烁。

## Rollout Notes

建议按以下顺序实施：

1. 先补番茄钟状态模型、配置与持久化。
2. 再实现纯逻辑状态机与恢复算法，并用测试压实。
3. 然后加应用层计时驱动与托盘提示桥接。
4. 最后接入 `PomodoroViewModel` 和主窗口专注卡片 UI。

这样可以把风险先压在可测试的纯逻辑层，再做 WPF 接线。
