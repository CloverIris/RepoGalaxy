using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RepoGalaxy.Desktop.Views;

public partial class RepoDetailView : UserControl
{
    public RepoDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
