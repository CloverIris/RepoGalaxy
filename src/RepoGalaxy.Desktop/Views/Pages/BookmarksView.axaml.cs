using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views.Pages;

public partial class BookmarksView : UserControl
{
    public BookmarksView()
    {
        InitializeComponent();
    }

    private void OnRemoveBookmarkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is long repoId)
        {
            if (DataContext is BookmarksViewModel vm)
            {
                vm.RemoveBookmarkCommand.Execute(repoId);
            }
        }
    }
}
