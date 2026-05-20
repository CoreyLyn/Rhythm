# Pomodoro Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Rhythm 增加一个可配置、主窗常驻、可选绑定事项的番茄钟模块，支持专注/短休/长休循环、托盘快捷控制和重启后状态恢复。

**Architecture:** 先扩展持久化 schema，加入番茄钟配置与运行快照；再用纯 C# 状态机承接开始、暂停、跳过、自动切段与恢复逻辑，并以单元测试压实。UI 层通过 `PomodoroViewModel` 暴露状态，由 `App.xaml.cs` 的秒级计时器驱动，主窗口新增专注卡片，托盘菜单补充快捷操作，设置项通过轻量弹窗修改。

**Tech Stack:** .NET 10, WPF, xUnit, DispatcherTimer, System.Text.Json

---

## File Structure

**Created files:**

- `src/Rhythm/State/PomodoroStateSchema.cs` — 番茄钟枚举、配置和运行快照记录
- `src/Rhythm/State/PomodoroStateMachine.cs` — 纯逻辑状态机，处理开始、暂停、跳过、自动切段和恢复
- `src/Rhythm/UI/PomodoroViewModel.cs` — 主窗专注卡片的 UI 状态与操作入口
- `src/Rhythm/PomodoroSettingsWindow.xaml` — 番茄钟配置弹窗
- `src/Rhythm/PomodoroSettingsWindow.xaml.cs` — 配置输入校验与结果回传
- `tests/Rhythm.Tests/PomodoroStateMachineTests.cs` — 番茄钟状态机单元测试

**Modified files:**

- `src/Rhythm/State/StateSchema.cs` — 扩展 `StateDocument`，加入番茄钟配置与快照并升级 schema version
- `src/Rhythm/State/RhythmState.cs` — 持有并持久化番茄钟配置与快照
- `src/Rhythm/UI/RhythmStateService.cs` — 保持持久化入口不变，供番茄钟 VM 保存快照
- `src/Rhythm/UI/MainViewModel.cs` — 组合 `PomodoroViewModel`，在事项变动时同步联动
- `src/Rhythm/MainWindow.xaml` — 加入主窗专注卡片 UI
- `src/Rhythm/MainWindow.xaml.cs` — 增加番茄钟按钮和设置入口事件
- `src/Rhythm/App.xaml.cs` — 增加秒级 timer、托盘菜单快捷操作、阶段切换提示和设置窗口打开逻辑
- `src/Rhythm/Themes/Dark.xaml` — 增加专注卡片、时间数字、阶段徽章、次级按钮等样式
- `tests/Rhythm.Tests/StateStoreTests.cs` — 覆盖番茄钟字段 roundtrip 与 legacy 回退
- `README.md` — 更新功能、数据结构、限制和开发说明

---

### Task 1: 持久化 schema 增加番茄钟配置与快照

**Files:**
- Create: `src/Rhythm/State/PomodoroStateSchema.cs`
- Modify: `src/Rhythm/State/StateSchema.cs`
- Modify: `tests/Rhythm.Tests/StateStoreTests.cs`

- [ ] **Step 1: 在 `StateStoreTests` 写失败测试**

在 `tests/Rhythm.Tests/StateStoreTests.cs` 的 `Save_Then_Load_RoundTripsAllFields` 后面追加：

```csharp
[Fact]
public void Save_Then_Load_RoundTripsPomodoroFields()
{
    var linkedId = Guid.NewGuid();
    var startedAt = new DateTimeOffset(2026, 5, 19, 9, 0, 0, TimeSpan.Zero);
    var updatedAt = startedAt.AddMinutes(5);

    var doc = new StateDocument
    {
        SchemaVersion = StateDocument.CurrentSchemaVersion,
        LastResetDate = new DateOnly(2026, 5, 19),
        PomodoroConfig = new PomodoroConfig
        {
            FocusMinutes = 30,
            ShortBreakMinutes = 7,
            LongBreakMinutes = 20,
            LongBreakEvery = 3,
            AutoStartNextPhase = false,
        },
        PomodoroSession = new PomodoroSessionSnapshot
        {
            Status = PomodoroStatus.Running,
            PhaseType = PomodoroPhaseType.Focus,
            RemainingSeconds = 1200,
            CompletedFocusCountInCycle = 2,
            CompletedFocusCountToday = 5,
            LinkedItemId = linkedId,
            PhaseStartedAt = startedAt,
            LastUpdatedAt = updatedAt,
        },
    };

    StateStore.Save(doc, _path);
    var loaded = StateStore.Load(_path);

    Assert.Equal(30, loaded.PomodoroConfig.FocusMinutes);
    Assert.Equal(7, loaded.PomodoroConfig.ShortBreakMinutes);
    Assert.Equal(20, loaded.PomodoroConfig.LongBreakMinutes);
    Assert.Equal(3, loaded.PomodoroConfig.LongBreakEvery);
    Assert.False(loaded.PomodoroConfig.AutoStartNextPhase);

    Assert.Equal(PomodoroStatus.Running, loaded.PomodoroSession.Status);
    Assert.Equal(PomodoroPhaseType.Focus, loaded.PomodoroSession.PhaseType);
    Assert.Equal(1200, loaded.PomodoroSession.RemainingSeconds);
    Assert.Equal(2, loaded.PomodoroSession.CompletedFocusCountInCycle);
    Assert.Equal(5, loaded.PomodoroSession.CompletedFocusCountToday);
    Assert.Equal(linkedId, loaded.PomodoroSession.LinkedItemId);
    Assert.Equal(startedAt, loaded.PomodoroSession.PhaseStartedAt);
    Assert.Equal(updatedAt, loaded.PomodoroSession.LastUpdatedAt);
}

[Fact]
public void Load_LegacyDocument_GetsDefaultPomodoroValues()
{
    File.WriteAllText(_path, """
    {
      "schemaVersion": 1,
      "items": [],
      "completedToday": [],
      "lastResetDate": "2026-05-19",
      "enableTransparency": true
    }
    """);

    var loaded = StateStore.Load(_path);

    Assert.Equal(25, loaded.PomodoroConfig.FocusMinutes);
    Assert.Equal(5, loaded.PomodoroConfig.ShortBreakMinutes);
    Assert.Equal(15, loaded.PomodoroConfig.LongBreakMinutes);
    Assert.Equal(4, loaded.PomodoroConfig.LongBreakEvery);
    Assert.True(loaded.PomodoroConfig.AutoStartNextPhase);

    Assert.Equal(PomodoroStatus.Idle, loaded.PomodoroSession.Status);
    Assert.Equal(PomodoroPhaseType.Focus, loaded.PomodoroSession.PhaseType);
    Assert.Equal(25 * 60, loaded.PomodoroSession.RemainingSeconds);
    Assert.Null(loaded.PomodoroSession.LinkedItemId);
}
```

