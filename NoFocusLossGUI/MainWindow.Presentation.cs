#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SharpestInjector;

namespace NoFocusLossGUI;

public partial class MainWindow
{
    private WindowTheme _theme = null!;
    internal WindowTheme Theme => _theme;
    private void ToggleTheme(object sender, RoutedEventArgs args) => _theme.Toggle();
    private void WindowLoaded(object sender, RoutedEventArgs args)
    {
        if (!App.IsUiSmoke) Refresh(sender, args);
    }
    private void SearchChanged(object sender, TextChangedEventArgs args)
    {
        if (Processes is null || InjectedProcesses is null) return;
        var query = Search.Text.Trim();
        bool Match(object value) => value is ProcessInfo process &&
            (query.Length == 0 || (process.WindowTitle ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
             (process.FileName ?? "").Contains(query, StringComparison.OrdinalIgnoreCase));
        Processes.Items.Filter = Match;
        InjectedProcesses.Items.Filter = Match;
    }
}

// The injector deliberately exposes fields, not WPF bindable properties. Keep UI formatting here.
public sealed class ProcessLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ProcessInfo process) return "";
        return parameter as string == "title"
            ? string.IsNullOrWhiteSpace(process.WindowTitle) ? process.FileName : process.WindowTitle
            : $"{process.FileName}  ·  PID {process.Id}  ·  {(process.IsWOW64 ? "32" : "64")}-bit";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
