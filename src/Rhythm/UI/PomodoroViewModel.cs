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

    public PomodoroViewModel(
        RhythmStateService service,
        ObservableCollection<ItemViewModel> availableItems,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        AvailableItems = availableItems ?? throw new ArgumentNullException(nameof(availableItems));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

        var config = NormalizeConfig(_service.State.PomodoroConfig);
        var session = NormalizeSession(config, _service.State.PomodoroSession);
        _machine = new PomodoroStateMachine(config, session);
        _selectedLinkedItemId = session.LinkedItemId;

        if (config != _service.State.PomodoroConfig || session != _service.State.PomodoroSession)
            PersistMachineState();

        RefreshBindings();
    }

    public ObservableCollection<ItemViewModel> AvailableItems { get; }

    public PomodoroConfig Config => _machine.Config;

    public PomodoroStatus Status => _machine.Session.Status;

    public PomodoroPhaseType PhaseType => _machine.Session.PhaseType;

    public string PhaseLabel => Status switch
    {
        PomodoroStatus.Idle => "准备开始专注",
        PomodoroStatus.Paused => PhaseType switch
        {
            PomodoroPhaseType.Focus => "专注已暂停",
            PomodoroPhaseType.ShortBreak => "短休息已暂停",
            PomodoroPhaseType.LongBreak => "长休息已暂停",
            _ => "已暂停",
        },
        PomodoroStatus.Running => PhaseType switch
        {
            PomodoroPhaseType.Focus => "专注中",
            PomodoroPhaseType.ShortBreak => "短休息中",
            PomodoroPhaseType.LongBreak => "长休息中",
            _ => "专注中",
        },
        _ => "专注中",
    };

    public string RemainingText => FormatRemaining(_machine.Session.RemainingSeconds);

    public string CycleText =>
        $"本轮 {Math.Min(_machine.Session.CompletedFocusCountInCycle, Math.Max(1, Config.LongBreakEvery))}/{Math.Max(1, Config.LongBreakEvery)}，今日 {_machine.Session.CompletedFocusCountToday} 个番茄";

    public string LinkedItemText => ResolveLinkedItemText();

    public bool IsIdle => Status == PomodoroStatus.Idle;

    public bool IsRunning => Status == PomodoroStatus.Running;

    public bool IsPaused => Status == PomodoroStatus.Paused;

    public bool CanStartOrResume => IsIdle || IsPaused;

    public bool CanPause => IsRunning;

    public bool CanSkip => !IsIdle;

    public bool CanReset => !IsIdle || _machine.Session.CompletedFocusCountToday > 0;

    public Guid? SelectedLinkedItemId
    {
        get => _selectedLinkedItemId;
        set
        {
            if (_selectedLinkedItemId == value)
                return;

            _selectedLinkedItemId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinkedItemText));
        }
    }

    public void StartOrResume()
    {
        var previousSession = _machine.Session;

        if (IsPaused)
        {
            _machine.Resume(_nowProvider());
        }
        else
        {
            _machine.Start(_nowProvider(), ResolveValidLinkedItemId(SelectedLinkedItemId));
        }

        SyncSelectedLinkedItemIdFromSession();
        PersistAndNotifyIfChanged(previousSession);
    }

    public void Pause()
    {
        var previousSession = _machine.Session;
        _machine.Pause(_nowProvider());
        PersistAndNotifyIfChanged(previousSession);
    }

    public void SkipCurrentPhase()
    {
        var previousSession = _machine.Session;
        _machine.SkipCurrentPhase(_nowProvider());
        SyncSelectedLinkedItemIdFromSession();
        PersistAndNotifyIfChanged(previousSession);
    }

    public void Reset()
    {
        var previousSession = _machine.Session;
        _machine.Reset();
        SyncSelectedLinkedItemIdFromSession();
        PersistAndNotifyIfChanged(previousSession);
    }

    public void ApplyConfig(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var normalizedConfig = NormalizeConfig(config);
        var adjustedSession = NormalizeSession(normalizedConfig, _machine.Session);
        var previousConfig = _machine.Config;
        var previousSession = _machine.Session;
        var previousSelectedLinkedItemId = _selectedLinkedItemId;

        _machine = new PomodoroStateMachine(normalizedConfig, adjustedSession);
        if (adjustedSession.Status == PomodoroStatus.Idle)
            SetSelectedLinkedItemId(ResolveValidLinkedItemId(previousSelectedLinkedItemId));
        else
            SyncSelectedLinkedItemIdFromSession();
        PersistAndNotifyIfChanged(previousConfig, previousSession);
    }

    public bool AdvanceToNow()
    {
        var previousSession = _machine.Session;
        _machine.AdvanceTo(_nowProvider());
        SyncSelectedLinkedItemIdFromSession();
        return PersistAndNotifyIfChanged(previousSession);
    }

    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(LinkedItemText));
    }

    public bool SynchronizeLinkedItemAvailability()
    {
        var sessionChanged = false;

        if (_machine.Session.LinkedItemId is { } linkedItemId && !HasAvailableItem(linkedItemId))
        {
            _machine = new PomodoroStateMachine(
                _machine.Config,
                _machine.Session with
                {
                    LinkedItemId = null,
                });
            sessionChanged = true;
        }

        var normalizedSelectedLinkedItemId = ResolveValidLinkedItemId(_selectedLinkedItemId);
        var selectedChanged = SetSelectedLinkedItemId(normalizedSelectedLinkedItemId);

        if (sessionChanged)
            _service.State.SetPomodoroSession(_machine.Session);

        if (sessionChanged)
            OnPropertyChanged(string.Empty);
        else if (selectedChanged)
            OnPropertyChanged(nameof(LinkedItemText));

        return sessionChanged || selectedChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static PomodoroConfig NormalizeConfig(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config with
        {
            FocusMinutes = Math.Max(1, config.FocusMinutes),
            ShortBreakMinutes = Math.Max(1, config.ShortBreakMinutes),
            LongBreakMinutes = Math.Max(1, config.LongBreakMinutes),
            LongBreakEvery = Math.Max(1, config.LongBreakEvery),
        };
    }

    private static PomodoroSessionSnapshot NormalizeSession(PomodoroConfig config, PomodoroSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status == PomodoroStatus.Idle)
        {
            return session with
            {
                Status = PomodoroStatus.Idle,
                PhaseType = PomodoroPhaseType.Focus,
                RemainingSeconds = GetPhaseDurationSeconds(config, PomodoroPhaseType.Focus),
                LinkedItemId = null,
                PhaseStartedAt = null,
                LastUpdatedAt = null,
            };
        }

        var phaseType = Enum.IsDefined(session.PhaseType) ? session.PhaseType : PomodoroPhaseType.Focus;
        var remainingSeconds = session.RemainingSeconds;
        var phaseDurationSeconds = GetPhaseDurationSeconds(config, phaseType);

        if (remainingSeconds <= 0)
            remainingSeconds = phaseDurationSeconds;
        else
            remainingSeconds = Math.Min(remainingSeconds, phaseDurationSeconds);

        return session with
        {
            PhaseType = phaseType,
            RemainingSeconds = remainingSeconds,
        };
    }

    private static int GetPhaseDurationSeconds(PomodoroConfig config, PomodoroPhaseType phaseType) => phaseType switch
    {
        PomodoroPhaseType.Focus => config.FocusMinutes * 60,
        PomodoroPhaseType.ShortBreak => config.ShortBreakMinutes * 60,
        PomodoroPhaseType.LongBreak => config.LongBreakMinutes * 60,
        _ => config.FocusMinutes * 60,
    };

    private static string FormatRemaining(int remainingSeconds)
    {
        var safeSeconds = Math.Max(0, remainingSeconds);
        var remaining = TimeSpan.FromSeconds(safeSeconds);
        return remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private string ResolveLinkedItemText()
    {
        if (_machine.Session.LinkedItemId is { } activeLinkedItemId)
        {
            var item = AvailableItems.FirstOrDefault(candidate => candidate.Id == activeLinkedItemId);
            return item is null ? "关联事项：未关联事项" : $"关联事项：{item.Text}";
        }

        if (IsIdle && SelectedLinkedItemId is { } selectedLinkedItemId)
        {
            var item = AvailableItems.FirstOrDefault(candidate => candidate.Id == selectedLinkedItemId);
            return item is null ? "关联事项：未关联事项" : $"待关联事项：{item.Text}";
        }

        return "关联事项：未关联事项";
    }

    private Guid? ResolveValidLinkedItemId(Guid? itemId)
        => itemId is { } value && HasAvailableItem(value) ? value : null;

    private bool HasAvailableItem(Guid itemId)
        => AvailableItems.Any(item => item.Id == itemId);

    private void SyncSelectedLinkedItemIdFromSession()
    {
        SetSelectedLinkedItemId(ResolveValidLinkedItemId(_machine.Session.LinkedItemId));
    }

    private bool SetSelectedLinkedItemId(Guid? itemId)
    {
        if (_selectedLinkedItemId == itemId)
            return false;

        _selectedLinkedItemId = itemId;
        OnPropertyChanged(nameof(SelectedLinkedItemId));
        return true;
    }

    private bool PersistAndNotifyIfChanged(PomodoroSessionSnapshot previousSession)
        => PersistAndNotifyIfChanged(_machine.Config, previousSession);

    private bool PersistAndNotifyIfChanged(PomodoroConfig previousConfig, PomodoroSessionSnapshot previousSession)
    {
        if (previousConfig == _machine.Config && previousSession == _machine.Session)
            return false;

        PersistMachineState();
        OnPropertyChanged(string.Empty);
        return true;
    }

    private void PersistMachineState()
    {
        _service.State.SetPomodoroConfig(_machine.Config);
        _service.State.SetPomodoroSession(_machine.Session);
        _service.Persist();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
