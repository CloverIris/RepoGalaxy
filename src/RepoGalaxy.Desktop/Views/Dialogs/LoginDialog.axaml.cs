using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RepoGalaxy.Desktop.Views.Dialogs;

public partial class LoginDialog : Window
{
    public LoginDialog() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
