using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 气泡云渲染操作
/// </summary>
public class BubbleCloudRenderOperation : ICustomDrawOperation
{
    private readonly List<BubbleItem> _bubbles;
    private readonly List<ClusterGroup> _clusters;
    private readonly Rect _bounds;
    private readonly BubbleItem? _hoveredBubble;
    private readonly bool _isShaking;
    private readonly Dictionary<long, ShakeVisualState> _shakeStates;
    private readonly ClusterGroup? _longPressCluster;
    private readonly float _longPressProgress;
    private readonly List<Particle> _particles;

    public BubbleCloudRenderOperation(
        List<BubbleItem> bubbles,
        List<ClusterGroup> clusters,
        Rect bounds,
        BubbleItem? hoveredBubble,
        bool isShaking = false,
        Dictionary<long, ShakeVisualState>? shakeStates = null,
        ClusterGroup? longPressCluster = null,
        float longPressProgress = 0f,
        List<Particle>? particles = null)
    {
        _bubbles = bubbles;
        _clusters = clusters;
        _bounds = bounds;
        _hoveredBubble = hoveredBubble;
        _isShaking = isShaking;
        _shakeStates = shakeStates ?? new Dictionary<long, ShakeVisualState>();
        _longPressCluster = longPressCluster;
        _longPressProgress = longPressProgress;
        _particles = particles ?? new List<Particle>();
    }

    public Rect Bounds => _bounds;
    public void Dispose() { }
    public bool Equals(ICustomDrawOperation? other) => false;
    public bool HitTest(Point point) => Bounds.Contains(point);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        
        canvas.Save();
        
        // 1. 绘制聚类背景
        foreach (var cluster in _clusters)
        {
            DrawClusterBackground(canvas, cluster);
        }
        
        // 2. 绘制气泡 (按半径从小到大，大圆在后)
        foreach (var bubble in _bubbles.OrderBy(b => b.Radius))
        {
            DrawBubble(canvas, bubble);
        }
        
        // 3. 绘制聚类覆盖层 (计数等)
        foreach (var cluster in _clusters)
        {
            DrawClusterOverlay(canvas, cluster);
        }
        
        // 4. 绘制长按进度环
        if (_longPressCluster != null && _longPressProgress > 0)
        {
            DrawLongPressIndicator(canvas, _longPressCluster, _longPressProgress);
        }
        
        // 5. 绘制粒子效果
        foreach (var particle in _particles)
        {
            DrawParticle(canvas, particle);
        }
        
