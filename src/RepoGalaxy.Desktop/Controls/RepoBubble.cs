using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 圆形仓库气泡控件 - 半透明玻璃效果
/// </summary>
public class RepoBubble : Control
{
    #region Dependency Properties
    
    public static readonly StyledProperty<int> StarCountProperty =
        AvaloniaProperty.Register<RepoBubble, int>(nameof(StarCount), 100);
    
    public static readonly StyledProperty<int> ForkCountProperty =
        AvaloniaProperty.Register<RepoBubble, int>(nameof(ForkCount), 10);
    
    public static readonly StyledProperty<double> ActivityIndexProperty =
        AvaloniaProperty.Register<RepoBubble, double>(nameof(ActivityIndex), 0.5);
    
    public static readonly StyledProperty<DateTime> LastUpdatedProperty =
        AvaloniaProperty.Register<RepoBubble, DateTime>(nameof(LastUpdated), DateTime.Now);
    
    public static readonly StyledProperty<List<LanguageInfo>> LanguagesProperty =
        AvaloniaProperty.Register<RepoBubble, List<LanguageInfo>>(nameof(Languages), new List<LanguageInfo>());
    
    public static readonly StyledProperty<string> RepoNameProperty =
        AvaloniaProperty.Register<RepoBubble, string>(nameof(RepoName), "repo");
    
    public static readonly StyledProperty<string> OwnerProperty =
        AvaloniaProperty.Register<RepoBubble, string>(nameof(Owner), "owner");
    
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<RepoBubble, bool>(nameof(IsSelected), false);
    
    public static readonly StyledProperty<bool> IsHoveredProperty =
        AvaloniaProperty.Register<RepoBubble, bool>(nameof(IsHovered), false);
    
    public int StarCount
    {
        get => GetValue(StarCountProperty);
        set => SetValue(StarCountProperty, value);
    }
    
    public int ForkCount
    {
        get => GetValue(ForkCountProperty);
        set => SetValue(ForkCountProperty, value);
    }
    
    public double ActivityIndex
    {
        get => GetValue(ActivityIndexProperty);
        set => SetValue(ActivityIndexProperty, value);
    }
    
    public DateTime LastUpdated
    {
        get => GetValue(LastUpdatedProperty);
        set => SetValue(LastUpdatedProperty, value);
    }
    
    public List<LanguageInfo> Languages
    {
        get => GetValue(LanguagesProperty);
        set => SetValue(LanguagesProperty, value);
    }
    
    public string RepoName
    {
        get => GetValue(RepoNameProperty);
        set => SetValue(RepoNameProperty, value);
    }
    
