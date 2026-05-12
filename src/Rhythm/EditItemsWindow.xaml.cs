using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class EditItemsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly bool _enableTransparency;

    public EditItemsWindow(MainViewModel vm, bool enableTransparency = true)
    {
        _vm = vm;
        _enableTransparency = enableTransparency;

        // Set window properties before InitializeComponent
        if (enableTransparency)
        {
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }
        else
        {
            AllowsTransparency = false;
            Background = (SolidColorBrush)Application.Current.FindResource("SolidWindowBackgroundBrush");

            // Use WindowChrome for rounded corners in non-transparent mode
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(5),
                CaptionHeight = 0
            });
        }

        InitializeComponent();
        DataContext = _vm;
        ItemsList.ItemsSource = _vm.Items;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set border background based on transparency mode
        if (!_enableTransparency)
        {
            SolidWindowChrome.Apply(this, RootBorder, 12);
        }
    }

    private ItemViewModel? Selected => ItemsList.SelectedItem as ItemViewModel;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EditTextBox.Text = Selected?.Text ?? string.Empty;
        RefreshSelectionActions();
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } v) return;
        var idx = ItemsList.SelectedIndex;
        var text = EditTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _vm.RenameItem(v, text);
        if (idx >= 0 && idx < _vm.Items.Count)
        {
            var renamed = _vm.Items[idx];
            ItemsList.SelectedItem = renamed;
            ItemsList.ScrollIntoView(renamed);
        }

        RefreshSelectionActions();
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        // Replaced in Task 5
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        // Replaced in Task 5
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } v) return;

        var idx = ItemsList.SelectedIndex;
        _vm.RemoveItem(v);

        if (_vm.Items.Count > 0)
            ItemsList.SelectedIndex = Math.Min(idx, _vm.Items.Count - 1);

        RefreshSelectionActions();
    }

    private void OnAdd(object sender, RoutedEventArgs e) => DoAdd();

    private void OnEditItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        OnRename(sender, e);
        e.Handled = true;
    }

    private void OnNewItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        DoAdd();
        e.Handled = true;
    }

    private void DoAdd()
    {
        var text = NewItemText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        var kind = OneTimeItemRadio.IsChecked == true ? RhythmItemKind.OneTime : RhythmItemKind.Daily;
        var added = _vm.AddItem(text, kind);
        NewItemText.Clear();
        ItemsList.SelectedItem = added;
        ItemsList.ScrollIntoView(added);
        EditTextBox.Focus();
        EditTextBox.SelectAll();
        RefreshSelectionActions();
    }

    private void RefreshSelectionActions()
    {
        var selectedIndex = ItemsList.SelectedIndex;
        var hasSelection = selectedIndex >= 0;

        EditTextBox.IsEnabled = hasSelection;
        RenameButton.IsEnabled = hasSelection;
        RemoveButton.IsEnabled = hasSelection;
        MoveUpButton.IsEnabled = hasSelection && selectedIndex > 0;
        MoveDownButton.IsEnabled = hasSelection && selectedIndex < _vm.Items.Count - 1;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        Close();
        e.Handled = true;
    }

    private void OnWindowChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<Button>(source) != null) return;
        if (FindAncestor<TextBox>(source) != null) return;
        if (FindAncestor<ListBoxItem>(source) != null) return;
        if (FindAncestor<ScrollBar>(source) != null) return;
        if (FindAncestor<ResizeGrip>(source) != null) return;

        DragMove();
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
