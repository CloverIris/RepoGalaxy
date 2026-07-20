using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace RepoGalaxy.Desktop.Views.Dialogs;

public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
        Opened += (_, _) => ConfigureWindowChrome();
    }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ConfigureWindowChrome()
    {
        if (this.FindControl<Control>("LoginTitleBar") is { } title)
            WindowDecorationProperties.SetElementRole(title, WindowDecorationsElementRole.TitleBar);
        if (this.FindControl<Control>("LoginCloseButton") is { } close)
            WindowDecorationProperties.SetElementRole(close, WindowDecorationsElementRole.CloseButton);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