        canvas.Restore();
    }

    private void DrawClusterBackground(SKCanvas canvas, ClusterGroup cluster)
    {
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(88, 166, 255, 60),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 20)
        };
        
        canvas.DrawCircle(cluster.CenterX, cluster.CenterY, cluster.CurrentRadius, glowPaint);
        
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(88, 166, 255, 180),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        
        borderPaint.PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0);
        canvas.DrawCircle(cluster.CenterX, cluster.CenterY, cluster.CurrentRadius, borderPaint);
    }

    private void DrawClusterOverlay(SKCanvas canvas, ClusterGroup cluster)
    {
        int memberCount = cluster.Members.Count;
        if (memberCount <= 1) return;
        
        string countText = $"+{memberCount - 1}";
        
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.Default
        };
        
        var textBounds = new SKRect();
        textPaint.MeasureText(countText, ref textBounds);
        
        float x = cluster.CenterX + cluster.CurrentRadius * 0.6f - textBounds.Width / 2;
        float y = cluster.CenterY + cluster.CurrentRadius * 0.6f + textBounds.Height / 2;
        
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(88, 166, 255, 200),
            IsAntialias = true
        };
        canvas.DrawCircle(x + textBounds.Width / 2, y - textBounds.Height / 2, textBounds.Width, bgPaint);
        
        canvas.DrawText(countText, x, y, textPaint);
    }

    private void DrawBubble(SKCanvas canvas, BubbleItem bubble)
    {
        float breathOffset = MathF.Sin(bubble.BreathPhase) * bubble.BreathScale;
        float baseRadius = bubble.Radius * bubble.HoverScale;
        float radius = baseRadius * (1 + breathOffset);
        
        float twinkleBrightness = 1.0f;
        if (bubble.TwinkleFrequency > 0)
        {
            twinkleBrightness = 0.8f + 0.2f * MathF.Sin(bubble.TwinklePhase);
        }
        
        float finalBrightness = bubble.Brightness * twinkleBrightness;
        if (_hoveredBubble != null && _hoveredBubble != bubble)
        {
            finalBrightness *= 0.7f;
        }
        
        // 摇晃发光效果
        float shakeIntensity = 0;
        if (bubble.IsDragging && _isShaking && _shakeStates.TryGetValue(bubble.Id, out var shakeState))
        {
            shakeIntensity = shakeState.GetGlowIntensity();
        }
        
        if (shakeIntensity > 0)
        {
            // 核心发光 - 金黄色
            using var shakeGlow = new SKPaint
            {
                Color = new SKColor(255, 200, 0, (byte)(150 * shakeIntensity)),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 20 * shakeIntensity)
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius + 8 * shakeIntensity, shakeGlow);
            
            // 外层光晕 - 橙色
            using var outerGlow = new SKPaint
            {
                Color = new SKColor(255, 150, 0, (byte)(80 * shakeIntensity)),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 30 * shakeIntensity)
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius + 15 * shakeIntensity, outerGlow);
            
            // 边框高亮
            using var borderGlow = new SKPaint
            {
                Color = new SKColor(255, 220, 100, (byte)(200 * shakeIntensity)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3 * shakeIntensity
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius + 2, borderGlow);
        }
        
        if (bubble.Languages.Count > 1)
        {
            DrawLanguagePie(canvas, bubble, radius, finalBrightness);
        }
        else
        {
            using var paint = new SKPaint
            {
                Color = ApplyBrightness(GetLanguageColor(bubble.PrimaryLanguage), finalBrightness),
                IsAntialias = true
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius, paint);
        }
        
        if (bubble.BreathScale > 0 && breathOffset > 0)
        {
            using var glowPaint = new SKPaint
            {
                Color = ApplyBrightness(0xFF58A6FF, finalBrightness * 0.3f * breathOffset * 5),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 10)
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius, glowPaint);
        }
        
        if (bubble.IsHovered)
        {
            using var strokePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 180),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3
            };
            canvas.DrawCircle(bubble.X, bubble.Y, radius + 3, strokePaint);
        }
        
        if (bubble.IsBookmarked)
        {
            DrawBookmarkIndicator(canvas, bubble, radius);
        }
        
        if (bubble.IsDragging)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 80),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 15)
            };
            canvas.DrawCircle(bubble.X + 5, bubble.Y + 5, radius, shadowPaint);
        }
    }

    private void DrawLanguagePie(SKCanvas canvas, BubbleItem bubble, float radius, float brightness)
    {
        float totalBytes = bubble.Languages.Sum(l => l.Bytes);
        if (totalBytes == 0) totalBytes = 1;
        
        float currentAngle = 0;
        var center = new SKPoint(bubble.X, bubble.Y);
        
        foreach (var lang in bubble.Languages.Where(l => l.Bytes > 0))
        {
            float percentage = (float)lang.Bytes / totalBytes;
            if (percentage < 0.01f) continue;
            
            float sweepAngle = percentage * 360;
            
            using var path = new SKPath();
            path.MoveTo(center);
            path.ArcTo(new SKRect(bubble.X - radius, bubble.Y - radius, 
                                  bubble.X + radius, bubble.Y + radius),
                       currentAngle, sweepAngle, false);
            path.Close();
            
            using var paint = new SKPaint
            {
                Color = ApplyBrightness(GetLanguageColor(lang.Name), brightness),
                IsAntialias = true
            };
            
            canvas.DrawPath(path, paint);
            
            currentAngle += sweepAngle;
        }
    }

    private void DrawBookmarkIndicator(SKCanvas canvas, BubbleItem bubble, float radius)
    {
        float starX = bubble.X + radius * 0.7f;
        float starY = bubble.Y - radius * 0.7f;
        float starSize = radius * 0.2f;
        
        using var paint = new SKPaint
        {
            Color = SKColors.Yellow,
            IsAntialias = true
        };
        
        canvas.DrawCircle(starX, starY, starSize, paint);
        
        using var highlightPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        canvas.DrawLine(starX - starSize * 0.3f, starY, starX + starSize * 0.3f, starY, highlightPaint);
        canvas.DrawLine(starX, starY - starSize * 0.3f, starX, starY + starSize * 0.3f, highlightPaint);
    }

    private SKColor ApplyBrightness(uint color, float brightness)
    {
        var skColor = new SKColor(color);
        return new SKColor(
            (byte)(skColor.Red * brightness),
            (byte)(skColor.Green * brightness),
            (byte)(skColor.Blue * brightness),
            skColor.Alpha);
    }

    private uint GetLanguageColor(string? language)
    {
        return language?.ToLower() switch
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
    }
    
    /// <summary>
    /// 绘制长按进度指示器
    /// </summary>
    private void DrawLongPressIndicator(SKCanvas canvas, ClusterGroup cluster, float progress)
    {
        float radius = cluster.CurrentRadius + 8;
        float strokeWidth = 4;
        
        // 背景圆环 (灰色)
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100, 100),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth
        };
        canvas.DrawCircle(cluster.CenterX, cluster.CenterY, radius, bgPaint);
        
        // 进度圆环 - 颜色从黄到橙到红
        SKColor progressColor;
        if (progress < 0.33f)
            progressColor = new SKColor(255, 220, 0); // 黄色
        else if (progress < 0.66f)
            progressColor = new SKColor(255, 150, 0); // 橙色
        else
            progressColor = new SKColor(255, 60, 0);  // 红色
        
        using var progressPaint = new SKPaint
        {
            Color = progressColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round
        };
        
        // 绘制进度弧
        float sweepAngle = progress * 360;
        using var path = new SKPath();
        path.ArcTo(
            new SKRect(cluster.CenterX - radius, cluster.CenterY - radius,
                      cluster.CenterX + radius, cluster.CenterY + radius),
            -90, sweepAngle, false);
        canvas.DrawPath(path, progressPaint);
        
        // 百分比文字
        using var textPaint = new SKPaint
        {
            Color = progressColor,
            TextSize = 14,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        string percentText = $"{(int)(progress * 100)}%";
        canvas.DrawText(percentText, cluster.CenterX, cluster.CenterY + 5, textPaint);
    }
    
    /// <summary>
    /// 绘制粒子
    /// </summary>
    private void DrawParticle(SKCanvas canvas, Particle particle)
    {
        using var paint = new SKPaint
        {
            Color = particle.Color.WithAlpha((byte)(255 * particle.Life)),
            IsAntialias = true
        };
        
        canvas.DrawCircle(particle.X, particle.Y, particle.Size * particle.Life, paint);
        
        // 粒子发光效果
        using var glowPaint = new SKPaint
        {
            Color = particle.Color.WithAlpha((byte)(100 * particle.Life)),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, particle.Size * 2)
        };
        canvas.DrawCircle(particle.X, particle.Y, particle.Size * particle.Life * 1.5f, glowPaint);
    }
}
