using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.ViewModels.Dialogs;

namespace RepoGalaxy.Desktop.Views.Dialogs;

public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
        
        // 监听 DataContext 变化，订阅剪贴板事件
        DataContextChanged += (s, e) =>
        {
            if (DataContext is LoginDialogViewModel vm)
            {
                vm.CopyToClipboard += async (sender, code) =>
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(code);
                    }
                };
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