- [ ] **Step 2: 跑测试确认当前失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~StateStoreTests" -c Debug`

Expected: 编译失败，提示找不到 `PomodoroConfig`、`PomodoroSessionSnapshot`、`PomodoroStatus`、`PomodoroPhaseType`，以及 `StateDocument.PomodoroConfig` / `StateDocument.PomodoroSession`。

- [ ] **Step 3: 新建番茄钟 schema 文件**

创建 `src/Rhythm/State/PomodoroStateSchema.cs`：

```csharp
using System;

namespace Rhythm.State;

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

public sealed record PomodoroConfig
{
    public int FocusMinutes { get; init; } = 25;
    public int ShortBreakMinutes { get; init; } = 5;
    public int LongBreakMinutes { get; init; } = 15;
    public int LongBreakEvery { get; init; } = 4;
    public bool AutoStartNextPhase { get; init; } = true;
}

public sealed record PomodoroSessionSnapshot
{
    public PomodoroStatus Status { get; init; } = PomodoroStatus.Idle;
    public PomodoroPhaseType PhaseType { get; init; } = PomodoroPhaseType.Focus;
    public int RemainingSeconds { get; init; } = 25 * 60;
    public int CompletedFocusCountInCycle { get; init; }
    public int CompletedFocusCountToday { get; init; }
    public Guid? LinkedItemId { get; init; }
    public DateTimeOffset? PhaseStartedAt { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }

    public static PomodoroSessionSnapshot CreateIdle(PomodoroConfig? config = null)
    {
        var effective = config ?? new PomodoroConfig();
        return new PomodoroSessionSnapshot
        {
            Status = PomodoroStatus.Idle,
            PhaseType = PomodoroPhaseType.Focus,
            RemainingSeconds = effective.FocusMinutes * 60,
            CompletedFocusCountInCycle = 0,
            CompletedFocusCountToday = 0,
            LinkedItemId = null,
            PhaseStartedAt = null,
            LastUpdatedAt = null,
        };
    }
}
```

- [ ] **Step 4: 扩展 `StateDocument`**

在 `src/Rhythm/State/StateSchema.cs` 中把 `CurrentSchemaVersion` 升到 2，并加入番茄钟字段：

```csharp
public sealed class StateDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public List<RhythmItem> Items { get; init; } = new();
    public List<Guid> CompletedToday { get; init; } = new();
    public DateOnly LastResetDate { get; init; }
    public WindowPos? WindowPos { get; init; }
    public bool EnableTransparency { get; init; } = true;
    public PomodoroConfig PomodoroConfig { get; init; } = new();
    public PomodoroSessionSnapshot PomodoroSession { get; init; } = PomodoroSessionSnapshot.CreateIdle();
}
```

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~StateStoreTests" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/State/PomodoroStateSchema.cs src/Rhythm/State/StateSchema.cs tests/Rhythm.Tests/StateStoreTests.cs
git commit -m "feat: persist pomodoro config and session snapshot"
```

---

### Task 2: 实现番茄钟状态机的基础生命周期

**Files:**
- Create: `tests/Rhythm.Tests/PomodoroStateMachineTests.cs`
- Create: `src/Rhythm/State/PomodoroStateMachine.cs`

- [ ] **Step 1: 写失败测试覆盖开始、暂停、继续和重置**

创建 `tests/Rhythm.Tests/PomodoroStateMachineTests.cs`：

```csharp
using System;
using Rhythm.State;
using Xunit;

namespace Rhythm.Tests;

public sealed class PomodoroStateMachineTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 5, 19, 9, 0, 0, TimeSpan.Zero);

    private static PomodoroConfig NewConfig() => new()
    {
        FocusMinutes = 25,
        ShortBreakMinutes = 5,
        LongBreakMinutes = 15,
        LongBreakEvery = 4,
        AutoStartNextPhase = true,
    };

    [Fact]
    public void Start_UsesConfiguredFocusDurationAndOptionalLinkedItem()
    {
        var linkedId = Guid.NewGuid();
        var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));

        machine.Start(StartTime, linkedId);

        Assert.Equal(PomodoroStatus.Running, machine.Snapshot.Status);
        Assert.Equal(PomodoroPhaseType.Focus, machine.Snapshot.PhaseType);
        Assert.Equal(25 * 60, machine.Snapshot.RemainingSeconds);
        Assert.Equal(linkedId, machine.Snapshot.LinkedItemId);
        Assert.Equal(StartTime, machine.Snapshot.PhaseStartedAt);
        Assert.Equal(StartTime, machine.Snapshot.LastUpdatedAt);
    }

    [Fact]
    public void Pause_FreezesRemainingSeconds()
    {
        var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));
        machine.Start(StartTime, null);

        machine.AdvanceTo(StartTime.AddMinutes(10));
        machine.Pause(StartTime.AddMinutes(10));

        Assert.Equal(PomodoroStatus.Paused, machine.Snapshot.Status);
        Assert.Equal(15 * 60, machine.Snapshot.RemainingSeconds);
        Assert.Null(machine.Snapshot.PhaseStartedAt);
        Assert.Equal(StartTime.AddMinutes(10), machine.Snapshot.LastUpdatedAt);
    }

    [Fact]
    public void Resume_RestartsCurrentPhaseFromPausedRemainingSeconds()
    {
        var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));
        machine.Start(StartTime, null);
        machine.AdvanceTo(StartTime.AddMinutes(10));
        machine.Pause(StartTime.AddMinutes(10));

        machine.Resume(StartTime.AddMinutes(12));

        Assert.Equal(PomodoroStatus.Running, machine.Snapshot.Status);
        Assert.Equal(15 * 60, machine.Snapshot.RemainingSeconds);
        Assert.Equal(StartTime.AddMinutes(12), machine.Snapshot.PhaseStartedAt);
        Assert.Equal(StartTime.AddMinutes(12), machine.Snapshot.LastUpdatedAt);
    }

    [Fact]
    public void Reset_ReturnsToIdleFocusAndClearsLinkedItem()
    {
        var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));
        machine.Start(StartTime, Guid.NewGuid());
        machine.AdvanceTo(StartTime.AddMinutes(8));

        machine.Reset();

        Assert.Equal(PomodoroStatus.Idle, machine.Snapshot.Status);
        Assert.Equal(PomodoroPhaseType.Focus, machine.Snapshot.PhaseType);
        Assert.Equal(25 * 60, machine.Snapshot.RemainingSeconds);
        Assert.Equal(0, machine.Snapshot.CompletedFocusCountInCycle);
        Assert.Null(machine.Snapshot.LinkedItemId);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~PomodoroStateMachineTests" -c Debug`

