using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 星空动态背景控件 - 深邃宇宙效果
/// 包含：星云、银河、星光粒子、Octocat轮廓
/// </summary>
public class StarfieldBackground : Control
{
    private const int MinStarCount = 80;
    private const int MaxStarCount = 150;
    private const int NebulaCount = 5;
    
    private readonly List<StarParticle> _stars = new();
    private readonly List<NebulaCloud> _nebulas = new();
    private readonly List<OctocatStar> _octocatStars = new();
    private readonly DispatcherTimer _animationTimer;
    
    // Octocat 轮廓坐标 (相对 0-1000 坐标系)
    private static readonly (double x, double y)[] OctocatOutline = new (double, double)[]
    {
        // 左耳
        (350.0, 250.0), (320.0, 200.0), (360.0, 150.0), (420.0, 180.0),
        // 头顶
        (500.0, 170.0), (580.0, 180.0),
        // 右耳
        (640.0, 150.0), (680.0, 200.0), (650.0, 250.0),
        // 右脸
        (700.0, 300.0), (710.0, 380.0), (680.0, 450.0),
        // 右身体
        (720.0, 520.0), (700.0, 600.0), (680.0, 680.0),
        // 右腿
        (680.0, 750.0), (670.0, 820.0), (630.0, 820.0), (620.0, 750.0),
        // 底部
        (580.0, 720.0), (500.0, 730.0), (420.0, 720.0),
        // 左腿
        (380.0, 750.0), (370.0, 820.0), (330.0, 820.0), (320.0, 750.0),
        // 左身体
        (300.0, 680.0), (280.0, 600.0), (300.0, 520.0),
        // 尾巴
        (280.0, 550.0), (220.0, 580.0), (180.0, 650.0), (200.0, 720.0), (260.0, 700.0), (300.0, 680.0),
        // 回到左脸
        (320.0, 450.0), (290.0, 380.0), (300.0, 300.0),
    };
    
    #region Dependency Properties
    
    public static readonly StyledProperty<Color> StarfieldColorProperty =
        AvaloniaProperty.Register<StarfieldBackground, Color>(
            nameof(StarfieldColor), 
            Color.Parse("#0B0E14"));
    
    public static readonly StyledProperty<bool> AnimationEnabledProperty =
        AvaloniaProperty.Register<StarfieldBackground, bool>(
            nameof(AnimationEnabled), 
            true);
    
    public static readonly StyledProperty<bool> ShowOctocatProperty =
        AvaloniaProperty.Register<StarfieldBackground, bool>(
            nameof(ShowOctocat), 
            true);
    
    public Color StarfieldColor
    {
        get => GetValue(StarfieldColorProperty);
        set => SetValue(StarfieldColorProperty, value);
    }
    
    public bool AnimationEnabled
    {
        get => GetValue(AnimationEnabledProperty);
        set => SetValue(AnimationEnabledProperty, value);
    }
    
    public bool ShowOctocat
    {
        get => GetValue(ShowOctocatProperty);
        set => SetValue(ShowOctocatProperty, value);
    }
    
    #endregion
    
