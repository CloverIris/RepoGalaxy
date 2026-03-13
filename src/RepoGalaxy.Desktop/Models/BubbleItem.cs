using System;
using System.Collections.Generic;
using System.Linq;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Models;

/// <summary>
/// 气泡云中的仓库可视化项
/// 包含完整的数据到视觉属性映射
/// </summary>
public class BubbleItem
{
    // 基础数据
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
    public string Description { get; set; } = string.Empty;
    
    // GitHub数据
    public long Stars { get; set; }
    public long Forks { get; set; }
    public long Watchers { get; set; }
    public int OpenIssues { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    public string? PrimaryLanguage { get; set; }
    public List<LanguageInfo> Languages { get; set; } = new();
    public List<string> Topics { get; set; } = new();
    public string HtmlUrl { get; set; } = string.Empty;
    
    // 视觉属性 - 由数据计算得出
    
    /// <summary>圆形半径 (16-72px)，由Star数非线性映射</summary>
    public float Radius { get; set; } = 32f;
    
    /// <summary>亮度 (0.4-1.0)，由新旧程度映射</summary>
    public float Brightness { get; set; } = 1.0f;
    
    /// <summary>闪烁频率 (0-2Hz)，由活跃度映射</summary>
    public float TwinkleFrequency { get; set; } = 0f;
    
    /// <summary>呼吸幅度 (0-0.15)，由Fork数映射</summary>
    public float BreathScale { get; set; } = 0f;
    
    /// <summary>呼吸周期 (秒)</summary>
    public float BreathPeriod { get; set; } = 2f;
    
    // 物理属性
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Mass { get; set; } = 1f;
    
    // 交互状态
    public bool IsHovered { get; set; }
    public bool IsDragging { get; set; }
    public bool IsStopped { get; set; }
    public float HoverScale { get; set; } = 1.0f;
    public bool IsBookmarked { get; set; }
    
    // 动画状态
    public float TwinklePhase { get; set; }
    public float BreathPhase { get; set; }
    
    /// <summary>
    /// 从Repository创建BubbleItem，计算所有视觉属性
    /// </summary>
    public static BubbleItem FromRepository(Repository repo)
    {
        var item = new BubbleItem
        {
            Id = repo.Id,
            Name = repo.Name,
            Owner = repo.Owner,
            Description = repo.Description ?? string.Empty,
            Stars = repo.Stars,
            Forks = repo.Forks,
            Watchers = repo.Watchers,
            OpenIssues = repo.OpenIssues,
            CreatedAt = repo.CreatedAt,
            UpdatedAt = repo.UpdatedAt,
            LastPushedAt = repo.LastPushedAt,
            PrimaryLanguage = repo.PrimaryLanguage,
            Languages = repo.Languages ?? new List<LanguageInfo>(),
            Topics = repo.Topics ?? new List<string>(),
            HtmlUrl = repo.HtmlUrl,
            
            // 计算视觉属性
            Radius = CalculateRadius(repo.Stars),
            Brightness = CalculateBrightness(repo.LastPushedAt ?? repo.UpdatedAt),
            TwinkleFrequency = CalculateTwinkleFrequency(repo),
            BreathScale = CalculateBreathScale(repo.Forks),
            BreathPeriod = CalculateBreathPeriod(repo.Forks),
            Mass = CalculateMass(repo.Stars),
            IsBookmarked = repo.IsBookmarked
        };
        
        return item;
    }
    
    /// <summary>
    /// Star数 → 半径 (非线性阶梯映射)
    /// </summary>
    private static float CalculateRadius(long stars)
    {
        return stars switch
        {
            0 => 16f,
            <= 100 => 24f,
            <= 200 => 32f,
            <= 400 => 40f,
            <= 500 => 48f,
            <= 1000 => 56f,
            <= 10000 => 64f,
            _ => 72f
        };
    }
    
    /// <summary>
    /// 最后提交时间 → 亮度 (新→亮，旧→暗)
    /// </summary>
    private static float CalculateBrightness(DateTimeOffset lastPushed)
    {
        var days = (DateTimeOffset.Now - lastPushed).TotalDays;
        return days switch
        {
            <= 1 => 1.0f,      // < 24h: 最亮
            <= 7 => 0.85f,     // 1-7天: 明亮
            <= 30 => 0.70f,    // 1-4周: 适中
            <= 90 => 0.55f,    // 1-3月: 较暗
            _ => 0.40f          // > 3月: 暗淡
        };
    }
    
    /// <summary>
    /// 综合活跃度 → 闪烁频率
    /// 活跃度 = Star数/100 + Fork数/10 + 近期更新奖励
    /// </summary>
    private static float CalculateTwinkleFrequency(Repository repo)
    {
        var baseActivity = repo.Stars / 100.0 + repo.Forks / 10.0;
        
        // 近期更新奖励
        var days = (DateTimeOffset.Now - (repo.LastPushedAt ?? repo.UpdatedAt)).TotalDays;
        var recencyBonus = days switch
        {
            <= 1 => 1.0,
            <= 7 => 0.5,
            <= 30 => 0.2,
            _ => 0
        };
        
        var activity = baseActivity + recencyBonus;
        
        // 映射到 0-2Hz
        return activity switch
        {
            <= 0.5 => 0f,      // 不闪烁
            <= 2 => 0.5f,      // 低频
            <= 5 => 1.0f,      // 中频
            _ => 2.0f           // 高频
        };
    }
    
    /// <summary>
    /// Fork数 → 呼吸幅度
    /// </summary>
    private static float CalculateBreathScale(long forks)
    {
        return forks switch
        {
            0 => 0f,
            <= 100 => 0.05f,
            <= 1000 => 0.10f,
            _ => 0.15f
        };
    }
    
    /// <summary>
    /// Fork数 → 呼吸周期 (大项目呼吸更慢)
    /// </summary>
    private static float CalculateBreathPeriod(long forks)
    {
        return forks switch
        {
            <= 100 => 2f,
            <= 1000 => 3f,
            _ => 4f
        };
    }
    
    /// <summary>
    /// Star数 → 质量 (大泡更惰性)
    /// </summary>
    private static float CalculateMass(long stars)
    {
        return 0.5f + (float)Math.Log10(stars + 10) * 0.5f;
    }
    
    /// <summary>
    /// 获取语言颜色 (GitHub官方色)
    /// </summary>
    public static uint GetLanguageColor(string? language)
    {
        return language?.ToLower() switch
        {
            "rust" => 0xFFdea584,
            "python" => 0xFF3572A5,
            "javascript" => 0xFFf1e05a,
            "typescript" => 0xFF3178c6,
            "go" => 0xFF00ADD8,
            "java" => 0xFFb07219,
            "c++" or "cpp" => 0xFFf34b7d,
            "c#" or "csharp" => 0xFF178600,
            "c" => 0xFF555555,
            "ruby" => 0xFF701516,
            "swift" => 0xFFffac45,
            "kotlin" => 0xFFA97BFF,
            "php" => 0xFF4F5D95,
            _ => 0xFF8b949e  // 默认灰色
        };
    }
}

/// <summary>
/// 气泡物理引擎状态
/// </summary>
public class BubblePhysics
{
    public float Friction { get; set; } = 0.99f;
    public float Elasticity { get; set; } = 0.8f;
    public float RepulsionRadius { get; set; } = 10f;
    public float RepulsionStrength { get; set; } = 0.5f;
    public float MaxSpeed { get; set; } = 0.5f;
    public float RandomForce { get; set; } = 0.02f;
}