Expected: 编译失败，提示缺少 `PomodoroStateMachine`。

- [ ] **Step 3: 实现状态机基础结构**

创建 `src/Rhythm/State/PomodoroStateMachine.cs`：

```csharp
using System;

namespace Rhythm.State;

public sealed class PomodoroStateMachine
{
    private PomodoroConfig _config;
    private PomodoroSessionSnapshot _snapshot;

    public PomodoroConfig Config => _config;
    public PomodoroSessionSnapshot Snapshot => _snapshot;

    public PomodoroStateMachine(PomodoroConfig config, PomodoroSessionSnapshot snapshot)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public void Start(DateTimeOffset now, Guid? linkedItemId)
    {
        _snapshot = new PomodoroSessionSnapshot
        {
            Status = PomodoroStatus.Running,
            PhaseType = PomodoroPhaseType.Focus,
            RemainingSeconds = _config.FocusMinutes * 60,
            CompletedFocusCountInCycle = _snapshot.CompletedFocusCountInCycle,
            CompletedFocusCountToday = _snapshot.CompletedFocusCountToday,
            LinkedItemId = linkedItemId,
            PhaseStartedAt = now,
            LastUpdatedAt = now,
        };
    }

    public void Pause(DateTimeOffset now)
    {
        if (_snapshot.Status != PomodoroStatus.Running)
            return;

        AdvanceTo(now);
        _snapshot = _snapshot with
        {
            Status = PomodoroStatus.Paused,
            PhaseStartedAt = null,
            LastUpdatedAt = now,
        };
    }

    public void Resume(DateTimeOffset now)
    {
        if (_snapshot.Status != PomodoroStatus.Paused)
            return;

        _snapshot = _snapshot with
        {
            Status = PomodoroStatus.Running,
            PhaseStartedAt = now,
            LastUpdatedAt = now,
        };
    }

    public void Reset()
    {
        _snapshot = PomodoroSessionSnapshot.CreateIdle(_config) with
        {
            CompletedFocusCountToday = _snapshot.CompletedFocusCountToday,
        };
    }

    public void UpdateConfig(PomodoroConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (_snapshot.Status == PomodoroStatus.Idle)
        {
            _snapshot = PomodoroSessionSnapshot.CreateIdle(_config) with
            {
                CompletedFocusCountToday = _snapshot.CompletedFocusCountToday,
            };
        }
    }

    public bool AdvanceTo(DateTimeOffset now)
    {
        if (_snapshot.Status != PomodoroStatus.Running || _snapshot.PhaseStartedAt is null)
            return false;

        var elapsed = (int)Math.Floor((now - _snapshot.PhaseStartedAt.Value).TotalSeconds);
        if (elapsed <= 0)
            return false;

        var remaining = Math.Max(0, _snapshot.RemainingSeconds - elapsed);
        _snapshot = _snapshot with
        {
            RemainingSeconds = remaining,
            PhaseStartedAt = now,
            LastUpdatedAt = now,
        };
        return true;
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~PomodoroStateMachineTests" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/State/PomodoroStateMachine.cs tests/Rhythm.Tests/PomodoroStateMachineTests.cs
git commit -m "feat: add pomodoro state machine lifecycle"
```

---

### Task 3: 完成状态机的切段、跳过、恢复与自动启动逻辑

**Files:**
- Modify: `tests/Rhythm.Tests/PomodoroStateMachineTests.cs`
- Modify: `src/Rhythm/State/PomodoroStateMachine.cs`

- [ ] **Step 1: 追加失败测试覆盖阶段推进**

在 `tests/Rhythm.Tests/PomodoroStateMachineTests.cs` 追加：

```csharp
[Fact]
public void AdvanceTo_FocusCompletionStartsShortBreakAndIncrementsCounts()
{
    var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));
    machine.Start(StartTime, null);

    machine.AdvanceTo(StartTime.AddMinutes(25));

    Assert.Equal(PomodoroStatus.Running, machine.Snapshot.Status);
    Assert.Equal(PomodoroPhaseType.ShortBreak, machine.Snapshot.PhaseType);
    Assert.Equal(5 * 60, machine.Snapshot.RemainingSeconds);
    Assert.Equal(1, machine.Snapshot.CompletedFocusCountInCycle);
    Assert.Equal(1, machine.Snapshot.CompletedFocusCountToday);
    Assert.Null(machine.Snapshot.LinkedItemId);
}

[Fact]
public void AdvanceTo_FourthFocusCompletionStartsLongBreak()
{
    var config = NewConfig() with { LongBreakEvery = 4 };
    var running = new PomodoroSessionSnapshot
    {
        Status = PomodoroStatus.Running,
        PhaseType = PomodoroPhaseType.Focus,
        RemainingSeconds = 60,
        CompletedFocusCountInCycle = 3,
        CompletedFocusCountToday = 7,
        PhaseStartedAt = StartTime,
        LastUpdatedAt = StartTime,
    };
    var machine = new PomodoroStateMachine(config, running);

    machine.AdvanceTo(StartTime.AddMinutes(1));

    Assert.Equal(PomodoroPhaseType.LongBreak, machine.Snapshot.PhaseType);
    Assert.Equal(15 * 60, machine.Snapshot.RemainingSeconds);
    Assert.Equal(0, machine.Snapshot.CompletedFocusCountInCycle);
    Assert.Equal(8, machine.Snapshot.CompletedFocusCountToday);
}

[Fact]
public void SkipCurrentPhase_DoesNotCountSkippedFocusAsCompleted()
{
    var machine = new PomodoroStateMachine(NewConfig(), PomodoroSessionSnapshot.CreateIdle(NewConfig()));
    machine.Start(StartTime, Guid.NewGuid());

    machine.SkipCurrentPhase(StartTime.AddMinutes(5));

    Assert.Equal(PomodoroPhaseType.ShortBreak, machine.Snapshot.PhaseType);
    Assert.Equal(PomodoroStatus.Running, machine.Snapshot.Status);
    Assert.Equal(0, machine.Snapshot.CompletedFocusCountToday);
    Assert.Null(machine.Snapshot.LinkedItemId);
}

[Fact]
public void AdvanceTo_AutoStartDisabledStopsAtNextPhasePaused()
{
    var config = NewConfig() with { AutoStartNextPhase = false };
    var machine = new PomodoroStateMachine(config, PomodoroSessionSnapshot.CreateIdle(config));
    machine.Start(StartTime, null);

    machine.AdvanceTo(StartTime.AddMinutes(25));

    Assert.Equal(PomodoroStatus.Paused, machine.Snapshot.Status);
    Assert.Equal(PomodoroPhaseType.ShortBreak, machine.Snapshot.PhaseType);
    Assert.Equal(5 * 60, machine.Snapshot.RemainingSeconds);
}

[Fact]
public void AdvanceTo_RestoresAcrossMultiplePhasesUsingElapsedTime()
{
    var config = NewConfig();
    var running = new PomodoroSessionSnapshot
    {
        Status = PomodoroStatus.Running,
        PhaseType = PomodoroPhaseType.Focus,
        RemainingSeconds = 25 * 60,
        CompletedFocusCountInCycle = 0,
        CompletedFocusCountToday = 0,
        LinkedItemId = Guid.NewGuid(),
        PhaseStartedAt = StartTime,
        LastUpdatedAt = StartTime,
    };
    var machine = new PomodoroStateMachine(config, running);

    machine.AdvanceTo(StartTime.AddMinutes(31));

    Assert.Equal(PomodoroStatus.Running, machine.Snapshot.Status);
    Assert.Equal(PomodoroPhaseType.Focus, machine.Snapshot.PhaseType);
    Assert.Equal(24 * 60, machine.Snapshot.RemainingSeconds);
    Assert.Equal(1, machine.Snapshot.CompletedFocusCountInCycle);
    Assert.Equal(1, machine.Snapshot.CompletedFocusCountToday);
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~PomodoroStateMachineTests" -c Debug`