    public StarfieldBackground()
    {
        Width = 800;
        Height = 600;
        
        InitializeNebulas();
        InitializeStars();
        InitializeOctocat();
        
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _animationTimer.Tick += OnAnimationTick;
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (AnimationEnabled)
            _animationTimer.Start();
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer.Stop();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == BoundsProperty)
        {
            InitializeStars();
            InitializeOctocat();
        }
        else if (change.Property == AnimationEnabledProperty)
        {
            if (AnimationEnabled)
                _animationTimer.Start();
            else
                _animationTimer.Stop();
        }
    }
    
    /// <summary>
    /// 初始化星云
    /// </summary>
    private void InitializeNebulas()
    {
        var random = new Random();
        _nebulas.Clear();
        
        // 银河效果的星云
        var colors = new[]
        {
            Color.FromArgb(40, 88, 166, 255),   // 蓝色
            Color.FromArgb(30, 139, 92, 246),   // 紫色
            Color.FromArgb(25, 236, 72, 153),   // 粉色
            Color.FromArgb(20, 34, 211, 238),   // 青色
            Color.FromArgb(35, 99, 102, 241),   // 靛蓝
        };
        
        for (int i = 0; i < NebulaCount; i++)
        {
            _nebulas.Add(new NebulaCloud
            {
                X = random.NextDouble() * 1000,
                Y = random.NextDouble() * 1000,
                Size = random.NextDouble() * 400 + 300,
                Color = colors[i % colors.Length],
                DriftSpeedX = (random.NextDouble() - 0.5) * 0.05,
                DriftSpeedY = (random.NextDouble() - 0.5) * 0.05,
                PulsePhase = random.NextDouble() * Math.PI * 2
            });
        }
    }
    
    /// <summary>
    /// 初始化星光粒子
    /// </summary>
    private void InitializeStars()
    {
        var random = new Random();
        var starCount = random.Next(MinStarCount, MaxStarCount + 1);
        
        _stars.Clear();
        
        for (int i = 0; i < starCount; i++)
        {
            // 避免在 Octocat 区域生成太多星星
            double x, y;
            int attempts = 0;
            do
            {
                x = random.NextDouble() * 1000;
                y = random.NextDouble() * 1000;
                attempts++;
            } while (IsNearOctocat(x, y) && attempts < 10);
            
            _stars.Add(new StarParticle
            {
                X = x,
                Y = y,
                Size = random.NextDouble() * 2.5 + 0.5,
                SpeedX = (random.NextDouble() - 0.5) * 0.15,
                SpeedY = (random.NextDouble() - 0.5) * 0.15,
                Opacity = random.NextDouble() * 0.6 + 0.4,
                TwinkleSpeed = random.NextDouble() * 0.03 + 0.01,
                TwinklePhase = random.NextDouble() * Math.PI * 2,
                Color = random.NextDouble() > 0.9 
                    ? Color.FromArgb(255, 180, 200, 255)  // 偏蓝
                    : Color.FromArgb(255, 230, 237, 243)  // 白色
            });
        }
    }
    
    /// <summary>
    /// 检查坐标是否在 Octocat 区域附近
    /// </summary>
    private bool IsNearOctocat(double x, double y)
    {
        // 简化的 Octocat 边界框检查
        return x > 200 && x < 750 && y > 150 && y < 850;
    }
    
    /// <summary>
    /// 初始化 Octocat 星光轮廓
    /// </summary>
    private void InitializeOctocat()
    {
        _octocatStars.Clear();
        var random = new Random();
        
        foreach (var (x, y) in OctocatOutline)
        {
            _octocatStars.Add(new OctocatStar
            {
                BaseX = x,
                BaseY = y,
                CurrentX = x,
                CurrentY = y,
                Size = random.NextDouble() * 2 + 2,
                PulsePhase = random.NextDouble() * Math.PI * 2,
                PulseSpeed = random.NextDouble() * 0.05 + 0.02
            });
        }
    }
    
    /// <summary>
    /// 动画帧更新
    /// </summary>
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // 更新星星
        foreach (var star in _stars)
        {
            star.X += star.SpeedX;
            star.Y += star.SpeedY;
            
            if (star.X < 0) star.X = 1000;
            if (star.X > 1000) star.X = 0;
            if (star.Y < 0) star.Y = 1000;
            if (star.Y > 1000) star.Y = 0;
            
            star.TwinklePhase += star.TwinkleSpeed;
            star.CurrentOpacity = star.Opacity * (0.6 + 0.4 * Math.Sin(star.TwinklePhase));
        }
        
        // 更新星云
        foreach (var nebula in _nebulas)
        {
            nebula.X += nebula.DriftSpeedX;
            nebula.Y += nebula.DriftSpeedY;
            
            if (nebula.X < -200) nebula.X = 1200;
            if (nebula.X > 1200) nebula.X = -200;
            if (nebula.Y < -200) nebula.Y = 1200;
            if (nebula.Y > 1200) nebula.Y = -200;
            
            nebula.PulsePhase += 0.01;
            nebula.CurrentOpacity = 0.5 + 0.3 * Math.Sin(nebula.PulsePhase);
        }
        
        // 更新 Octocat 星光
        foreach (var star in _octocatStars)
        {
            star.PulsePhase += star.PulseSpeed;
            star.CurrentSize = star.Size * (0.8 + 0.4 * Math.Sin(star.PulsePhase));
            star.CurrentBrightness = 0.7 + 0.3 * Math.Sin(star.PulsePhase);
        }
        
        InvalidateVisual();
    }
    
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        
        // 绘制深邃背景
        context.FillRectangle(
            new SolidColorBrush(StarfieldColor),
            bounds);
        
        // 绘制星云（在星星后面）
        foreach (var nebula in _nebulas)
        {
            DrawNebula(context, nebula, bounds);
        }
        
        // 绘制星星
        foreach (var star in _stars)
        {
            DrawStar(context, star, bounds);
        }
        
        // 绘制 Octocat 轮廓
        if (ShowOctocat)
        {
            DrawOctocat(context, bounds);
        }
    }
    
    private void DrawNebula(DrawingContext context, NebulaCloud nebula, Rect bounds)
    {
        var x = nebula.X / 1000 * bounds.Width;
        var y = nebula.Y / 1000 * bounds.Height;
        var size = nebula.Size / 1000 * Math.Min(bounds.Width, bounds.Height);
        
        var alpha = (byte)(nebula.Color.A * nebula.CurrentOpacity);
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Radius = 0.5,
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(alpha, nebula.Color.R, nebula.Color.G, nebula.Color.B), 0),
                new GradientStop(Color.FromArgb(0, nebula.Color.R, nebula.Color.G, nebula.Color.B), 1)
            }
        };
        
        context.FillRectangle(
            brush,
            new Rect(x - size/2, y - size/2, size, size));
    }
    
    private void DrawStar(DrawingContext context, StarParticle star, Rect bounds)
    {
        var x = star.X / 1000 * bounds.Width;
        var y = star.Y / 1000 * bounds.Height;
        var size = star.Size / 1000 * Math.Min(bounds.Width, bounds.Height) * 20;
        
        var alpha = (byte)(star.CurrentOpacity * 255);
        var brush = new SolidColorBrush(
            Color.FromArgb(alpha, star.Color.R, star.Color.G, star.Color.B));
        
        // 十字星光效果
        var h = size / 2;
        context.FillRectangle(brush, new Rect(x - h/4, y - h, h/2, h*2));
        context.FillRectangle(brush, new Rect(x - h, y - h/4, h*2, h/2));
    }
    
    private void DrawOctocat(DrawingContext context, Rect bounds)
    {
        if (_octocatStars.Count < 2) return;
        
        var scaleX = bounds.Width / 1000;
        var scaleY = bounds.Height / 1000;
        
        // 绘制连线
        var lineBrush = new SolidColorBrush(Color.FromArgb(100, 100, 180, 255));
        var linePen = new Pen(lineBrush, 1.5);
        
        for (int i = 0; i < _octocatStars.Count - 1; i++)
        {
            var star1 = _octocatStars[i];
            var star2 = _octocatStars[i + 1];
            
            // 跳过尾巴部分的断点
            if (i == 30) continue; // 尾巴连接处
            
            var x1 = star1.CurrentX * scaleX;
            var y1 = star1.CurrentY * scaleY;
            var x2 = star2.CurrentX * scaleX;
            var y2 = star2.CurrentY * scaleY;
            
            context.DrawLine(linePen, new Point(x1, y1), new Point(x2, y2));
        }
        
        // 闭合轮廓
        var first = _octocatStars[0];
        var last = _octocatStars[30]; // 到尾巴前闭合
        context.DrawLine(linePen, 
            new Point(last.CurrentX * scaleX, last.CurrentY * scaleY),
            new Point(first.CurrentX * scaleX, first.CurrentY * scaleY));
        
        // 绘制星光点
        foreach (var star in _octocatStars)
        {
            var x = star.CurrentX * scaleX;
            var y = star.CurrentY * scaleY;
            var size = star.CurrentSize / 1000 * Math.Min(bounds.Width, bounds.Height) * 15;
            
            var alpha = (byte)(star.CurrentBrightness * 255);
            var glowBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(alpha, 150, 200, 255), 0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.5), 100, 150, 255), 0.5),
                    new GradientStop(Color.FromArgb(0, 100, 150, 255), 1)
                }
            };
            
            context.DrawEllipse(
                glowBrush,
                null,
                new Point(x, y),
                size * 2,
                size * 2);
            
            // 中心亮点
            context.FillRectangle(
                new SolidColorBrush(Color.FromArgb(255, 200, 230, 255)),
                new Rect(x - size/4, y - size/4, size/2, size/2));
        }
    }
    
    private class StarParticle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public double SpeedX { get; set; }
        public double SpeedY { get; set; }
        public double Opacity { get; set; }
        public double CurrentOpacity { get; set; } = 1.0;
        public double TwinklePhase { get; set; }
        public double TwinkleSpeed { get; set; }
        public Color Color { get; set; }
    }
    
    private class NebulaCloud
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public Color Color { get; set; }
        public double DriftSpeedX { get; set; }
        public double DriftSpeedY { get; set; }
        public double PulsePhase { get; set; }
        public double CurrentOpacity { get; set; } = 1.0;
    }
    
    private class OctocatStar
    {
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public double Size { get; set; }
        public double CurrentSize { get; set; } = 1.0;
        public double PulsePhase { get; set; }
        public double PulseSpeed { get; set; }
        public double CurrentBrightness { get; set; } = 1.0;
    }
}
