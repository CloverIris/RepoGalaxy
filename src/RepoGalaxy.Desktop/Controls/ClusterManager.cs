using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 聚类管理器
/// 处理拖拽摇晃后的相似项目检索和聚类管理
/// </summary>
public class ClusterManager
{
    private readonly IRepositoryService _repositoryService;
    private readonly IRecommendationEngine _recommendationEngine;
    
    // 当前活跃的气泡
    private List<BubbleItem> _allBubbles = new();
    
    // 聚类状态
    private readonly Dictionary<long, ClusterGroup> _clusters = new();
    private readonly Dictionary<long, long> _bubbleToCluster = new();
    
    // 动画状态
    private readonly Dictionary<long, ClusterAnimationState> _animationStates = new();

    public ClusterManager(
        IRepositoryService repositoryService,
        IRecommendationEngine recommendationEngine)
    {
        _repositoryService = repositoryService;
        _recommendationEngine = recommendationEngine;
    }

    /// <summary>
    /// 设置当前所有气泡
    /// </summary>
    public void SetBubbles(List<BubbleItem> bubbles)
    {
        _allBubbles = bubbles;
    }

    #region 聚类创建

    /// <summary>
    /// 对种子气泡执行摇晃聚类
    /// </summary>
    public async Task<ClusterGroup?> CreateClusterAsync(BubbleItem seedBubble)
    {
        // 1. 查找相似项目
        var similar = await FindSimilarRepositoriesAsync(seedBubble);
        
        if (similar.Count < 2) // 至少需要一个相似项目
        {
            return null;
        }

        // 2. 创建聚类
        var cluster = new ClusterGroup
        {
            Id = seedBubble.Id,
            SeedBubble = seedBubble,
            CenterX = seedBubble.X,
            CenterY = seedBubble.Y,
            TargetRadius = CalculateClusterRadius(similar.Count + 1),
            Members = new List<ClusterMember>(),
            CreatedAt = DateTime.Now
        };

        // 3. 添加种子作为第一个成员
        cluster.Members.Add(new ClusterMember
        {
            Bubble = seedBubble,
            TargetX = cluster.CenterX,
            TargetY = cluster.CenterY,
            IsSeed = true,
            State = ClusterMemberState.InCluster
        });

        // 4. 添加相似项目作为成员
        foreach (var similarBubble in similar)
        {
            cluster.Members.Add(new ClusterMember
            {
                Bubble = similarBubble,
                StartX = similarBubble.X,
                StartY = similarBubble.Y,
                TargetX = cluster.CenterX + (float)(new Random().NextDouble() - 0.5) * 20,
                TargetY = cluster.CenterY + (float)(new Random().NextDouble() - 0.5) * 20,
                IsSeed = false,
                State = ClusterMemberState.FlyingIn,
                AnimationProgress = 0f
            });

            // 标记气泡属于哪个聚类
            _bubbleToCluster[similarBubble.Id] = cluster.Id;
        }

        // 标记种子气泡
        _bubbleToCluster[seedBubble.Id] = cluster.Id;
        
        // 保存聚类
        _clusters[cluster.Id] = cluster;
        
        // 初始化动画状态
        _animationStates[cluster.Id] = new ClusterAnimationState
        {
            Phase = ClusterAnimationPhase.Forming,
            Progress = 0f,
            Duration = 0.5f // 500ms 形成动画
        };

        return cluster;
    }

    /// <summary>
    /// 查找相似仓库
    /// </summary>
    private async Task<List<BubbleItem>> FindSimilarRepositoriesAsync(BubbleItem seed)
    {
        var similar = new List<BubbleItem>();
        
        try
        {
            // 使用推荐引擎获取相似项目
            var repoId = seed.Id;
            var recommendations = await _recommendationEngine.GetSimilarAsync(repoId, 15);
            
            // 映射回气泡
            foreach (var repo in recommendations)
            {
                var bubble = _allBubbles.FirstOrDefault(b => b.Id == repo.Id);
                if (bubble != null && bubble != seed)
                {
                    similar.Add(bubble);
                }
            }
        }
        catch
        {
            // 如果推荐引擎失败，使用本地相似度计算
            similar = FindLocalSimilarBubbles(seed);
        }

        // 限制数量，最多15个
        return similar.Take(15).ToList();
    }