Expected: 至少 5 个新测试失败，分别卡在阶段切换、长休、跳过、自动暂停和多阶段恢复逻辑。

- [ ] **Step 3: 扩展状态机实现阶段流转**

在 `src/Rhythm/State/PomodoroStateMachine.cs` 中把 `AdvanceTo` 替换为完整实现，并新增 `SkipCurrentPhase`、辅助方法：

```csharp
public void SkipCurrentPhase(DateTimeOffset now)
{
    if (_snapshot.Status == PomodoroStatus.Idle)
        return;

    _snapshot = BuildNextPhase(now, countCompletedFocus: false);
}

public bool AdvanceTo(DateTimeOffset now)
{
    if (_snapshot.Status != PomodoroStatus.Running)
        return false;
    if (_snapshot.PhaseStartedAt is null)
        return false;

    var elapsed = (int)Math.Floor((now - _snapshot.PhaseStartedAt.Value).TotalSeconds);
    if (elapsed <= 0)
        return false;

    var changed = false;
    var remainingElapsed = elapsed;
    var current = _snapshot;

    while (remainingElapsed > 0 && current.Status == PomodoroStatus.Running)
    {
        if (remainingElapsed < current.RemainingSeconds)
        {
            current = current with
            {
                RemainingSeconds = current.RemainingSeconds - remainingElapsed,
                PhaseStartedAt = now,
                LastUpdatedAt = now,
            };
            remainingElapsed = 0;
            break;
        }

        remainingElapsed -= current.RemainingSeconds;
        _snapshot = current;
        current = BuildNextPhase(now, countCompletedFocus: current.PhaseType == PomodoroPhaseType.Focus);
        changed = true;

        if (current.Status != PomodoroStatus.Running)
        {
            current = current with { LastUpdatedAt = now };
            remainingElapsed = 0;
        }
    }

    _snapshot = current;
    return changed || elapsed > 0;
}

private PomodoroSessionSnapshot BuildNextPhase(DateTimeOffset now, bool countCompletedFocus)
{
    var completedInCycle = _snapshot.CompletedFocusCountInCycle;
    var completedToday = _snapshot.CompletedFocusCountToday;

    if (countCompletedFocus && _snapshot.PhaseType == PomodoroPhaseType.Focus)
    {
        completedInCycle += 1;
        completedToday += 1;
    }

    var nextPhase = GetNextPhase(_snapshot.PhaseType, completedInCycle);
    if (nextPhase == PomodoroPhaseType.LongBreak)
        completedInCycle = 0;

    var nextRemaining = GetDurationSeconds(nextPhase);
    var nextStatus = _config.AutoStartNextPhase ? PomodoroStatus.Running : PomodoroStatus.Paused;

    return new PomodoroSessionSnapshot
    {
        Status = nextStatus,
        PhaseType = nextPhase,
        RemainingSeconds = nextRemaining,
        CompletedFocusCountInCycle = completedInCycle,
        CompletedFocusCountToday = completedToday,
        LinkedItemId = nextPhase == PomodoroPhaseType.Focus ? _snapshot.LinkedItemId : null,
        PhaseStartedAt = nextStatus == PomodoroStatus.Running ? now : null,
        LastUpdatedAt = now,
    };
}

private PomodoroPhaseType GetNextPhase(PomodoroPhaseType phaseType, int completedInCycle)
{
    return phaseType switch
    {
        PomodoroPhaseType.Focus when completedInCycle >= _config.LongBreakEvery => PomodoroPhaseType.LongBreak,
        PomodoroPhaseType.Focus => PomodoroPhaseType.ShortBreak,
        PomodoroPhaseType.ShortBreak => PomodoroPhaseType.Focus,
        PomodoroPhaseType.LongBreak => PomodoroPhaseType.Focus,
        _ => PomodoroPhaseType.Focus,
    };
}

private int GetDurationSeconds(PomodoroPhaseType phaseType)
{
    return phaseType switch
    {
        PomodoroPhaseType.Focus => _config.FocusMinutes * 60,
        PomodoroPhaseType.ShortBreak => _config.ShortBreakMinutes * 60,
        PomodoroPhaseType.LongBreak => _config.LongBreakMinutes * 60,
        _ => _config.FocusMinutes * 60,
    };
}
```

并把 `Start` 调整为重启一个新专注段时清掉循环内剩余绑定噪音：

```csharp
public void Start(DateTimeOffset now, Guid? linkedItemId)
{
    _snapshot = new PomodoroSessionSnapshot
    {
        Status = PomodoroStatus.Running,
        PhaseType = PomodoroPhaseType.Focus,
        RemainingSeconds = _config.FocusMinutes * 60,
        CompletedFocusCountInCycle = _snapshot.CompletedFocusCountInCycle,
        CompletedFocusCountToday = _snapshot.CompletedFocusCountToday,
        LinkedItemId = linkedItemId,
        PhaseStartedAt = now,
        LastUpdatedAt = now,
    };
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~PomodoroStateMachineTests" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/State/PomodoroStateMachine.cs tests/Rhythm.Tests/PomodoroStateMachineTests.cs
git commit -m "feat: add pomodoro phase transitions and restore logic"
```

---

### Task 4: 把番茄钟快照接入 `RhythmState`

**Files:**
- Modify: `src/Rhythm/State/RhythmState.cs`
- Modify: `tests/Rhythm.Tests/RhythmStateTests.cs`

- [ ] **Step 1: 在 `RhythmStateTests` 增加 roundtrip 测试**

