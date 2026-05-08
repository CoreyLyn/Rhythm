using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class ItemViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;
    private bool _isCompleted;

    public Guid Id { get; }
    public string Text { get; }

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

    public ItemViewModel(RhythmStateService service, RhythmItem item)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(item);
        _service = service;
        Id = item.Id;
        Text = item.Text;
        _isCompleted = service.State.CompletedToday.Contains(item.Id);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