    /// <summary>
    /// 本地相似度计算 (Fallback)
    /// </summary>
    private List<BubbleItem> FindLocalSimilarBubbles(BubbleItem seed)
    {
        return _allBubbles
            .Where(b => b != seed)
            .Select(b => new { Bubble = b, Score = CalculateLocalSimilarity(seed, b) })
            .Where(x => x.Score > 0.3) // 相似度阈值
            .OrderByDescending(x => x.Score)
            .Take(15)
            .Select(x => x.Bubble)
            .ToList();
    }

    /// <summary>
    /// 计算两个气泡的本地相似度
    /// </summary>
    private float CalculateLocalSimilarity(BubbleItem a, BubbleItem b)
    {
        float score = 0f;
        
        // 语言相同
        if (a.PrimaryLanguage == b.PrimaryLanguage)
        {
            score += 0.4f;
        }
        
        // 共同主题
        var commonTopics = a.Topics.Intersect(b.Topics, StringComparer.OrdinalIgnoreCase).Count();
        score += Math.Min(commonTopics * 0.15f, 0.45f);
        
        // Star 范围相近
        float starRatio = Math.Min(a.Stars, b.Stars) / (float)Math.Max(a.Stars, b.Stars);
        score += starRatio * 0.15f;
        
        return Math.Min(score, 1f);
    }

    /// <summary>
    /// 计算聚类大气泡的半径
    /// </summary>
    private float CalculateClusterRadius(int memberCount)
    {
        // 基础半径 + 成员数量增量
        float baseRadius = 60f;
        float increment = Math.Min(memberCount * 3f, 30f); // 最多增加30
        return baseRadius + increment;
    }

    #endregion

    #region 动画更新

    /// <summary>
    /// 更新聚类动画
    /// </summary>
    public void Update(float deltaTime)
    {
        var completedClusters = new List<long>();

        foreach (var kvp in _animationStates)
        {
            var clusterId = kvp.Key;
            var state = kvp.Value;
            
            if (!_clusters.TryGetValue(clusterId, out var cluster))
                continue;

            state.Progress += deltaTime / state.Duration;

            switch (state.Phase)
            {
                case ClusterAnimationPhase.Forming:
                    UpdateFormingAnimation(cluster, state.Progress);
                    if (state.Progress >= 1f)
                    {
                        state.Phase = ClusterAnimationPhase.Stable;
                        state.Progress = 0f;
                    }
                    break;
                    
                case ClusterAnimationPhase.Stable:
                    UpdateStableAnimation(cluster, deltaTime);
                    break;
                    
                case ClusterAnimationPhase.Breaking:
                    UpdateBreakingAnimation(cluster, state.Progress);
                    if (state.Progress >= 1f)
                    {
                        completedClusters.Add(clusterId);
                    }
                    break;
            }
        }

        // 清理已完成的破裂动画
        foreach (var clusterId in completedClusters)
        {
            RemoveCluster(clusterId);
        }
    }

    /// <summary>
    /// 更新形成动画 (成员飞入)
    /// </summary>
    private void UpdateFormingAnimation(ClusterGroup cluster, float progress)
    {
        // 种子气泡放大到聚类大小
        float seedScale = 1f + (0.5f * EaseOutBack(progress));
        cluster.SeedBubble.HoverScale = seedScale;
        
        // 其他成员飞入
        foreach (var member in cluster.Members.Where(m => !m.IsSeed))
        {
            if (member.State == ClusterMemberState.FlyingIn)
            {
                member.AnimationProgress = Math.Min(progress * 1.5f, 1f); // 飞入快一点
                
                float t = EaseIn(member.AnimationProgress);
                member.Bubble.X = Lerp(member.StartX, member.TargetX, t);
                member.Bubble.Y = Lerp(member.StartY, member.TargetY, t);
                
                // 飞入过程中逐渐变小
                float scale = 1f - (0.3f * t);
                member.Bubble.HoverScale = scale;
                
                if (member.AnimationProgress >= 1f)
                {
                    member.State = ClusterMemberState.InCluster;
                }
            }
        }
        
        // 更新聚类中心
        cluster.CurrentRadius = Lerp(0, cluster.TargetRadius, EaseOutBack(progress));
    }