在 `tests/Rhythm.Tests/RhythmStateTests.cs` 的 `ToDocument_RoundtripsMutations` 后追加：

```csharp
[Fact]
public void ToDocument_RoundtripsPomodoroConfigAndSession()
{
    var state = NewState();
    var config = new PomodoroConfig
    {
        FocusMinutes = 40,
        ShortBreakMinutes = 8,
        LongBreakMinutes = 25,
        LongBreakEvery = 2,
        AutoStartNextPhase = false,
    };
    var snapshot = new PomodoroSessionSnapshot
    {
        Status = PomodoroStatus.Paused,
        PhaseType = PomodoroPhaseType.ShortBreak,
        RemainingSeconds = 240,
        CompletedFocusCountInCycle = 1,
        CompletedFocusCountToday = 3,
        LinkedItemId = Guid.NewGuid(),
        LastUpdatedAt = new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero),
    };

    state.SetPomodoroConfig(config);
    state.SetPomodoroSession(snapshot);
    var doc = state.ToDocument();

    Assert.Equal(40, doc.PomodoroConfig.FocusMinutes);
    Assert.Equal(8, doc.PomodoroConfig.ShortBreakMinutes);
    Assert.Equal(25, doc.PomodoroConfig.LongBreakMinutes);
    Assert.Equal(2, doc.PomodoroConfig.LongBreakEvery);
    Assert.False(doc.PomodoroConfig.AutoStartNextPhase);

    Assert.Equal(PomodoroStatus.Paused, doc.PomodoroSession.Status);
    Assert.Equal(PomodoroPhaseType.ShortBreak, doc.PomodoroSession.PhaseType);
    Assert.Equal(240, doc.PomodoroSession.RemainingSeconds);
    Assert.Equal(1, doc.PomodoroSession.CompletedFocusCountInCycle);
    Assert.Equal(3, doc.PomodoroSession.CompletedFocusCountToday);
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~RhythmStateTests" -c Debug`

Expected: 编译失败，提示 `RhythmState` 缺少 `PomodoroConfig`、`PomodoroSession`、`SetPomodoroConfig`、`SetPomodoroSession`。

- [ ] **Step 3: 扩展 `RhythmState`**

在 `src/Rhythm/State/RhythmState.cs` 中加入字段、属性和 setter：

```csharp
private PomodoroConfig _pomodoroConfig;
private PomodoroSessionSnapshot _pomodoroSession;

public RhythmState(StateDocument doc)
{
    ArgumentNullException.ThrowIfNull(doc);
    _items = doc.Items.ToList();
    _completed = doc.CompletedToday.ToHashSet();
    LastResetDate = doc.LastResetDate;
    WindowPos = doc.WindowPos;
    EnableTransparency = doc.EnableTransparency;
    _pomodoroConfig = doc.PomodoroConfig ?? new PomodoroConfig();
    _pomodoroSession = doc.PomodoroSession ?? PomodoroSessionSnapshot.CreateIdle(_pomodoroConfig);
}

public PomodoroConfig PomodoroConfig => _pomodoroConfig;
public PomodoroSessionSnapshot PomodoroSession => _pomodoroSession;

public void SetPomodoroConfig(PomodoroConfig config)
{
    ArgumentNullException.ThrowIfNull(config);
    _pomodoroConfig = config;
}

public void SetPomodoroSession(PomodoroSessionSnapshot snapshot)
{
    ArgumentNullException.ThrowIfNull(snapshot);
    _pomodoroSession = snapshot;
}
```

并在 `ToDocument()` 中追加：

```csharp
PomodoroConfig = _pomodoroConfig,
PomodoroSession = _pomodoroSession,
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test Rhythm.slnx --filter "FullyQualifiedName~RhythmStateTests|FullyQualifiedName~StateStoreTests" -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/State/RhythmState.cs tests/Rhythm.Tests/RhythmStateTests.cs
git commit -m "feat: store pomodoro state inside RhythmState"
```

---

### Task 5: 新增 `PomodoroViewModel` 并接入 `MainViewModel`

**Files:**
- Create: `src/Rhythm/UI/PomodoroViewModel.cs`
- Modify: `src/Rhythm/UI/MainViewModel.cs`

- [ ] **Step 1: 创建 `PomodoroViewModel`**

创建 `src/Rhythm/UI/PomodoroViewModel.cs`：

```csharp
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class PomodoroViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;
    private readonly Func<DateTimeOffset> _nowProvider;
    private PomodoroStateMachine _machine;
    private Guid? _selectedLinkedItemId;

    public ObservableCollection<ItemViewModel> AvailableItems { get; }

    public PomodoroViewModel(
        RhythmStateService service,
        ObservableCollection<ItemViewModel> items,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        AvailableItems = items ?? throw new ArgumentNullException(nameof(items));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
        _machine = new PomodoroStateMachine(service.State.PomodoroConfig, service.State.PomodoroSession);
        _selectedLinkedItemId = _machine.Snapshot.LinkedItemId;
    }

    public PomodoroStatus Status => _machine.Snapshot.Status;
    public PomodoroPhaseType PhaseType => _machine.Snapshot.PhaseType;
    public string PhaseLabel => PhaseType switch
    {
        PomodoroPhaseType.Focus => "专注中",
        PomodoroPhaseType.ShortBreak => "短休息",
        PomodoroPhaseType.LongBreak => "长休息",
        _ => "专注中",
    };
    public string RemainingText => TimeSpan.FromSeconds(_machine.Snapshot.RemainingSeconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    public string CycleText => $"今日已完成 {_machine.Snapshot.CompletedFocusCountToday} 个番茄";
    public string LinkedItemText => ResolveLinkedItemText();
    public bool IsIdle => Status == PomodoroStatus.Idle;
    public bool IsRunning => Status == PomodoroStatus.Running;
    public bool IsPaused => Status == PomodoroStatus.Paused;
    public bool CanStartOrResume => IsIdle || IsPaused;
    public bool CanPause => IsRunning;
    public bool CanSkip => !IsIdle;
    public bool CanReset => !IsIdle || _machine.Snapshot.CompletedFocusCountToday > 0;
    public PomodoroConfig Config => _machine.Config;

    public Guid? SelectedLinkedItemId
    {
        get => _selectedLinkedItemId;
        set
        {
            if (_selectedLinkedItemId == value) return;
            _selectedLinkedItemId = value;
            OnPropertyChanged();
        }
    }

    public void StartOrResume()
    {
        var now = _nowProvider();
        if (IsPaused)
            _machine.Resume(now);
        else
            _machine.Start(now, SelectedLinkedItemId);
        PersistAndNotify();
    }

    public void Pause()
    {
        _machine.Pause(_nowProvider());
        PersistAndNotify();
    }

    public void SkipCurrentPhase()
    {
        _machine.SkipCurrentPhase(_nowProvider());
        PersistAndNotify();
    }

    public void Reset()
    {
        _machine.Reset();
        SelectedLinkedItemId = null;
        PersistAndNotify();
    }

    public void ApplyConfig(PomodoroConfig config)
    {
        _machine.UpdateConfig(config);
        PersistAndNotify();
    }

    public bool AdvanceToNow()
    {
        var changed = _machine.AdvanceTo(_nowProvider());
        if (changed)
            PersistAndNotify();
        return changed;
    }

    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(LinkedItemText));
    }

    private string ResolveLinkedItemText()
    {
        var linkedId = IsIdle ? SelectedLinkedItemId : _machine.Snapshot.LinkedItemId;
        if (linkedId is null)
            return "未关联事项";

        var item = AvailableItems.FirstOrDefault(i => i.Id == linkedId.Value);
        return item?.Text ?? "已删除事项";
    }

    private void PersistAndNotify()
    {
        _service.State.SetPomodoroConfig(_machine.Config);
        _service.State.SetPomodoroSession(_machine.Snapshot);
        _service.Persist();

        OnPropertyChanged(string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: 在 `MainViewModel` 中挂入子 ViewModel**

在 `src/Rhythm/UI/MainViewModel.cs` 中加入属性和构造器初始化：

```csharp
public PomodoroViewModel Pomodoro { get; }

