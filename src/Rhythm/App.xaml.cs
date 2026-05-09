using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

    public bool IsShuttingDown { get; private set; }

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
        _mainWindow = new MainWindow { DataContext = _viewModel };
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
        var miAbout = new MenuItem { Header = "关于 Rhythm" };
        miAbout.Click += (_, _) => ShowAboutDialogAfterMenuCloses();
        var miExit = new MenuItem { Header = "退出" };
        miExit.Click += (_, _) => ShutdownApp();

        menu.Items.Add(miShow);
        menu.Items.Add(miHide);
        menu.Items.Add(miEdit);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAutostart);
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
        const string message = "Rhythm v1\n\n本地桌面便签\n勾选完成，每日自动重置。";
        const string caption = "关于 Rhythm";

        if (_mainWindow?.IsVisible == true)
        {
            MessageBox.Show(_mainWindow, message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShutdownFromUi() => ShutdownApp();

    public void OpenEditItems()
    {
        if (_viewModel == null) return;
        var w = new EditItemsWindow(_viewModel)
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