    public string Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }
    
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
    
    public bool IsHovered
    {
        get => GetValue(IsHoveredProperty);
        set => SetValue(IsHoveredProperty, value);
    }
    
    #endregion
    
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    
    private double _breathPhase = 0;
    private double _twinklePhase = 0;
    private double _currentScale = 1.0;
    
    private static readonly Dictionary<string, Color> LanguageColors = new()
    {
        ["C#"] = Color.Parse("#178600"),
        ["JavaScript"] = Color.Parse("#f1e05a"),
        ["TypeScript"] = Color.Parse("#3178c6"),
        ["Python"] = Color.Parse("#3572A5"),
        ["Java"] = Color.Parse("#b07219"),
        ["Go"] = Color.Parse("#00ADD8"),
        ["Rust"] = Color.Parse("#dea584"),
        ["C++"] = Color.Parse("#f34b7d"),
        ["C"] = Color.Parse("#555555"),
        ["Ruby"] = Color.Parse("#701516"),
        ["Swift"] = Color.Parse("#ffac45"),
        ["Kotlin"] = Color.Parse("#A97BFF"),
        ["HTML"] = Color.Parse("#e34c26"),
        ["CSS"] = Color.Parse("#563d7c"),
        ["Shell"] = Color.Parse("#89e051"),
        ["Vue"] = Color.Parse("#41b883"),
    };
    
    public RepoBubble()
    {
        UpdateSize();
        PointerEntered += (s, e) => { IsHovered = true; InvalidateVisual(); };
        PointerExited += (s, e) => { IsHovered = false; InvalidateVisual(); };
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StarCountProperty) UpdateSize();
    }
    
    private void UpdateSize()
    {
        var size = CalculateSize(StarCount);
        Width = size;
        Height = size;
        InvalidateVisual();
    }
    
    private double CalculateSize(int stars)
    {
        var normalized = Math.Log10(Math.Max(1, stars)) / Math.Log10(100000);
        return 32 + normalized * 56;
    }
    
    private double CalculateBreathScale(int forks)
    {
        return forks switch
        {
            0 => 1.0,
            <= 100 => 1.0 + 0.03 * Math.Sin(_breathPhase),
            <= 1000 => 1.0 + 0.06 * Math.Sin(_breathPhase),
            _ => 1.0 + 0.09 * Math.Sin(_breathPhase)
        };
    }
    
    private double CalculateBrightness()
    {
        var daysAgo = (DateTime.Now - LastUpdated).TotalDays;
        var age = Math.Clamp(daysAgo / 365, 0, 1);
        return 1.0 - age * 0.5;
    }
    
    public void UpdateAnimation(double deltaTime)
    {
        var breathSpeed = ForkCount switch
        {
            0 => 0,
            <= 100 => 2 * Math.PI / 200,
            <= 1000 => 2 * Math.PI / 300,
            _ => 2 * Math.PI / 400
        };
        
        _breathPhase += breathSpeed;
        _currentScale = CalculateBreathScale(ForkCount);
        
        var twinkleSpeed = ActivityIndex * 0.1;
        _twinklePhase += twinkleSpeed;
        
        InvalidateVisual();
    }
    
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;
        var baseRadius = Math.Min(bounds.Width, bounds.Height) / 2 - 2;
        
        var hoverScale = IsHovered ? 1.08 : 1.0;
        var finalScale = _currentScale * hoverScale;
        var radius = baseRadius * finalScale;
        
        var brightness = CalculateBrightness();
        
        // 外发光
        if (IsHovered || IsSelected)
        {
            DrawGlow(context, cx, cy, radius);
        }
        
        // 玻璃背景
        DrawGlassBackground(context, cx, cy, radius, brightness);
        
        // 语言扇形环
        if (Languages != null && Languages.Count > 0)
        {
            DrawLanguageRing(context, cx, cy, radius, brightness);
        }
        
        // 边框
        DrawBorder(context, cx, cy, radius, brightness);
        
        // 活跃度指示
        if (ActivityIndex > 0)
        {
            DrawActivityIndicator(context, cx, cy, radius);
        }
    }
    
    private void DrawGlow(DrawingContext context, double cx, double cy, double radius)
    {
        var glowRadius = radius * 1.4;
        var glowBrush = new RadialGradientBrush
        {
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(60, 88, 166, 255), 0),
                new GradientStop(Color.FromArgb(30, 88, 166, 255), 0.5),
                new GradientStop(Color.FromArgb(0, 88, 166, 255), 1)
            }
        };
        
        context.DrawEllipse(glowBrush, null, new Point(cx, cy), glowRadius, glowRadius);
    }
    
    private void DrawGlassBackground(DrawingContext context, double cx, double cy, double radius, double brightness)
    {
        var alpha = (byte)(40 * brightness);
        
        // 外圈半透明
        var outerBrush = new SolidColorBrush(Color.FromArgb(alpha, 30, 40, 60));
        context.DrawEllipse(outerBrush, null, new Point(cx, cy), radius, radius);
        
        // 内圈更透明
        var innerAlpha = (byte)(25 * brightness);
        var innerBrush = new SolidColorBrush(Color.FromArgb(innerAlpha, 50, 60, 80));
        context.DrawEllipse(innerBrush, null, new Point(cx, cy), radius * 0.7, radius * 0.7);
    }
    
    private void DrawLanguageRing(DrawingContext context, double cx, double cy, double radius, double brightness)
    {
        var validLanguages = Languages?.Where(l => l.Percentage > 0).ToList();
        if (validLanguages == null || validLanguages.Count == 0) return;
        
        var innerR = radius * 0.35;
        var outerR = radius * 0.85;
        var startAngle = -90.0;
        
        foreach (var lang in validLanguages.Take(4)) // 最多显示4种语言
        {
            var sweepAngle = lang.Percentage * 360;
            var color = GetLanguageColor(lang.Name);
            var alpha = (byte)(200 * brightness);
            
            // 使用椭圆弧近似扇形
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            
            // 简化：用同心圆环表示
            var ringRadius = innerR + (outerR - innerR) * (validLanguages.IndexOf(lang) + 1) / validLanguages.Count;
            var ringThickness = (outerR - innerR) / validLanguages.Count * 0.8;
            
            var pen = new Pen(brush, ringThickness);
            context.DrawEllipse(null, pen, new Point(cx, cy), ringRadius, ringRadius);
            
            startAngle += sweepAngle;
        }
    }
    
    private void DrawBorder(DrawingContext context, double cx, double cy, double radius, double brightness)
    {
        var alpha = (byte)(120 * brightness);
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 150, 160, 180)), 1.5);
        context.DrawEllipse(null, borderPen, new Point(cx, cy), radius, radius);
        
        // 内边框
        var innerAlpha = (byte)(60 * brightness);
        var innerPen = new Pen(new SolidColorBrush(Color.FromArgb(innerAlpha, 200, 210, 230)), 0.5);
        context.DrawEllipse(null, innerPen, new Point(cx, cy), radius * 0.7, radius * 0.7);
    }
    
    private void DrawActivityIndicator(DrawingContext context, double cx, double cy, double radius)
    {
        var twinkle = 0.5 + 0.5 * Math.Sin(_twinklePhase);
        var alpha = (byte)(ActivityIndex * twinkle * 255);
        
        // 发光
        var glowBrush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.3), 100, 255, 150));
        context.DrawEllipse(glowBrush, null, 
            new Point(cx + radius * 0.5, cy + radius * 0.5), 
            radius * 0.4, radius * 0.4);
        
        // 中心点
        var dotBrush = new SolidColorBrush(Color.FromArgb(alpha, 100, 255, 150));
        context.DrawEllipse(dotBrush, null,
            new Point(cx + radius * 0.5, cy + radius * 0.5),
            radius * 0.12, radius * 0.12);
    }
    
    private Color GetLanguageColor(string language)
    {
        if (LanguageColors.TryGetValue(language, out var color))
            return color;
        return Color.Parse("#8B949E");
    }
}

public class LanguageInfo
{
    public string Name { get; set; } = "";
    public double Percentage { get; set; }
}
