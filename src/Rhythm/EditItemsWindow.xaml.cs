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

    private void OnAdd(object sender, RoutedEventArgs e) => DoAdd();

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

    private void OnKindBadgeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
        {
            var newKind = itemVm.Kind == RhythmItemKind.Daily
                ? RhythmItemKind.OneTime
                : RhythmItemKind.Daily;
            _vm.ChangeKind(itemVm, newKind);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
        {
            _vm.RemoveItem(itemVm);
        }
    }

    private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<Button>(source) != null) return;
        if (sender is not ListBoxItem listItem) return;
        if (listItem.DataContext is not ItemViewModel itemVm) return;
        if (itemVm.IsEditing) return;

        itemVm.IsEditing = true;
        e.Handled = true;
    }

    private void OnEditTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;
        if (!itemVm.IsEditing) return;
        tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            tb.Focus();
            tb.SelectAll();
        });
    }

    private void OnEditTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;

        if (e.Key == Key.Enter)
        {
            CommitInlineEdit(itemVm, tb);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tb.Text = itemVm.Text;
            itemVm.IsEditing = false;
            e.Handled = true;
        }
    }

    private void OnEditTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;
        if (!itemVm.IsEditing) return;
        CommitInlineEdit(itemVm, tb);
    }

    private void CommitInlineEdit(ItemViewModel itemVm, TextBox tb)
    {
        var newText = tb.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(newText) && newText != itemVm.Text)
        {
            _vm.RenameItem(itemVm, newText);
        }
        else
        {
            tb.Text = itemVm.Text;
        }
        itemVm.IsEditing = false;
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
