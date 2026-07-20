using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Views.Dialogs;

public partial class LoginDialog : Window
{
    private WindowsMetroChrome? _nativeChrome;

    public LoginDialog()
    {
        InitializeComponent();
        Opened += (_, _) => _nativeChrome = WindowsMetroChrome.Attach(this, 42);
        Closed += (_, _) => _nativeChrome?.Dispose();
    }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
