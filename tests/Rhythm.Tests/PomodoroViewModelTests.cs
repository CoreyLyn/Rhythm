using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rhythm;
using Rhythm.State;
using Rhythm.UI;
using Xunit;

namespace Rhythm.Tests;

public sealed class PomodoroViewModelTests : IDisposable
{
    private readonly string _statePath;
    private readonly bool _hadOriginalStateFile;
    private readonly string? _originalStateContent;

    public PomodoroViewModelTests()
    {
        _statePath = AppPaths.StateFilePath;
        _hadOriginalStateFile = File.Exists(_statePath);
        _originalStateContent = _hadOriginalStateFile ? File.ReadAllText(_statePath) : null;
    }

    [Fact]
    public void SelectedLinkedItemId_InIdle_ShowsPendingLinkedItemText()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(itemId, "Test task"),
            ]);
        var viewModel = context.ViewModel;

        Assert.True(viewModel.Pomodoro.IsIdle);
        Assert.Null(viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Equal("关联事项：未关联事项", viewModel.Pomodoro.LinkedItemText);

        viewModel.Pomodoro.SelectedLinkedItemId = itemId;

        Assert.Equal(itemId, viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Equal("待关联事项：Test task", viewModel.Pomodoro.LinkedItemText);
    }

    [Fact]
    public void AdvanceToNow_InIdle_KeepsSelectedLinkedItemId()
    {
        var itemId = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(itemId, "Test task"),
            ],
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25));
        var viewModel = context.ViewModel;

        viewModel.Pomodoro.SelectedLinkedItemId = itemId;

        viewModel.Pomodoro.AdvanceToNow();

        Assert.True(viewModel.Pomodoro.IsIdle);
        Assert.Equal(itemId, viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Equal("待关联事项：Test task", viewModel.Pomodoro.LinkedItemText);
    }

    [Fact]
    public void ApplyConfig_KeepsIdleSelectedLinkedItem()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(itemId, "Write tests"),
            ]);
        var viewModel = context.ViewModel;

        viewModel.Pomodoro.SelectedLinkedItemId = itemId;

        viewModel.Pomodoro.ApplyConfig(new PomodoroConfig(
            FocusMinutes: 30,
            ShortBreakMinutes: 6,
            LongBreakMinutes: 20,
            LongBreakEvery: 3,
            AutoStartNextPhase: false));

        Assert.Equal(itemId, viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Equal("待关联事项：Write tests", viewModel.Pomodoro.LinkedItemText);
    }

    [Fact]
    public void RolloverAndRefresh_KeepsLinkedItemWhenItemStillExists()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            lastResetDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            items:
            [
                new RhythmItem(itemId, "Daily focus"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1200,
                0,
                0,
                itemId,
                DateTimeOffset.Now.AddMinutes(-5),
                DateTimeOffset.Now.AddMinutes(-5)));
        var viewModel = context.ViewModel;

        viewModel.RolloverAndRefresh();

        Assert.Equal(itemId, viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Equal(itemId, context.State.PomodoroSession.LinkedItemId);
        Assert.Equal("关联事项：Daily focus", viewModel.Pomodoro.LinkedItemText);
    }

    [Fact]
    public void RolloverAndRefresh_ResetsCompletedFocusCountToday()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            lastResetDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            items:
            [
                new RhythmItem(itemId, "Daily focus"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1200,
                2,
                3,
                itemId,
                DateTimeOffset.Now.AddMinutes(-5),
                DateTimeOffset.Now.AddMinutes(-5)));
        var viewModel = context.ViewModel;

        viewModel.RolloverAndRefresh();

        Assert.Equal(0, context.State.PomodoroSession.CompletedFocusCountToday);
        Assert.Equal("本轮 2/4，今日 0 个番茄钟", viewModel.Pomodoro.CycleText);
    }

    [Fact]
    public void RemoveItem_ClearsLinkedItemIdFromState()
    {
        var linkedItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(linkedItemId, "Current focus"),
                new RhythmItem(otherItemId, "Other"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                900,
                0,
                0,
                linkedItemId,
                DateTimeOffset.Now.AddMinutes(-10),
                DateTimeOffset.Now.AddMinutes(-10)));
        var viewModel = context.ViewModel;
        var itemViewModel = viewModel.Items.Single(item => item.Id == linkedItemId);

        viewModel.RemoveItem(itemViewModel);

        Assert.Null(context.State.PomodoroSession.LinkedItemId);
    }

    [Fact]
    public void PrepareForItemRemoval_ClearsLinksBeforeItemIsRemoved()
    {
        var linkedItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(linkedItemId, "Current focus"),
                new RhythmItem(otherItemId, "Other"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                900,
                0,
                0,
                linkedItemId,
                DateTimeOffset.Now.AddMinutes(-10),
                DateTimeOffset.Now.AddMinutes(-10)));
        var viewModel = context.ViewModel;

        viewModel.Pomodoro.PrepareForItemRemoval(linkedItemId);

        Assert.Null(context.State.PomodoroSession.LinkedItemId);
        Assert.Null(viewModel.Pomodoro.SelectedLinkedItemId);
        Assert.Contains(context.State.Items, item => item.Id == linkedItemId);
        Assert.Contains(viewModel.Items, item => item.Id == linkedItemId);
    }

    [Fact]
    public void RolloverAndRefresh_ClearsLinkedItemIdWhenCompletedOneTimeItemIsRemoved()
    {
        var linkedItemId = Guid.NewGuid();
        var keepItemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            lastResetDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            items:
            [
                new RhythmItem(linkedItemId, "One-time task", RhythmItemKind.OneTime),
                new RhythmItem(keepItemId, "Daily task"),
            ],
            completedToday:
            [
                linkedItemId,
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                300,
                0,
                0,
                linkedItemId,
                DateTimeOffset.Now.AddMinutes(-20),
                DateTimeOffset.Now.AddMinutes(-20)));
        var viewModel = context.ViewModel;

        viewModel.RolloverAndRefresh();

        Assert.Null(context.State.PomodoroSession.LinkedItemId);
        Assert.DoesNotContain(viewModel.Items, item => item.Id == linkedItemId);
    }

    [Fact]
    public void Constructor_NormalizesInvalidStatusToIdle()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(itemId, "Write tests"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                (PomodoroStatus)999,
                PomodoroPhaseType.ShortBreak,
                42,
                1,
                2,
                itemId,
                DateTimeOffset.Now.AddMinutes(-3),
                DateTimeOffset.Now.AddMinutes(-1)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal(PomodoroStatus.Idle, pomodoro.Status);
        Assert.Equal(PomodoroPhaseType.Focus, pomodoro.PhaseType);
        Assert.True(pomodoro.IsIdle);
        Assert.True(pomodoro.CanStartOrResume);
        Assert.Equal(PomodoroStatus.Idle, context.State.PomodoroSession.Status);
    }

    [Fact]
    public void CompactStripTexts_ShowIdleSummaryWhenSessionIsIdle()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 30, ShortBreakMinutes: 5, LongBreakMinutes: 15, LongBreakEvery: 4, AutoStartNextPhase: true));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal("准备专注", pomodoro.CompactPhaseLabel);
        Assert.Equal("可开始 30 分钟", pomodoro.CompactContextText);
        Assert.Equal("开始", pomodoro.PrimaryActionText);
        Assert.Equal("Idle", pomodoro.CompactToneKey);
        Assert.False(pomodoro.IsExpanded);
    }

    [Fact]
    public void ProgressProjection_ShowsFullRingWhenSessionIsIdle()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 30));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal(1800, pomodoro.PhaseTotalSeconds);
        Assert.Equal(1.0, pomodoro.ProgressRatio, precision: 5);
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
    public void ProgressProjection_UsesRemainingFocusSecondsWhenRunning()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25),
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                750,
                0,
                0,
                null,
                DateTimeOffset.Now.AddMinutes(-12),
                DateTimeOffset.Now.AddMinutes(-12)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal(1500, pomodoro.PhaseTotalSeconds);
        Assert.Equal(0.5, pomodoro.ProgressRatio, precision: 5);
    }

    [Fact]
    public void CompactStripTexts_ShowUnlinkedSummaryDuringRunningFocus()
    {
        var context = CreateMainViewModel(
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1452,
                0,
                0,
                null,
                DateTimeOffset.Now.AddMinutes(-1),
                DateTimeOffset.Now.AddMinutes(-1)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal("专注中", pomodoro.CompactPhaseLabel);
        Assert.Equal("未关联事项", pomodoro.CompactContextText);
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
    public void ProgressProjection_UsesRemainingSecondsWhenPaused()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25),
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Paused,
                PomodoroPhaseType.Focus,
                375,
                1,
                2,
                null,
                DateTimeOffset.Now.AddMinutes(-10),
                DateTimeOffset.Now.AddMinutes(-4)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal(1500, pomodoro.PhaseTotalSeconds);
        Assert.Equal(0.25, pomodoro.ProgressRatio, precision: 5);
    }

    [Fact]
    public void CompactStripTexts_ShowCompletedSummaryDuringRunningShortBreak()
    {
        var context = CreateMainViewModel(
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.ShortBreak,
                240,
                1,
                2,
                null,
                DateTimeOffset.Now.AddMinutes(-1),
                DateTimeOffset.Now.AddMinutes(-1)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal("短休息", pomodoro.CompactPhaseLabel);
        Assert.Equal("上一轮已完成", pomodoro.CompactContextText);
        Assert.Equal("暂停", pomodoro.PrimaryActionText);
        Assert.Equal("ShortBreak", pomodoro.CompactToneKey);
    }

    [Fact]
    public void ProgressProjection_UsesShortBreakDuration()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5),
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.ShortBreak,
                240,
                1,
                2,
                null,
                DateTimeOffset.Now.AddMinutes(-1),
                DateTimeOffset.Now.AddMinutes(-1)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal(300, pomodoro.PhaseTotalSeconds);
        Assert.Equal(0.8, pomodoro.ProgressRatio, precision: 5);
    }

    [Fact]
    public void CompactStripTexts_ShowCycleSummaryDuringRunningLongBreak()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5, LongBreakMinutes: 20, LongBreakEvery: 4, AutoStartNextPhase: true),
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.LongBreak,
                600,
                0,
                4,
                null,
                DateTimeOffset.Now.AddMinutes(-1),
                DateTimeOffset.Now.AddMinutes(-1)));
        var pomodoro = context.ViewModel.Pomodoro;

        Assert.Equal("长休息", pomodoro.CompactPhaseLabel);
        Assert.Equal("已完成 4 轮", pomodoro.CompactContextText);
        Assert.Equal("暂停", pomodoro.PrimaryActionText);
        Assert.Equal("LongBreak", pomodoro.CompactToneKey);
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

    [Fact]
    public void ExecutePrimaryAction_ResumesWhenPaused()
    {
        var pausedContext = CreateMainViewModel(
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Paused,
                PomodoroPhaseType.Focus,
                900,
                0,
                0,
                null,
                DateTimeOffset.Now.AddMinutes(-10),
                DateTimeOffset.Now.AddMinutes(-2)));

        pausedContext.ViewModel.Pomodoro.ExecutePrimaryAction();

        Assert.Equal(PomodoroStatus.Running, pausedContext.ViewModel.Pomodoro.Status);
    }

    [Fact]
    public void CompleteCurrentPhase_CountsFocusAndTransitionsToLongBreak()
    {
        var context = CreateMainViewModel(
            pomodoroConfig: new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5, LongBreakMinutes: 15, LongBreakEvery: 4),
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                900,
                3,
                7,
                Guid.NewGuid(),
                DateTimeOffset.Now.AddMinutes(-10),
                DateTimeOffset.Now.AddMinutes(-10)));

        context.ViewModel.Pomodoro.CompleteCurrentPhase();

        Assert.Equal(PomodoroPhaseType.LongBreak, context.ViewModel.Pomodoro.PhaseType);
        Assert.Equal(0, context.State.PomodoroSession.CompletedFocusCountInCycle);
        Assert.Equal(8, context.State.PomodoroSession.CompletedFocusCountToday);
    }

    [Fact]
    public void RenameItem_RefreshesCompactContextTextForLinkedRunningSession()
    {
        var itemId = Guid.NewGuid();
        var context = CreateMainViewModel(
            items:
            [
                new RhythmItem(itemId, "Old focus"),
            ],
            pomodoroSession: new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1200,
                0,
                0,
                itemId,
                DateTimeOffset.Now.AddMinutes(-5),
                DateTimeOffset.Now.AddMinutes(-5)));
        var viewModel = context.ViewModel;
        var pomodoro = viewModel.Pomodoro;
        var itemViewModel = viewModel.Items.Single(item => item.Id == itemId);
        var changedProperties = new List<string?>();
        pomodoro.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.Equal("Old focus", pomodoro.CompactContextText);

        viewModel.RenameItem(itemViewModel, "New focus");

        Assert.Equal("New focus", pomodoro.CompactContextText);
        Assert.Contains(nameof(PomodoroViewModel.CompactContextText), changedProperties);
    }

    [Fact]
    public void ShouldNotifyPomodoroPhaseChange_DoesNotNotifyWhenManuallyStartingFromIdle()
    {
        var shouldNotify = InvokeShouldNotifyPomodoroPhaseChange(
            phase: PomodoroPhaseType.Focus,
            status: PomodoroStatus.Running,
            previousPhase: PomodoroPhaseType.Focus,
            previousStatus: PomodoroStatus.Idle,
            completedFocusCountToday: 0,
            previousCompletedFocusCountToday: 0);

        Assert.False(shouldNotify);
    }

    [Fact]
    public void ShouldNotifyPomodoroPhaseChange_NotifiesWhenCompletedCountIncreasesWithoutPhaseChange()
    {
        var shouldNotify = InvokeShouldNotifyPomodoroPhaseChange(
            phase: PomodoroPhaseType.Focus,
            status: PomodoroStatus.Running,
            previousPhase: PomodoroPhaseType.Focus,
            previousStatus: PomodoroStatus.Running,
            completedFocusCountToday: 3,
            previousCompletedFocusCountToday: 2);

        Assert.True(shouldNotify);
    }

    public void Dispose()
    {
        try
        {
            if (_hadOriginalStateFile)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
                File.WriteAllText(_statePath, _originalStateContent!);
            }
            else if (File.Exists(_statePath))
            {
                File.Delete(_statePath);
            }
        }
        catch
        {
        }
    }

    private static (MainViewModel ViewModel, RhythmState State) CreateMainViewModel(
        DateOnly? lastResetDate = null,
        IReadOnlyList<RhythmItem>? items = null,
        IReadOnlyList<Guid>? completedToday = null,
        PomodoroConfig? pomodoroConfig = null,
        PomodoroSessionSnapshot? pomodoroSession = null)
    {
        var state = new RhythmState(new StateDocument
        {
            LastResetDate = lastResetDate ?? DateOnly.FromDateTime(DateTime.Today),
            Items = items?.ToList() ?? [],
            CompletedToday = completedToday?.ToList() ?? [],
            PomodoroConfig = pomodoroConfig ?? new PomodoroConfig(),
            PomodoroSession = pomodoroSession ?? new PomodoroSessionSnapshot(),
        });

        return (new MainViewModel(new RhythmStateService(state)), state);
    }

    private static bool InvokeShouldNotifyPomodoroPhaseChange(
        PomodoroPhaseType phase,
        PomodoroStatus status,
        PomodoroPhaseType? previousPhase,
        PomodoroStatus? previousStatus,
        int? completedFocusCountToday,
        int? previousCompletedFocusCountToday)
    {
        var method = typeof(App).GetMethod(
            "ShouldNotifyPomodoroPhaseChange",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (bool)method!.Invoke(
            null,
            [
                phase,
                status,
                previousPhase,
                previousStatus,
                completedFocusCountToday,
                previousCompletedFocusCountToday,
            ])!;
    }
}