public MainViewModel(RhythmStateService service)
{
    ArgumentNullException.ThrowIfNull(service);
    _service = service;
    Items = new ObservableCollection<ItemViewModel>(
        _service.State.Items.Select(i => new ItemViewModel(_service, i)));
    Pomodoro = new PomodoroViewModel(_service, Items);

    Items.CollectionChanged += (_, _) =>
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProgressText));
        Pomodoro.RefreshBindings();
    };

    foreach (var item in Items)
        item.PropertyChanged += OnItemPropertyChanged;
}
```

并在 `RenameItem`, `RemoveItem`, `RolloverAndRefresh` 末尾追加：

```csharp
Pomodoro.RefreshBindings();
```

- [ ] **Step 3: 先做一次构建校验**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 4: 提交**

```bash
git add src/Rhythm/UI/PomodoroViewModel.cs src/Rhythm/UI/MainViewModel.cs
git commit -m "feat: add pomodoro view model and main view model integration"
```

---

### Task 6: 增加番茄钟设置弹窗

**Files:**
- Create: `src/Rhythm/PomodoroSettingsWindow.xaml`
- Create: `src/Rhythm/PomodoroSettingsWindow.xaml.cs`

- [ ] **Step 1: 创建设置弹窗 XAML**

创建 `src/Rhythm/PomodoroSettingsWindow.xaml`：

```xml
<Window
    x:Class="Rhythm.PomodoroSettingsWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="番茄钟设置"
    Width="360"
    Height="320"
    ResizeMode="NoResize"
    ShowInTaskbar="False"
    WindowStartupLocation="CenterOwner">
    <Border Padding="20" Background="{StaticResource WindowBackgroundBrush}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <TextBlock Style="{StaticResource HeaderDateStyle}" Text="番茄钟设置" />

            <Grid Grid.Row="1" Margin="0,16,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="96" />
                </Grid.ColumnDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" VerticalAlignment="Center" Foreground="{StaticResource ForegroundBrush}" Text="专注时长（分钟）" />
                <TextBox x:Name="FocusMinutesBox" Grid.Row="0" Grid.Column="1" Margin="12,0,0,8" />

                <TextBlock Grid.Row="1" Grid.Column="0" VerticalAlignment="Center" Foreground="{StaticResource ForegroundBrush}" Text="短休息时长（分钟）" />
                <TextBox x:Name="ShortBreakMinutesBox" Grid.Row="1" Grid.Column="1" Margin="12,0,0,8" />

                <TextBlock Grid.Row="2" Grid.Column="0" VerticalAlignment="Center" Foreground="{StaticResource ForegroundBrush}" Text="长休息时长（分钟）" />
                <TextBox x:Name="LongBreakMinutesBox" Grid.Row="2" Grid.Column="1" Margin="12,0,0,8" />

                <TextBlock Grid.Row="3" Grid.Column="0" VerticalAlignment="Center" Foreground="{StaticResource ForegroundBrush}" Text="每几轮进入长休息" />
                <TextBox x:Name="LongBreakEveryBox" Grid.Row="3" Grid.Column="1" Margin="12,0,0,8" />

                <CheckBox
                    x:Name="AutoStartNextPhaseCheckBox"
                    Grid.Row="4"
                    Grid.ColumnSpan="2"
                    Margin="0,8,0,0"
                    Content="阶段结束后自动进入下一段" />
            </Grid>

            <StackPanel Grid.Row="2" Margin="0,20,0,0" HorizontalAlignment="Right" Orientation="Horizontal">
                <Button Width="84" Margin="0,0,8,0" Click="OnCancelClick" Content="取消" />
                <Button Width="84" Click="OnSaveClick" Content="保存" />
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 2: 创建设置弹窗 code-behind**

创建 `src/Rhythm/PomodoroSettingsWindow.xaml.cs`：

```csharp
using System;
using System.Windows;
using Rhythm.State;

namespace Rhythm;

public partial class PomodoroSettingsWindow : Window
{
    public PomodoroConfig? ResultConfig { get; private set; }

    public PomodoroSettingsWindow(PomodoroConfig config)
    {
        InitializeComponent();
        FocusMinutesBox.Text = config.FocusMinutes.ToString();
        ShortBreakMinutesBox.Text = config.ShortBreakMinutes.ToString();
        LongBreakMinutesBox.Text = config.LongBreakMinutes.ToString();
        LongBreakEveryBox.Text = config.LongBreakEvery.ToString();
        AutoStartNextPhaseCheckBox.IsChecked = config.AutoStartNextPhase;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPositiveInt(FocusMinutesBox.Text, out var focus) ||
            !TryReadPositiveInt(ShortBreakMinutesBox.Text, out var shortBreak) ||
            !TryReadPositiveInt(LongBreakMinutesBox.Text, out var longBreak) ||
            !TryReadPositiveInt(LongBreakEveryBox.Text, out var longBreakEvery))
        {
            MessageBox.Show("请输入大于 0 的整数。", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultConfig = new PomodoroConfig
        {
            FocusMinutes = focus,
            ShortBreakMinutes = shortBreak,
            LongBreakMinutes = longBreak,
            LongBreakEvery = longBreakEvery,
            AutoStartNextPhase = AutoStartNextPhaseCheckBox.IsChecked == true,
        };

        DialogResult = true;
    }

    private static bool TryReadPositiveInt(string text, out int value)
        => int.TryParse(text, out value) && value > 0;
}
```

