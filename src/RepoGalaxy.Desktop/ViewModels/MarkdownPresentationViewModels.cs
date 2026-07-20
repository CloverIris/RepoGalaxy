using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class MarkdownBlockViewModel : ObservableObject
{
    public MarkdownBlockViewModel(MarkdownBlock block) => Block = block;
    public MarkdownBlock Block { get; }
    public string Text => Block.Text;
    public string Url => Block.Url;
    public string AltText => Block.AltText;
    public string Info => Block.Info;
    public bool IsHeading => Block.Kind == MarkdownBlockKind.Heading;
    public bool IsParagraph => Block.Kind == MarkdownBlockKind.Paragraph;
    public bool IsList => Block.Kind == MarkdownBlockKind.ListItem;
    public bool IsQuote => Block.Kind == MarkdownBlockKind.Quote;
    public bool IsCode => Block.Kind == MarkdownBlockKind.Code;
    public bool IsTable => Block.Kind == MarkdownBlockKind.Table;
    public bool IsImage => Block.Kind == MarkdownBlockKind.Image;
    public bool IsRule => Block.Kind == MarkdownBlockKind.Rule;
    public string Bullet => Block.IsChecked switch { true => "☑", false => "☐", _ => Block.Level == 1 ? "1." : "•" };
    public double HeadingSize => Block.Level switch { 1 => 26, 2 => 21, 3 => 18, _ => 16 };
    [ObservableProperty] private Bitmap? _image;
    [ObservableProperty] private bool _isImageLoading;
    [ObservableProperty] private string _imageStatus = string.Empty;
}

public sealed class SemanticMosaicItemViewModel
{
    public const double Unit = 88;
    public const double Gap = 8;
    public SemanticMosaicItemViewModel(SemanticMosaicPlacement placement, Avalonia.Media.IBrush accent, Avalonia.Media.IBrush border)
    {
        Placement = placement; Accent = accent; Border = border;
    }
    public SemanticMosaicPlacement Placement { get; }
    public SemanticIndexItem Item => Placement.Item;
    public string Title => Item.Title;
    public string KindLabel => Item.Kind == SemanticIndexKind.Language ? "语言" : "技术栈";
    public string CountText => Item.ProjectCount == 0 ? "已收录信号" : $"{Item.ProjectCount} 个项目";
    public double Left => Placement.Column * (Unit + Gap);
    public double Top => Placement.Row * (Unit + Gap);
    public double Width => Placement.ColumnSpan * Unit + (Placement.ColumnSpan - 1) * Gap;
    public double Height => Placement.RowSpan * Unit + (Placement.RowSpan - 1) * Gap;
    public Avalonia.Media.IBrush Accent { get; }
    public Avalonia.Media.IBrush Border { get; }
}

public sealed class LocalIdeViewModel
{
    public LocalIdeViewModel(LocalIdeDescriptor descriptor) => Descriptor = descriptor;
    public LocalIdeDescriptor Descriptor { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Descriptor.Version) ? Descriptor.DisplayName : $"{Descriptor.DisplayName}  {Descriptor.Version}";
    public override string ToString() => DisplayName;
}

public sealed record CloneModeOption(CloneMode Mode, string Title, string Description)
{
    public override string ToString() => Title;
}