    /// <summary>
    /// 更新稳定状态动画 (轻微呼吸)
    /// </summary>
    private void UpdateStableAnimation(ClusterGroup cluster, float deltaTime)
    {
        // 聚类整体轻微呼吸
        cluster.BreathPhase += deltaTime * 2f; // 2秒周期
        float breath = 1f + MathF.Sin(cluster.BreathPhase) * 0.03f;
        
        cluster.CurrentRadius = cluster.TargetRadius * breath;
        
        // 内部成员轻微浮动
        foreach (var member in cluster.Members)
        {
            if (member.State == ClusterMemberState.InCluster)
            {
                member.FloatPhase += deltaTime * 3f;
                float floatX = MathF.Sin(member.FloatPhase) * 2f;
                float floatY = MathF.Cos(member.FloatPhase * 0.7f) * 2f;
                
                member.Bubble.X = member.TargetX + floatX;
                member.Bubble.Y = member.TargetY + floatY;
                member.Bubble.HoverScale = 0.6f; // 聚类内部缩小
            }
        }
    }

    /// <summary>
    /// 更新破裂动画
    /// </summary>
    private void UpdateBreakingAnimation(ClusterGroup cluster, float progress)
    {
        // 聚类缩小
        cluster.CurrentRadius = Lerp(cluster.TargetRadius, 0, EaseIn(progress));
        
        // 成员四散
        foreach (var member in cluster.Members.Where(m => !m.IsSeed))
        {
            if (member.State == ClusterMemberState.Scattering)
            {
                // 向外弹射
                float t = EaseOut(progress);
                member.Bubble.X = Lerp(member.TargetX, member.ScatterTargetX, t);
                member.Bubble.Y = Lerp(member.TargetY, member.ScatterTargetY, t);
                
                // 恢复大小
                member.Bubble.HoverScale = Lerp(0.6f, 1f, t);
            }
        }
    }

    #endregion

    #region 聚类操作

    /// <summary>
    /// 点击聚类 - 展开多选模式
    /// </summary>
    public void ExpandCluster(long clusterId)
    {
        if (!_clusters.TryGetValue(clusterId, out var cluster))
            return;

        // 展开动画
        if (_animationStates.TryGetValue(clusterId, out var state))
        {
            state.Phase = ClusterAnimationPhase.Expanded;
            state.Progress = 0f;
            state.Duration = 0.3f;
        }

        // 成员展开排列
        int count = cluster.Members.Count;
        float radius = cluster.TargetRadius * 1.5f;
        
        for (int i = 0; i < count; i++)
        {
            var member = cluster.Members[i];
            if (member.IsSeed) continue;
            
            float angle = (float)(2 * Math.PI * i / count);
            member.TargetX = cluster.CenterX + MathF.Cos(angle) * radius;
            member.TargetY = cluster.CenterY + MathF.Sin(angle) * radius;
            member.State = ClusterMemberState.Expanded;
        }
    }