- [ ] **Step 3: 构建确认设置窗口可编译**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

- [ ] **Step 4: 提交**

```bash
git add src/Rhythm/PomodoroSettingsWindow.xaml src/Rhythm/PomodoroSettingsWindow.xaml.cs
git commit -m "feat: add pomodoro settings window"
```

---

### Task 7: 应用层增加 timer、托盘控制和设置入口

**Files:**
- Modify: `src/Rhythm/App.xaml.cs`

- [ ] **Step 1: 增加应用级字段**

在 `src/Rhythm/App.xaml.cs` 的字段区追加：

```csharp
private DispatcherTimer? _pomodoroTimer;
private PomodoroPhaseType? _lastPomodoroPhase;
private PomodoroStatus? _lastPomodoroStatus;
```

- [ ] **Step 2: 在启动时初始化 timer 并缓存初始状态**

在 `OnStartup` 中，`_mainWindow.Show();` 之后追加：

```csharp
if (_viewModel != null)
{
    _lastPomodoroPhase = _viewModel.Pomodoro.PhaseType;
    _lastPomodoroStatus = _viewModel.Pomodoro.Status;
}

StartPomodoroTimer();
```

并新增方法：

```csharp
private void StartPomodoroTimer()
{
    _pomodoroTimer?.Stop();
    _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _pomodoroTimer.Tick += (_, _) =>
    {
        if (_viewModel == null) return;

        _viewModel.Pomodoro.AdvanceToNow();
        var phaseChanged = _lastPomodoroPhase != _viewModel.Pomodoro.PhaseType;
        var statusChanged = _lastPomodoroStatus != _viewModel.Pomodoro.Status;

        if (phaseChanged || statusChanged)
        {
            NotifyPomodoroTransition(_viewModel.Pomodoro);
            _lastPomodoroPhase = _viewModel.Pomodoro.PhaseType;
            _lastPomodoroStatus = _viewModel.Pomodoro.Status;
        }
    };
    _pomodoroTimer.Start();
}
```

- [ ] **Step 3: 增加托盘菜单动作和提示**

在 `BuildTrayIcon()` 的菜单构建中，放在 `miEdit` 后面插入：

```csharp
var miPomodoroStartPause = new MenuItem { Header = "开始/继续专注" };
miPomodoroStartPause.Click += (_, _) =>
{
    if (_viewModel == null) return;
    if (_viewModel.Pomodoro.CanPause)
        _viewModel.Pomodoro.Pause();
    else
        _viewModel.Pomodoro.StartOrResume();
};

var miPomodoroSkip = new MenuItem { Header = "跳过当前阶段" };
miPomodoroSkip.Click += (_, _) => _viewModel?.Pomodoro.SkipCurrentPhase();

var miPomodoroReset = new MenuItem { Header = "重置番茄钟" };
miPomodoroReset.Click += (_, _) => _viewModel?.Pomodoro.Reset();

var miPomodoroSettings = new MenuItem { Header = "番茄钟设置..." };
miPomodoroSettings.Click += (_, _) => OpenPomodoroSettings();
```

并加入菜单项：

```csharp
menu.Items.Add(miPomodoroStartPause);
menu.Items.Add(miPomodoroSkip);
menu.Items.Add(miPomodoroReset);
menu.Items.Add(miPomodoroSettings);
menu.Items.Add(new Separator());
```

在 `App.xaml.cs` 中新增：

```csharp
private void NotifyPomodoroTransition(PomodoroViewModel pomodoro)
{
    if (_trayIcon == null) return;

    if (pomodoro.Status == PomodoroStatus.Paused)
        return;

    var text = pomodoro.PhaseType switch
    {
        PomodoroPhaseType.Focus => "休息结束，开始下一轮专注",
        PomodoroPhaseType.ShortBreak => "专注结束，开始短休息",
        PomodoroPhaseType.LongBreak => "本轮完成，开始长休息",
        _ => "番茄钟状态已更新",
    };

    _trayIcon.ShowBalloonTip("Rhythm", text, BalloonIcon.Info);
}
```

- [ ] **Step 4: 打开设置窗口并应用配置**

在 `App.xaml.cs` 中新增：

```csharp
public void OpenPomodoroSettings()
{
    if (_viewModel == null) return;

    var window = new PomodoroSettingsWindow(_viewModel.Pomodoro.Config)
    {
        Owner = _mainWindow?.IsVisible == true ? _mainWindow : null,
    };

    if (window.ShowDialog() == true && window.ResultConfig is { } config)
    {
        _viewModel.Pomodoro.ApplyConfig(config);
    }
}
```

并在 `ShutdownApp()` 和 `OnExit()` 中追加：

```csharp
_pomodoroTimer?.Stop();
```

- [ ] **Step 5: 构建并做一轮基础运行**

Run: `dotnet build Rhythm.slnx -c Debug`

Expected: `Build succeeded.`

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

Expected: 应用启动成功；托盘菜单出现 4 个番茄钟相关入口。

- [ ] **Step 6: 提交**

```bash
git add src/Rhythm/App.xaml.cs
git commit -m "feat: add pomodoro timer and tray controls"
```

---

### Task 8: 主窗口加入专注卡片并接线操作

**Files:**
- Modify: `src/Rhythm/MainWindow.xaml`
- Modify: `src/Rhythm/MainWindow.xaml.cs`
- Modify: `src/Rhythm/Themes/Dark.xaml`

- [ ] **Step 1: 在主题中加入专注卡片样式**

在 `src/Rhythm/Themes/Dark.xaml` 中追加：

```xml
<SolidColorBrush x:Key="PomodoroCardBrush" Color="#24374A" />
<SolidColorBrush x:Key="PomodoroMutedBrush" Color="#9FB4C7" />

<Style x:Key="PomodoroCardStyle" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource PomodoroCardBrush}" />
    <Setter Property="CornerRadius" Value="10" />
    <Setter Property="Padding" Value="14" />
    <Setter Property="Margin" Value="0,0,0,12" />
</Style>

<Style x:Key="PomodoroTimerStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}" />
    <Setter Property="FontSize" Value="34" />
    <Setter Property="FontWeight" Value="Bold" />
    <Setter Property="FontFamily" Value="Consolas, Microsoft YaHei UI" />
</Style>

<Style x:Key="PomodoroMetaStyle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource PomodoroMutedBrush}" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="Margin" Value="0,4,0,0" />
</Style>
```

- [ ] **Step 2: 在主窗口 XAML 加入专注卡片**

