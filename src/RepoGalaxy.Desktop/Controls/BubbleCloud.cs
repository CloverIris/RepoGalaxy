using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RepoGalaxy.Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 气泡云容器控件
/// 管理多个 RepoBubble 的物理漂浮效果
/// </summary>
public class BubbleCloud : Canvas
{
    private readonly List<RepoBubble> _bubbles = new();
    private readonly DispatcherTimer _physicsTimer;
    
    /// <summary>
    /// 气泡点击事件 - 打开详情
    /// </summary>
    public event EventHandler<RepoBubble>? BubbleClicked;
    
    private const double Friction = 0.99;
    private const double BounceElasticity = 0.8;
    private const double AvoidanceRadius = 10;
    
    #region Dependency Properties
    
    public static readonly StyledProperty<bool> IsPhysicsEnabledProperty =
        AvaloniaProperty.Register<BubbleCloud, bool>(nameof(IsPhysicsEnabled), true);
    
    public static readonly StyledProperty<double> GlobalSpeedMultiplierProperty =
        AvaloniaProperty.Register<BubbleCloud, double>(nameof(GlobalSpeedMultiplier), 1.0);
    
    public bool IsPhysicsEnabled
    {
        get => GetValue(IsPhysicsEnabledProperty);
        set => SetValue(IsPhysicsEnabledProperty, value);
    }
    
    public double GlobalSpeedMultiplier
    {
        get => GetValue(GlobalSpeedMultiplierProperty);
        set => SetValue(GlobalSpeedMultiplierProperty, value);
    }
    
    #endregion
    
