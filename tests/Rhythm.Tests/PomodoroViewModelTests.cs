using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
}
