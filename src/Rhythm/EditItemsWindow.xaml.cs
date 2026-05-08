using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rhythm.UI;

namespace Rhythm;

public partial class EditItemsWindow : Window
{
    private readonly MainViewModel _vm;

    public EditItemsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        ItemsList.ItemsSource = _vm.Items;
    }

    private ItemViewModel? Selected => ItemsList.SelectedItem as ItemViewModel;

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } v) return;
        var text = EditTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _vm.RenameItem(v, text);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (Selected is { } v) _vm.MoveItem(v, -1);
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (Selected is { } v) _vm.MoveItem(v, +1);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Selected is { } v) _vm.RemoveItem(v);
    }

    private void OnAdd(object sender, RoutedEventArgs e) => DoAdd();

    private void OnNewItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DoAdd();
    }

    private void DoAdd()
    {
        var text = NewItemText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        _vm.AddItem(text);
        NewItemText.Clear();
    }
}
