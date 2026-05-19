using System;
using System.Linq;
using Rhythm.State;
using Xunit;

namespace Rhythm.Tests;

public class RhythmStateTests
{
    private static RhythmState NewState() => new(new StateDocument
    {
        LastResetDate = new DateOnly(2026, 5, 8),
    });

    [Fact]
    public void RolloverIfNeeded_ClearsCompletedWhenDayAdvances()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        Assert.Single(state.CompletedToday);

        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.True(changed);
        Assert.Empty(state.CompletedToday);
        Assert.Equal(new DateOnly(2026, 5, 9), state.LastResetDate);
    }

    [Fact]
    public void RolloverIfNeeded_KeepsCompletedDailyItems()
    {
        var state = NewState();
        var daily = state.AddItem("Daily", RhythmItemKind.Daily);
        state.ToggleItem(daily.Id);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Contains(state.Items, i => i.Id == daily.Id);
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_RemovesCompletedOneTimeItems()
    {
        var state = NewState();
        var daily = state.AddItem("Daily", RhythmItemKind.Daily);
        var oneTime = state.AddItem("One time", RhythmItemKind.OneTime);
        state.ToggleItem(daily.Id);
        state.ToggleItem(oneTime.Id);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Equal(new[] { daily.Id }, state.Items.Select(i => i.Id).ToArray());
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_KeepsIncompleteOneTimeItems()
    {
        var state = NewState();
        var oneTime = state.AddItem("One time", RhythmItemKind.OneTime);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Contains(state.Items, i => i.Id == oneTime.Id);
    }

    [Fact]
    public void RolloverIfNeeded_IsIdempotentOnSameDay()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.RolloverIfNeeded(new DateOnly(2026, 5, 8));
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 8));

        Assert.False(changed);
        Assert.Single(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_NoOpWhenTodayEqualsLastReset()
    {
        var state = NewState();
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 8));
        Assert.False(changed);
    }

    [Fact]
    public void RolloverIfNeeded_NoOpWhenTodayEarlierThanLastReset()
    {
        var state = NewState();
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 7));
        Assert.False(changed);
        Assert.Equal(new DateOnly(2026, 5, 8), state.LastResetDate);
    }

    [Fact]
    public void ToggleItem_FlipsBothWays()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        Assert.Contains(a.Id, state.CompletedToday);
        state.ToggleItem(a.Id);
        Assert.DoesNotContain(a.Id, state.CompletedToday);
    }

    [Fact]
    public void ToggleItem_ThrowsForUnknownId()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.ToggleItem(Guid.NewGuid()));
    }

    [Fact]
    public void AddItem_RejectsEmptyOrWhitespace()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.AddItem(""));
        Assert.Throws<ArgumentException>(() => state.AddItem("   "));
        Assert.Throws<ArgumentException>(() => state.AddItem("\t\n"));
    }

    [Fact]
    public void AddItem_TrimsAndAssignsUniqueGuid()
    {
        var state = NewState();
        var a = state.AddItem("  hello ");
        var b = state.AddItem("world", RhythmItemKind.OneTime);
        Assert.Equal("hello", a.Text);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(RhythmItemKind.Daily, a.Kind);
        Assert.Equal(RhythmItemKind.OneTime, b.Kind);
        Assert.Equal(2, state.Items.Count);
    }

    [Fact]
    public void AddItem_RejectsUnknownKind()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.AddItem("A", (RhythmItemKind)999));
    }

    [Fact]
    public void RemoveItem_RemovesFromItemsAndCompleted()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.RemoveItem(a.Id);
        Assert.Empty(state.Items);
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RemoveItem_ThrowsForUnknownId()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.RemoveItem(Guid.NewGuid()));
    }

    [Fact]
    public void RenameItem_UpdatesText()
    {
        var state = NewState();
        var a = state.AddItem("old");
        state.RenameItem(a.Id, " new ");
        Assert.Equal("new", state.Items.Single(i => i.Id == a.Id).Text);
    }

    [Fact]
    public void RenameItem_RejectsEmpty()
    {
        var state = NewState();
        var a = state.AddItem("A");
        Assert.Throws<ArgumentException>(() => state.RenameItem(a.Id, ""));
        Assert.Throws<ArgumentException>(() => state.RenameItem(a.Id, "   "));
    }

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

    [Fact]
    public void ToDocument_RoundtripsMutations()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.SetWindowPos(new WindowPos(10, 20, 300, 400, "DISPLAY1"));
        state.SetEnableTransparency(false);
        var doc = state.ToDocument();

        Assert.Equal(StateDocument.CurrentSchemaVersion, doc.SchemaVersion);
        Assert.Single(doc.Items);
        Assert.Equal(RhythmItemKind.Daily, doc.Items[0].Kind);
        Assert.Single(doc.CompletedToday);
        Assert.Equal("DISPLAY1", doc.WindowPos!.ScreenDeviceName);
        Assert.False(doc.EnableTransparency);
    }

    [Fact]
    public void ToDocument_RoundtripsPomodoroState()
    {
        var linkedId = Guid.NewGuid();
        var initialConfig = new PomodoroConfig(30, 6, 18, 3, false);
        var initialSession = new PomodoroSessionSnapshot(
            PomodoroStatus.Running,
            PomodoroPhaseType.Focus,
            900,
            2,
            5,
            linkedId,
            new DateTimeOffset(2026, 5, 19, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 19, 9, 10, 0, TimeSpan.Zero));
        var state = new RhythmState(new StateDocument
        {
            LastResetDate = new DateOnly(2026, 5, 8),
            PomodoroConfig = initialConfig,
            PomodoroSession = initialSession,
        });

        Assert.Equal(initialConfig, state.PomodoroConfig);
        Assert.Equal(initialSession, state.PomodoroSession);

        var updatedConfig = new PomodoroConfig(25, 5, 15, 4, true);
        var updatedSession = new PomodoroSessionSnapshot(
            PomodoroStatus.Paused,
            PomodoroPhaseType.ShortBreak,
            120,
            3,
            6,
            linkedId,
            new DateTimeOffset(2026, 5, 19, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 19, 9, 33, 0, TimeSpan.Zero));

        state.SetPomodoroConfig(updatedConfig);
        state.SetPomodoroSession(updatedSession);

        var doc = state.ToDocument();

        Assert.Equal(updatedConfig, doc.PomodoroConfig);
        Assert.Equal(updatedSession, doc.PomodoroSession);
    }

    [Fact]
    public void Constructor_FallsBackWhenCollectionsAndPomodoroFieldsAreNull()
    {
        var state = new RhythmState(new StateDocument
        {
            LastResetDate = new DateOnly(2026, 5, 8),
            Items = null!,
            CompletedToday = null!,
            PomodoroConfig = null!,
            PomodoroSession = null!,
        });

        Assert.Empty(state.Items);
        Assert.Empty(state.CompletedToday);
        Assert.Equal(new PomodoroConfig(), state.PomodoroConfig);
        Assert.Equal(new PomodoroSessionSnapshot(), state.PomodoroSession);

        var doc = state.ToDocument();

        Assert.NotNull(doc.Items);
        Assert.NotNull(doc.CompletedToday);
        Assert.NotNull(doc.PomodoroConfig);
        Assert.NotNull(doc.PomodoroSession);
        Assert.Empty(doc.Items);
        Assert.Empty(doc.CompletedToday);
        Assert.Equal(new PomodoroConfig(), doc.PomodoroConfig);
        Assert.Equal(new PomodoroSessionSnapshot(), doc.PomodoroSession);
    }
}