    public BubbleCloud()
    {
        Background = Brushes.Transparent;
        
        InitializeSampleBubbles();
        
        _physicsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _physicsTimer.Tick += OnPhysicsUpdate;
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsPhysicsEnabled)
            _physicsTimer.Start();
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _physicsTimer.Stop();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == IsVisibleProperty)
        {
            if (IsVisible && IsPhysicsEnabled)
                _physicsTimer.Start();
            else
                _physicsTimer.Stop();
        }
        else if (change.Property == IsPhysicsEnabledProperty)
        {
            if (IsPhysicsEnabled && IsVisible)
                _physicsTimer.Start();
            else
                _physicsTimer.Stop();
        }
    }
    
    private void InitializeSampleBubbles()
    {
        var random = new Random();
        var sampleRepos = new[]
        {
            ("avaloniaui", "avalonia", 24000, "C#"),
            ("microsoft", "vscode", 160000, "TypeScript"),
            ("torvalds", "linux", 180000, "C"),
            ("rust-lang", "rust", 95000, "Rust"),
            ("golang", "go", 120000, "Go"),
            ("python", "cpython", 60000, "Python"),
            ("facebook", "react", 220000, "JavaScript"),
            ("vuejs", "vue", 210000, "TypeScript"),
            ("jetbrains", "kotlin", 49000, "Kotlin"),
            ("apple", "swift", 65000, "Swift"),
        };
        
        foreach (var (owner, name, stars, lang) in sampleRepos)
        {
            var bubble = new RepoBubble
            {
                Owner = owner,
                RepoName = name,
                StarCount = stars,
                ForkCount = random.Next(0, stars / 10),
                ActivityIndex = random.NextDouble(),
                LastUpdated = DateTime.Now.AddDays(-random.Next(0, 365)),
                Languages = new List<LanguageInfo>
                {
                    new() { Name = lang, Percentage = 0.8 },
                    new() { Name = "Other", Percentage = 0.2 }
                }
            };
            
            SetLeft(bubble, random.Next(100, 800));
            SetTop(bubble, random.Next(100, 500));
            
            bubble.VelocityX = (random.NextDouble() - 0.5) * 0.3;
            bubble.VelocityY = (random.NextDouble() - 0.5) * 0.3;
            
            SetupDragHandlers(bubble);
            
            _bubbles.Add(bubble);
            Children.Add(bubble);
        }
    }
    
    private void SetupDragHandlers(RepoBubble bubble)
    {
        var isDragging = false;
        var dragStartPoint = default(Point);
        var dragStartPosition = default(Point);
        
        bubble.PointerPressed += (s, e) =>
        {
            var properties = e.GetCurrentPoint(this).Properties;
            if (properties.IsLeftButtonPressed)
            {
                isDragging = true;
                dragStartPoint = e.GetPosition(this);
                dragStartPosition = new Point(GetLeft(bubble), GetTop(bubble));
                bubble.IsSelected = true;
                e.Handled = true;
            }
        };
        
        bubble.PointerMoved += (s, e) =>
        {
            if (isDragging)
            {
                var currentPoint = e.GetPosition(this);
                var offset = currentPoint - dragStartPoint;
                
                SetLeft(bubble, dragStartPosition.X + offset.X);
                SetTop(bubble, dragStartPosition.Y + offset.Y);
                
                bubble.VelocityX = 0;
                bubble.VelocityY = 0;
                
                e.Handled = true;
            }
        };
        
        bubble.PointerReleased += (s, e) =>
        {
            if (isDragging)
            {
                isDragging = false;
                bubble.IsSelected = false;
                
                var random = new Random();
                bubble.VelocityX = (random.NextDouble() - 0.5) * 0.3;
                bubble.VelocityY = (random.NextDouble() - 0.5) * 0.3;
                
                e.Handled = true;
            }
            else
            {
                // 点击打开详情
                BubbleClicked?.Invoke(this, bubble);
                e.Handled = true;
            }
        };
    }
    
    private void OnPhysicsUpdate(object? sender, EventArgs e)
    {
        if (!IsPhysicsEnabled) return;
        
        var width = Bounds.Width;
        var height = Bounds.Height;
        
        if (width <= 0 || height <= 0) return;
        
        foreach (var bubble in _bubbles)
        {
            if (bubble.IsSelected) continue;
            
            var currentX = GetLeft(bubble);
            var currentY = GetTop(bubble);
            var bubbleSize = bubble.Bounds.Width;
            
            bubble.VelocityX *= Friction;
            bubble.VelocityY *= Friction;
            
            currentX += bubble.VelocityX * GlobalSpeedMultiplier;
            currentY += bubble.VelocityY * GlobalSpeedMultiplier;
            
            // 边界反弹
            if (currentX < 0)
            {
                currentX = 0;
                bubble.VelocityX = Math.Abs(bubble.VelocityX) * BounceElasticity;
            }
            else if (currentX + bubbleSize > width)
            {
                currentX = width - bubbleSize;
                bubble.VelocityX = -Math.Abs(bubble.VelocityX) * BounceElasticity;
            }
            
            if (currentY < 0)
            {
                currentY = 0;
                bubble.VelocityY = Math.Abs(bubble.VelocityY) * BounceElasticity;
            }
            else if (currentY + bubbleSize > height)
            {
                currentY = height - bubbleSize;
                bubble.VelocityY = -Math.Abs(bubble.VelocityY) * BounceElasticity;
            }
            
            // 相互避让
            foreach (var other in _bubbles.Where(b => b != bubble))
            {
                var otherX = GetLeft(other);
                var otherY = GetTop(other);
                var otherSize = other.Bounds.Width;
                
                var dx = (currentX + bubbleSize / 2) - (otherX + otherSize / 2);
                var dy = (currentY + bubbleSize / 2) - (otherY + otherSize / 2);
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var minDistance = (bubbleSize + otherSize) / 2 + AvoidanceRadius;
                
                if (distance < minDistance && distance > 0)
                {
                    var force = (minDistance - distance) / minDistance * 0.02;
                    bubble.VelocityX += (dx / distance) * force;
                    bubble.VelocityY += (dy / distance) * force;
                }
            }
            
            // 随机扰动
            if (Random.Shared.NextDouble() < 0.005)
            {
                bubble.VelocityX += (Random.Shared.NextDouble() - 0.5) * 0.1;
                bubble.VelocityY += (Random.Shared.NextDouble() - 0.5) * 0.1;
            }
            
            // 限制速度
            var maxSpeed = 0.5;
            bubble.VelocityX = Math.Clamp(bubble.VelocityX, -maxSpeed, maxSpeed);
            bubble.VelocityY = Math.Clamp(bubble.VelocityY, -maxSpeed, maxSpeed);
            
            SetLeft(bubble, currentX);
            SetTop(bubble, currentY);
            
            bubble.UpdateAnimation(33);
        }
    }
}
