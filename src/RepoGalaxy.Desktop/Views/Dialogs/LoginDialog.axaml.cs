using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Views.Dialogs;

public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
        PlatformWindowChrome.UseNativeMacDecorations(this);
        Opened += (_, _) => ConfigureWindowChrome();
    }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ConfigureWindowChrome()
    {
        if (OperatingSystem.IsMacOS())
        {
            if (this.FindControl<Control>("LoginCloseButton") is { } macClose) macClose.IsVisible = false;
            return;
        }

        if (this.FindControl<Control>("LoginTitleBar") is { } title)
            WindowDecorationProperties.SetElementRole(title, WindowDecorationsElementRole.TitleBar);
        if (this.FindControl<Control>("LoginCloseButton") is { } close)
            WindowDecorationProperties.SetElementRole(close, WindowDecorationsElementRole.CloseButton);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