在 `src/Rhythm/MainWindow.xaml` 中，把 header 和 items 外层 grid 改成 3 行：

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <StackPanel Grid.Row="0" Margin="0,0,0,12">
        <TextBlock Style="{StaticResource HeaderDateStyle}" Text="{Binding CurrentDate}" />
        <TextBlock Style="{StaticResource HeaderStatsStyle}" Text="{Binding ProgressText}" />
    </StackPanel>

    <Border Grid.Row="1" Style="{StaticResource PomodoroCardStyle}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <DockPanel>
                <TextBlock Style="{StaticResource HeaderStatsStyle}" Text="{Binding Pomodoro.PhaseLabel}" />
                <Button DockPanel.Dock="Right" Padding="8,2" Click="OnPomodoroSettingsClick" Content="设置" />
            </DockPanel>

            <TextBlock Grid.Row="1" Margin="0,8,0,0" Style="{StaticResource PomodoroTimerStyle}" Text="{Binding Pomodoro.RemainingText}" />

            <StackPanel Grid.Row="2" Margin="0,8,0,0">
                <TextBlock Style="{StaticResource PomodoroMetaStyle}" Text="{Binding Pomodoro.CycleText}" />
                <TextBlock Style="{StaticResource PomodoroMetaStyle}" Text="{Binding Pomodoro.LinkedItemText}" />
            </StackPanel>

            <StackPanel Grid.Row="3" Margin="0,12,0,0">
                <ComboBox
                    Margin="0,0,0,8"
                    DisplayMemberPath="Text"
                    IsEnabled="{Binding Pomodoro.IsIdle}"
                    ItemsSource="{Binding Pomodoro.AvailableItems}"
                    SelectedValue="{Binding Pomodoro.SelectedLinkedItemId, Mode=TwoWay}"
                    SelectedValuePath="Id" />

                <UniformGrid Columns="4">
                    <Button Margin="0,0,8,0" Click="OnPomodoroStartOrResumeClick" Content="开始/继续" />
                    <Button Margin="0,0,8,0" Click="OnPomodoroPauseClick" Content="暂停" />
                    <Button Margin="0,0,8,0" Click="OnPomodoroSkipClick" Content="跳过" />
                    <Button Click="OnPomodoroResetClick" Content="重置" />
                </UniformGrid>
            </StackPanel>
        </Grid>
    </Border>

    <ScrollViewer Grid.Row="2" MaxHeight="600" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
        <!-- existing items section -->
    </ScrollViewer>
</Grid>
```

- [ ] **Step 3: 在 `MainWindow.xaml.cs` 增加按钮事件**

在 `src/Rhythm/MainWindow.xaml.cs` 追加：

```csharp
private void OnPomodoroStartOrResumeClick(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.Pomodoro.StartOrResume();
}

private void OnPomodoroPauseClick(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.Pomodoro.Pause();
}

private void OnPomodoroSkipClick(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.Pomodoro.SkipCurrentPhase();
}

private void OnPomodoroResetClick(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.Pomodoro.Reset();
}

private void OnPomodoroSettingsClick(object sender, RoutedEventArgs e)
{
    if (Application.Current is App app)
        app.OpenPomodoroSettings();
}
```

- [ ] **Step 4: 手动验证主窗交互**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

验证：

- 主窗在清单上方显示专注卡片，窗口仍适合桌角放置。
- 空闲时可在下拉中选择事项或不选，点击“开始/继续”后倒计时开始。
- 运行时 `LinkedItemText` 正确显示绑定事项或“未关联事项”。
- 点击暂停、继续、跳过、重置都能反映到 UI。
- 当阶段切换时，主窗文案从“专注中”切到“短休息/长休息”，且不弹模态框。

- [ ] **Step 5: 提交**

```bash
git add src/Rhythm/MainWindow.xaml src/Rhythm/MainWindow.xaml.cs src/Rhythm/Themes/Dark.xaml
git commit -m "feat: add pomodoro card to main window"
```

---

### Task 9: 更新文档并做整体验证

**Files:**
- Modify: `README.md`

- [ ] **Step 1: 更新 README 的功能和状态文件说明**

在 `README.md` 的“核心功能”中追加一项：

```md
- 番茄钟：主窗常驻专注卡片，支持可配置专注/短休/长休循环、可选绑定事项、托盘快捷控制和重启后恢复
```

在 “本地数据” 的状态文件说明中追加：

```md
- `pomodoroConfig`
- `pomodoroSession`
```

在 “已知限制” 中删除“不支持提醒、通知或弹窗”，改成：

```md
- 仅支持托盘提示，不支持声音提醒、系统通知中心和专注历史统计
```

- [ ] **Step 2: 跑完整测试**

Run: `dotnet test Rhythm.slnx -c Debug`

Expected: `Passed!  - Failed: 0`

- [ ] **Step 3: 做最终手动回归**

Run: `dotnet run --project src/Rhythm/Rhythm.csproj -c Debug`

最终检查：

- 开始一个 25 分钟专注，暂停后关闭应用，重开后仍保持暂停和剩余时间。
- 开始一个专注，手工把 `%APPDATA%\Rhythm\state.json` 中 `remainingSeconds` 调小到 3，再重启验证阶段恢复与托盘提示。
- 关闭窗口到托盘后，托盘菜单可以开始/暂停、跳过、重置和打开设置。
- 完成一个番茄后，不会自动勾选关联事项。
- 透明和不透明窗口模式下，专注卡片都渲染正常。
- 跨午夜后，清单 rollover 仍正常；番茄钟快照不导致应用异常。

- [ ] **Step 4: 提交**

```bash
git add README.md
git commit -m "docs: describe pomodoro support"
```

---

## Self-Review

**Spec coverage:**

- 独立专注模式：Task 2, 3, 5, 8
- 可选绑定事项：Task 5, 8
- 可配置循环：Task 1, 2, 3, 6
- 主窗常驻模块：Task 8
- 窗口内提醒 + 托盘提示：Task 7, 8
- 重启恢复真实阶段：Task 3, 4, 7, 9
- 不自动勾选事项：Task 5, 9
- 轻量配置入口：Task 6, 7
- 单元测试覆盖逻辑层：Task 1, 2, 3, 4, 9

无缺项。

**Placeholder scan:**

- 没有 `TBD`、`TODO`、`implement later` 之类占位词。
- 每个任务都给了明确文件路径、代码片段、命令和预期结果。
- UI 任务没有用“自行处理样式”这种泛化描述，而是给了具体样式和控件结构。

**Type consistency:**

- 持久化类型统一为 `PomodoroConfig` + `PomodoroSessionSnapshot`
- 状态机统一为 `PomodoroStateMachine`
- 主窗子 VM 统一为 `PomodoroViewModel`
- `RhythmState` 统一通过 `SetPomodoroConfig` / `SetPomodoroSession` 持久化
- 所有 UI 入口都调用 `StartOrResume` / `Pause` / `SkipCurrentPhase` / `Reset`

命名一致。
