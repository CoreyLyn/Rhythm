using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Rhythm;

public partial class AboutWindow : Window
{
    private readonly bool _enableTransparency;

    public AboutWindow(bool enableTransparency = true)
    {
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
        }

        InitializeComponent();
        DataContext = new AboutWindowViewModel();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set border background based on transparency mode
        if (!_enableTransparency)
        {
            RootBorder.Background = (SolidColorBrush)FindResource("SolidWindowBackgroundBrush");
        }
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

public sealed class AboutWindowViewModel
{
    public string VersionText => "Rhythm v1";
}