    /// <summary>
    /// 长按破裂聚类
    /// </summary>
    public void BreakCluster(long clusterId, float explosionPower = 10f)
    {
        if (!_clusters.TryGetValue(clusterId, out var cluster))
            return;

        // 启动破裂动画
        if (_animationStates.TryGetValue(clusterId, out var state))
        {
            state.Phase = ClusterAnimationPhase.Breaking;
            state.Progress = 0f;
            state.Duration = 0.8f;
        }

        // 为每个非种子成员计算弹射目标
        var random = new Random();
        foreach (var member in cluster.Members.Where(m => !m.IsSeed))
        {
            member.State = ClusterMemberState.Scattering;
            
            // 随机方向弹射
            float angle = (float)(random.NextDouble() * 2 * Math.PI);
            float distance = 100f + (float)random.NextDouble() * 100f;
            
            member.ScatterTargetX = cluster.CenterX + MathF.Cos(angle) * distance;
            member.ScatterTargetY = cluster.CenterY + MathF.Sin(angle) * distance;
            
            // 设置初速度
            member.Bubble.VelocityX = MathF.Cos(angle) * explosionPower;
            member.Bubble.VelocityY = MathF.Sin(angle) * explosionPower;
        }

        // 种子恢复
        cluster.SeedBubble.HoverScale = 1f;
    }

    /// <summary>
    /// 移除聚类
    /// </summary>
    private void RemoveCluster(long clusterId)
    {
        if (!_clusters.TryGetValue(clusterId, out var cluster))
            return;

        // 清除所有成员的聚类标记
        foreach (var member in cluster.Members)
        {
            _bubbleToCluster.Remove(member.Bubble.Id);
        }

        _clusters.Remove(clusterId);
        _animationStates.Remove(clusterId);
    }

    #endregion

    #region 查询方法

    /// <summary>
    /// 获取气泡所属的聚类
    /// </summary>
    public ClusterGroup? GetClusterForBubble(long bubbleId)
    {
        if (_bubbleToCluster.TryGetValue(bubbleId, out var clusterId))
        {
            return _clusters.GetValueOrDefault(clusterId);
        }
        return null;
    }

    /// <summary>
    /// 检查气泡是否在聚类中
    /// </summary>
    public bool IsBubbleInCluster(long bubbleId)
    {
        return _bubbleToCluster.ContainsKey(bubbleId);
    }

    /// <summary>
    /// 获取所有活跃聚类
    /// </summary>
    public IEnumerable<ClusterGroup> GetAllClusters()
    {
        return _clusters.Values;
    }

    /// <summary>
    /// 获取聚类中心的气泡
    /// </summary>
    public BubbleItem? GetClusterCenterBubble(long clusterId)
    {
        return _clusters.TryGetValue(clusterId, out var cluster) ? cluster.SeedBubble : null;
    }

    #endregion

    #region 动画缓动函数

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Math.Clamp(t, 0f, 1f);
    }

    private static float EaseIn(float t)
    {
        return t * t;
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3) + c1 * MathF.Pow(t - 1f, 2);
    }

    #endregion
}

/// <summary>
/// 聚类组
/// </summary>
public class ClusterGroup
{
    public long Id { get; set; }
    public BubbleItem SeedBubble { get; set; } = null!;
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float TargetRadius { get; set; }
    public float CurrentRadius { get; set; }
    public List<ClusterMember> Members { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public float BreathPhase { get; set; }
}

/// <summary>
/// 聚类成员
/// </summary>
public class ClusterMember
{
    public BubbleItem Bubble { get; set; } = null!;
    public bool IsSeed { get; set; }
    public ClusterMemberState State { get; set; }
    
    // 动画位置
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float AnimationProgress { get; set; }
    
    // 展开/破裂用
    public float ScatterTargetX { get; set; }
    public float ScatterTargetY { get; set; }
    public float FloatPhase { get; set; }
}

/// <summary>
/// 聚类成员状态
/// </summary>
public enum ClusterMemberState
{
    FlyingIn,    // 飞入中
    InCluster,   // 已在聚类中
    Expanded,    // 展开状态
    Scattering   // 四散中
}

/// <summary>
/// 聚类动画阶段
/// </summary>
public enum ClusterAnimationPhase
{
    Forming,    // 形成中
    Stable,     // 稳定状态
    Expanded,   // 展开状态
    Breaking    // 破裂中
}

/// <summary>
/// 聚类动画状态
/// </summary>
public class ClusterAnimationState
{
    public ClusterAnimationPhase Phase { get; set; }
    public float Progress { get; set; }
    public float Duration { get; set; }
}
