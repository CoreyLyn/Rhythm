using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Rhythm.Interop;

namespace Rhythm.UI;

internal static class SolidWindowChrome
{
    public static void Apply(Window window, Border root, double cornerRadius)
    {
        var brush = (SolidColorBrush)Application.Current.FindResource("SolidWindowBackgroundBrush");
        window.Background = brush;
        root.Background = brush;

        UpdateRegion(window, cornerRadius);
        window.SizeChanged += (_, _) => UpdateRegion(window, cornerRadius);
        window.DpiChanged += (_, _) => UpdateRegion(window, cornerRadius);
    }

    private static void UpdateRegion(Window window, double cornerRadius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var dpi = VisualTreeHelper.GetDpi(window);
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
        var radiusX = Math.Max(1, (int)Math.Round(cornerRadius * 2 * dpi.DpiScaleX));
        var radiusY = Math.Max(1, (int)Math.Round(cornerRadius * 2 * dpi.DpiScaleY));

        var region = Win32.CreateRoundRectRgn(0, 0, width + 1, height + 1, radiusX, radiusY);
        if (region != IntPtr.Zero)
        {
            var applied = Win32.SetWindowRgn(hwnd, region, bRedraw: true) != 0;
            if (!applied)
            {
                Win32.DeleteObject(region);
            }
        }
    }
}
