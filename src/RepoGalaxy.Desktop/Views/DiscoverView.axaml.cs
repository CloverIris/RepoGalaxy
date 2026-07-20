using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
namespace RepoGalaxy.Desktop.Views;
public partial class DiscoverView : UserControl
{
    public DiscoverView()
    {
        AvaloniaXamlLoader.Load(this);
        Loaded += (_, _) => UpdateCardWidth();
        SizeChanged += (_, _) => UpdateCardWidth();
    }
    private void UpdateCardWidth()
    {
        var panel = this.GetVisualDescendants().OfType<WrapPanel>().FirstOrDefault();
        if (panel is null) return;
        var windowWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? Bounds.Width;
        panel.ItemWidth = windowWidth < 1000 ? Math.Max(360, Bounds.Width - 36) : 410;
    }
}
