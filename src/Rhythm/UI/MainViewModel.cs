using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;

    public ObservableCollection<ItemViewModel> Items { get; }

    public string TodayLabel => DateTime.Today.ToString("yyyy-MM-dd ddd");

    public bool HasItems => Items.Count > 0;

    public WindowPos? WindowPos => _service.State.WindowPos;

    public MainViewModel(RhythmStateService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        Items = new ObservableCollection<ItemViewModel>(
            _service.State.Items.Select(i => new ItemViewModel(_service, i)));
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasItems));
    }

    public void UpdateWindowPos(WindowPos pos) => _service.State.SetWindowPos(pos);

    public void PersistWindowPos() => _service.Persist();

    public ItemViewModel AddItem(string text)
    {
        var item = _service.State.AddItem(text);
        var vm = new ItemViewModel(_service, item);
        Items.Add(vm);
        _service.Persist();
        return vm;
    }

    public void RemoveItem(ItemViewModel itemVm)
    {
        _service.State.RemoveItem(itemVm.Id);
        Items.Remove(itemVm);
        _service.Persist();
    }

    public void RenameItem(ItemViewModel itemVm, string newText)
    {
        var idx = Items.IndexOf(itemVm);
        if (idx < 0) return;
        _service.State.RenameItem(itemVm.Id, newText);
        var renamed = _service.State.Items[idx];
        Items[idx] = new ItemViewModel(_service, renamed);
        _service.Persist();
    }

    public void MoveItem(ItemViewModel itemVm, int delta)
    {
        var idx = Items.IndexOf(itemVm);
        if (idx < 0) return;
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= Items.Count) return;
        _service.State.MoveItem(itemVm.Id, delta);
        Items.Move(idx, newIdx);
        _service.Persist();
    }

    public void RolloverAndRefresh()
    {
        var changed = _service.State.RolloverIfNeeded(DateOnly.FromDateTime(DateTime.Today));
        if (!changed) return;

        _service.Persist();
        var snapshot = _service.State.Items.ToArray();
        Items.Clear();
        foreach (var item in snapshot)
            Items.Add(new ItemViewModel(_service, item));
        OnPropertyChanged(nameof(TodayLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
