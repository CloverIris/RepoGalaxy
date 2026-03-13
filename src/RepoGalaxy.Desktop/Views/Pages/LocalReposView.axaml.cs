using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views.Pages;

public partial class LocalReposView : UserControl
{
    public LocalReposView()
    {
        InitializeComponent();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is long id)
        {
            if (DataContext is LocalReposViewModel vm)
            {
                vm.RemoveCommand.Execute(id);
            }
        }
    }
}
