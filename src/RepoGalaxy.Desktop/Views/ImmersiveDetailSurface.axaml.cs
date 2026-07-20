using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views;

public partial class ImmersiveDetailSurface : UserControl
{
    public ImmersiveDetailSurface() => InitializeComponent();

    private async void OnChooseCloneFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DiscoverViewModel viewModel || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 Git 仓库的父目录",
            AllowMultiple = false
        });
        if (folders.Count == 1 && folders[0].TryGetLocalPath() is { } path) viewModel.SetCloneParentDirectory(path);
    }
}
