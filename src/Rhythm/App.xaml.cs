using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Rhythm.Autostart;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _midnightTimer;
    private bool _isRecreatingWindow;

    public bool IsShuttingDown { get; private set; }

    public bool IsRecreatingWindow => _isRecreatingWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureAppDataDir();
        var firstLaunch = !File.Exists(AppPaths.StateFilePath);

        var doc = StateStore.Load();
        var state = new RhythmState(doc);
        state.RolloverIfNeeded(DateOnly.FromDateTime(DateTime.Today));

        if (firstLaunch)
        {
            state.AddItem("早起冥想 5 分钟");
            state.AddItem("运动 20 分钟");
            state.AddItem("读书 30 分钟");
        }

        StateStore.Save(state.ToDocument());

        var service = new RhythmStateService(state);
        _viewModel = new MainViewModel(service);
        _mainWindow = new MainWindow(state.EnableTransparency) { DataContext = _viewModel };
        _mainWindow.Show();

        BuildTrayIcon();
        ScheduleNextMidnight();
    }

    private void BuildTrayIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Resources/icon.ico");
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Rhythm",
            IconSource = new BitmapImage(iconUri),
        };

        var menu = new ContextMenu();
        var enableTransparency = _viewModel?.EnableTransparency ?? true;
        UpdateMenuBackground(menu, enableTransparency);
        var miShow = new MenuItem { Header = "显示窗口" };
        miShow.Click += (_, _) => ShowMainWindow();
        var miHide = new MenuItem { Header = "隐藏窗口" };
        miHide.Click += (_, _) => HideMainWindow();
        var miEdit = new MenuItem { Header = "编辑事项..." };
        miEdit.Click += (_, _) => OpenEditItems();
        var miAutostart = new MenuItem { IsCheckable = true, StaysOpenOnClick = true };
        RefreshAutostartMenuItem(miAutostart);
        menu.Opened += (_, _) => RefreshAutostartMenuItem(miAutostart);
        miAutostart.Click += (_, _) =>
        {
            try
            {
                AutostartManager.Set(miAutostart.IsChecked);
                RefreshAutostartMenuItem(miAutostart);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法修改自启动设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshAutostartMenuItem(miAutostart);
            }
        };

        var miTransparency = new MenuItem { IsCheckable = true, StaysOpenOnClick = true };
        RefreshTransparencyMenuItem(miTransparency);
        menu.Opened += (_, _) => RefreshTransparencyMenuItem(miTransparency);
        miTransparency.Click += (_, _) =>
        {
            if (_viewModel == null) return;
            try
            {
                var newValue = !miTransparency.IsChecked;
                _viewModel.SetEnableTransparency(newValue);
                miTransparency.IsChecked = newValue;
                RecreateMainWindow(newValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法修改透明设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshTransparencyMenuItem(miTransparency);
            }
        };

        var miAbout = new MenuItem { Header = "关于 Rhythm" };
        miAbout.Click += (_, _) => ShowAboutDialogAfterMenuCloses();
        var miExit = new MenuItem { Header = "退出" };
        miExit.Click += (_, _) => ShutdownApp();

        menu.Items.Add(miShow);
        menu.Items.Add(miHide);
        menu.Items.Add(miEdit);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAutostart);
        menu.Items.Add(miTransparency);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAbout);
        menu.Items.Add(miExit);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayLeftMouseDoubleClick += (_, _) => ShowMainWindow();
        _trayIcon.ForceCreate();
    }

    private static void RefreshAutostartMenuItem(MenuItem menuItem)
    {
        try
        {
            var isEnabled = AutostartManager.IsEnabled();
            menuItem.IsChecked = isEnabled;
            menuItem.Header = "开机启动";
        }
        catch (Exception ex)
        {
            menuItem.IsChecked = false;
            menuItem.Header = "开机启动（状态未知）";
            MessageBox.Show($"无法读取自启动设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshTransparencyMenuItem(MenuItem menuItem)
    {
        if (_viewModel == null)
        {
            menuItem.IsChecked = false;
            menuItem.Header = "透明效果（状态未知）";
            return;
        }
        menuItem.IsChecked = _viewModel.EnableTransparency;
        menuItem.Header = "透明效果";
    }

    private void RecreateMainWindow(bool enableTransparency)
    {
        if (_mainWindow == null || _viewModel == null) return;

        // Save current window position and size
        var oldLeft = _mainWindow.Left;
        var oldTop = _mainWindow.Top;
        var oldWidth = _mainWindow.Width;
        var oldHeight = _mainWindow.ActualHeight;

        // Set flag to allow window to close for recreation
        _isRecreatingWindow = true;

        // Close old window
        _mainWindow.Close();

        // Reset flag
        _isRecreatingWindow = false;

        // Create new window with updated transparency setting
        _mainWindow = new MainWindow(enableTransparency) { DataContext = _viewModel };
        _mainWindow.Left = oldLeft;
        _mainWindow.Top = oldTop;
        _mainWindow.Width = oldWidth;
        // Height is controlled by SizeToContent, so we restore it after load
        _mainWindow.Loaded += (_, _) =>
        {
            // Restore height if it differs from saved height (e.g., after manual resize)
            if (Math.Abs(_mainWindow.ActualHeight - oldHeight) > 1 && oldHeight > 0)
                _mainWindow.Height = oldHeight;
        };
        _mainWindow.Show();

        // Update tray menu background
        if (_trayIcon?.ContextMenu != null)
            UpdateMenuBackground(_trayIcon.ContextMenu, enableTransparency);
    }

    private void UpdateMenuBackground(ContextMenu menu, bool enableTransparency)
    {
        if (enableTransparency)
        {
            // Use transparent brush (let the style take over)
            menu.ClearValue(ContextMenu.BackgroundProperty);
        }
        else
        {
            // Use solid background
            menu.Background = (SolidColorBrush)Current.FindResource("SolidWindowBackgroundBrush");
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void HideMainWindow() => _mainWindow?.Hide();

    private void ShowAboutDialogAfterMenuCloses()
    {
        Dispatcher.BeginInvoke(new Action(ShowAboutDialog), DispatcherPriority.ContextIdle);
    }

    private void ShowAboutDialog()
    {
        var enableTransparency = _viewModel?.EnableTransparency ?? true;
        var aboutWindow = new AboutWindow(enableTransparency);
        if (_mainWindow?.IsVisible == true)
        {
            aboutWindow.Owner = _mainWindow;
        }
        else
        {
            aboutWindow.ShowInTaskbar = true;
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        aboutWindow.ShowDialog();
    }

    public void ShutdownFromUi() => ShutdownApp();

    public void OpenEditItems()
    {
        if (_viewModel == null) return;
        var enableTransparency = _viewModel.EnableTransparency;
        var w = new EditItemsWindow(_viewModel, enableTransparency)
        {
            Owner = _mainWindow?.IsVisible == true ? _mainWindow : null,
        };
        w.ShowDialog();
    }

    private void ShutdownApp()
    {
        IsShuttingDown = true;
        _midnightTimer?.Stop();
        _trayIcon?.Dispose();
        Shutdown();
    }

    private void ScheduleNextMidnight()
    {
        var now = DateTime.Now;
        var nextMidnight = now.Date.AddDays(1);
        var delta = nextMidnight - now;
        if (delta.TotalSeconds < 1) delta = TimeSpan.FromSeconds(1);

        _midnightTimer?.Stop();
        _midnightTimer = new DispatcherTimer { Interval = delta };
        _midnightTimer.Tick += (_, _) =>
        {
            _midnightTimer!.Stop();
            _viewModel?.RolloverAndRefresh();
            ScheduleNextMidnight();
        };
        _midnightTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _midnightTimer?.Stop();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
