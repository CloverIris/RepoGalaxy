using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.Controls;

public partial class ClusterMemberItem : UserControl
{
    private BubbleItem? _bubbleItem;
    private bool _isSelected;

    public ClusterMemberItem()
    {
        InitializeComponent();
        
        // 点击卡片切换选择
        PointerPressed += OnCardPointerPressed;
        
        // Checkbox 变化时更新状态
        var checkBox = this.FindControl<CheckBox>("SelectionCheckBox");
        if (checkBox != null)
        {
            checkBox.IsCheckedChanged += (s, e) =>
            {
                _isSelected = checkBox.IsChecked ?? false;
                UpdateVisualState();
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 设置成员数据
    /// </summary>
    public void SetMember(BubbleItem bubble)
    {
        _bubbleItem = bubble;
        
        var nameText = this.FindControl<TextBlock>("NameTextBlock");
        var ownerText = this.FindControl<TextBlock>("OwnerTextBlock");
        var langText = this.FindControl<TextBlock>("LanguageTextBlock");
        var starsText = this.FindControl<TextBlock>("StarsTextBlock");
        var langDot = this.FindControl<Ellipse>("LanguageDot");
        
        if (nameText != null) nameText.Text = bubble.Name;
        if (ownerText != null) ownerText.Text = $"@{bubble.Owner}";
        if (langText != null) langText.Text = bubble.PrimaryLanguage ?? "Unknown";
        if (starsText != null) starsText.Text = bubble.Stars.ToString("N0");
        if (langDot != null) langDot.Fill = GetLanguageBrush(bubble.PrimaryLanguage);
    }

    /// <summary>
    /// 获取或设置选择状态
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            var checkBox = this.FindControl<CheckBox>("SelectionCheckBox");
            if (checkBox != null)
            {
                checkBox.IsChecked = value;
            }
            UpdateVisualState();
        }
    }

    /// <summary>
    /// 获取关联的 BubbleItem
    /// </summary>
    public BubbleItem? BubbleItem => _bubbleItem;

    private void UpdateVisualState()
    {
        var card = this.FindControl<Border>("CardBorder");
        if (card != null)
        {
            if (_isSelected)
            {
                card.Background = new SolidColorBrush(Color.Parse("#23863620"));
                card.BorderBrush = new SolidColorBrush(Color.Parse("#238636"));
                card.BorderThickness = new Thickness(2);
            }
            else
            {
                card.Background = new SolidColorBrush(Color.Parse("#21262D"));
                card.BorderBrush = new SolidColorBrush(Color.Parse("#30363D"));
                card.BorderThickness = new Thickness(1);
            }
        }
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 点击卡片切换选择（不包括 Checkbox 区域）
        if (e.Source is not CheckBox)
        {
            IsSelected = !IsSelected;
        }
    }

    private IBrush GetLanguageBrush(string? language)
    {
        var color = language?.ToLower() switch
        {
            "rust" => 0xFFdea584,
            "python" => 0xFF3572A5,
            "javascript" => 0xFFf1e05a,
            "typescript" => 0xFF3178c6,
            "go" or "golang" => 0xFF00ADD8,
            "java" => 0xFFb07219,
            "c++" or "cpp" => 0xFFf34b7d,
            "c#" or "csharp" => 0xFF178600,
            "c" => 0xFF555555,
            "ruby" => 0xFF701516,
            "swift" => 0xFFffac45,
            "kotlin" => 0xFFA97BFF,
            "php" => 0xFF4F5D95,
            "shell" => 0xFF89e051,
            "html" => 0xFFe34c26,
            "css" => 0xFF563d7c,
            _ => 0xFF8b949e
        };

        return new SolidColorBrush(Color.FromUInt32((uint)color));
    }
}
