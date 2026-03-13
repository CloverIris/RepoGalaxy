using System;
using System.Collections.Generic;
using System.Linq;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// Apple Watch 风格蜂窝布局引擎
/// 六边形网格 + 中心放大镜效果 + 物理碰撞
/// </summary>
public class HoneycombLayoutEngine
{
    #region 配置参数
    
    /// <summary>基础圆形半径</summary>
    public float BaseRadius { get; set; } = 40f;
    
    /// <summary>圆形间距系数 (1.0 = 相切, <1.0 = 重叠)</summary>
    public float SpacingFactor { get; set; } = 0.85f;
    
    /// <summary>放大镜效果强度 (0 = 无效果, 1.0 = 最大效果)</summary>
    public float MagnificationStrength { get; set; } = 0.5f;
    
    /// <summary>放大镜影响半径 (像素)</summary>
    public float MagnificationRadius { get; set; } = 250f;
    
    /// <summary>是否启用物理碰撞</summary>
    public bool EnablePhysics { get; set; } = true;
    
    /// <summary>物理迭代次数 (越高越稳定)</summary>
    public int PhysicsIterations { get; set; } = 3;
    
    /// <summary>边界弹性系数</summary>
    public float BoundaryElasticity { get; set; } = 0.6f;
    
    #endregion

    #region 布局状态
    
    private float _viewportWidth;
    private float _viewportHeight;
    private float _centerX;
    private float _centerY;
    private List<HoneycombCell> _cells = new();
    private Dictionary<long, BubbleItem> _bubbles = new();
    
    // 六边形网格参数
    private float _hexWidth;
    private float _hexHeight;
    private float _hexRadius;
    
    #endregion

    #region 初始化与布局

    /// <summary>
    /// 初始化布局引擎
    /// </summary>
    public void Initialize(float viewportWidth, float viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _centerX = viewportWidth / 2;
        _centerY = viewportHeight / 2;
        
        // 计算六边形参数
        // 六边形宽 = 2 * 半径 * SpacingFactor
        // 六边形高 = sqrt(3) * 半径 * SpacingFactor
        _hexRadius = BaseRadius * SpacingFactor;
        _hexWidth = _hexRadius * 2;
        _hexHeight = _hexRadius * (float)Math.Sqrt(3);
    }

    /// <summary>
    /// 执行完整布局计算
    /// </summary>
    public void Layout(List<BubbleItem> bubbles)
    {
        if (bubbles.Count == 0) return;

        // 1. 双维度排序 (Y=时间新→旧, X=Star多→少)
        var sortedBubbles = SortByTimeAndStars(bubbles);
        
        // 2. 分配到六边形网格
        AssignToHoneycombGrid(sortedBubbles);
        
        // 3. 计算基础位置 (六边形网格)
        CalculateBasePositions();
        
        // 4. 应用放大镜效果
        ApplyMagnification();
        
        // 5. 物理碰撞解决 (允许轻微重叠但防止严重穿透)
        if (EnablePhysics)
        {
            ResolveCollisions();
        }
        
        // 6. 更新气泡位置
        UpdateBubblePositions();
    }

    #endregion

    #region 双维度排序

    /// <summary>
    /// 按时间+Star双维度排序
    /// 从上到下: 新→旧
    /// 从左到右: Star多→少
    /// </summary>
    private List<BubbleItem> SortByTimeAndStars(List<BubbleItem> bubbles)
    {
        // 先按时间分组 (新→旧)
        var timeGroups = bubbles
            .GroupBy(b => GetTimeBucket(b.UpdatedAt))
            .OrderByDescending(g => g.Key)
            .ToList();
        
        var result = new List<BubbleItem>();
        
        foreach (var group in timeGroups)
        {
            // 每个时间组内按 Star 排序 (多→少)
            var sorted = group.OrderByDescending(b => b.Stars).ToList();
            result.AddRange(sorted);
        }
        
        return result;
    }

    /// <summary>
    /// 将时间分到不同的"桶"中
    /// 桶越小，时间维度越精细
    /// </summary>
    private DateTime GetTimeBucket(DateTimeOffset updatedAt)
    {
        // 按天分组 (可以调整为按周/按月)
        return updatedAt.Date;
    }

    #endregion

    #region 六边形网格分配

    /// <summary>
    /// 将气泡分配到六边形网格位置
    /// </summary>
    private void AssignToHoneycombGrid(List<BubbleItem> bubbles)
    {
        _cells.Clear();
        _bubbles = bubbles.ToDictionary(b => b.Id, b => b);
        
        // 计算需要的环数
        // 第0环: 1个 (中心)
        // 第1环: 6个 
        // 第2环: 12个
        // 第n环: 6*n 个
        int ringCount = CalculateRequiredRings(bubbles.Count);
        
        int bubbleIndex = 0;
        
        // 中心位置 (第0环)
        if (bubbleIndex < bubbles.Count)
        {
            _cells.Add(new HoneycombCell
            {
                BubbleId = bubbles[bubbleIndex].Id,
                Ring = 0,
                IndexInRing = 0,
                GridQ = 0, // 轴向坐标 q
                GridR = 0  // 轴向坐标 r
            });
            bubbleIndex++;
        }
        
        // 外环
        for (int ring = 1; ring <= ringCount && bubbleIndex < bubbles.Count; ring++)
        {
            int cellsInRing = 6 * ring;
            
            for (int i = 0; i < cellsInRing && bubbleIndex < bubbles.Count; i++)
            {
                // 计算轴向坐标 (Axial Coordinates)
                var (q, r) = GetAxialCoordinates(ring, i);
                
                _cells.Add(new HoneycombCell
                {
                    BubbleId = bubbles[bubbleIndex].Id,
                    Ring = ring,
                    IndexInRing = i,
                    GridQ = q,
                    GridR = r
                });
                
                bubbleIndex++;
            }
        }
    }

    /// <summary>
    /// 计算需要的环数
    /// 总单元数 = 1 + 6 + 12 + ... + 6*n = 1 + 3*n*(n+1)
    /// </summary>
    private int CalculateRequiredRings(int totalCount)
    {
        if (totalCount <= 1) return 0;
        
        // 解方程: 1 + 3*n*(n+1) >= totalCount
        // 近似: n^2 + n - (totalCount-1)/3 >= 0
        double a = 1, b = 1, c = -(totalCount - 1) / 3.0;
        double n = (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
        
        return (int)Math.Ceiling(n);
    }

    /// <summary>
    /// 计算六边形轴向坐标
    /// 从顶部开始，顺时针方向
    /// </summary>
    private (int q, int r) GetAxialCoordinates(int ring, int index)
    {
        // 每个边有 ring 个单元
        // 6条边，每边 ring 个，共 6*ring 个单元
        
        int side = index / ring;      // 第几条边 (0-5)
        int pos = index % ring;       // 在边上的位置
        
        // 6个方向向量 (轴向坐标)
        // 从顶部开始，顺时针
        var directions = new[]
        {
            (0, -1),   // 上
            (1, -1),   // 右上
            (1, 0),    // 右下
            (0, 1),    // 下
            (-1, 1),   // 左下
            (-1, 0)    // 左上
        };
        
        // 起始位置 (上顶点)
        int q = 0;
        int r = -ring;
        
        // 沿各边移动
        for (int s = 0; s < side; s++)
        {
            q += directions[s].Item1 * ring;
            r += directions[s].Item2 * ring;
        }
        
        // 在当前边上移动
        if (pos > 0)
        {
            q += directions[side].Item1 * pos;
            r += directions[side].Item2 * pos;
        }
        
        return (q, r);
    }

    #endregion

    #region 基础位置计算

    /// <summary>
    /// 计算六边形网格的基础位置
    /// 使用轴向坐标转像素坐标
    /// </summary>
    private void CalculateBasePositions()
    {
        foreach (var cell in _cells)
        {
            // 轴向坐标转像素坐标 (Pointy-topped hexagons)
            // x = (q + r/2) * width
            // y = r * height * 3/4
            
            float x = (cell.GridQ + cell.GridR * 0.5f) * _hexWidth;
            float y = cell.GridR * _hexHeight * 0.75f;
            
            // 偏移到视口中心
            cell.BaseX = _centerX + x;
            cell.BaseY = _centerY + y;
            
            // 计算基础半径 (根据Star数)
            if (_bubbles.TryGetValue(cell.BubbleId, out var bubble))
            {
                cell.BaseRadius = bubble.Radius;
            }
            else
            {
                cell.BaseRadius = BaseRadius;
            }
        }
    }

    #endregion

    #region 放大镜效果

    /// <summary>
    /// 应用中心放大镜效果
    /// 距离中心越近，圆形越大
    /// </summary>
    private void ApplyMagnification()
    {
        foreach (var cell in _cells)
        {
            // 计算到中心的距离
            float dx = cell.BaseX - _centerX;
            float dy = cell.BaseY - _centerY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            
            // 计算放大系数 (0-1)
            float t = 1f - Math.Min(distance / MagnificationRadius, 1f);
            
            // 应用平滑曲线 (EaseOutQuad)
            float magnification = 1f + MagnificationStrength * (1f - (1f - t) * (1f - t));
            
            // 应用放大镜效果
            cell.MagnifiedRadius = cell.BaseRadius * magnification;
            
            // 轻微向中心拉拢 (视觉聚焦)
            float pullFactor = MagnificationStrength * 0.1f * t;
            cell.CurrentX = cell.BaseX - dx * pullFactor;
            cell.CurrentY = cell.BaseY - dy * pullFactor;
        }
    }

    #endregion

    #region 物理碰撞解决

    /// <summary>
    /// 解决圆形之间的碰撞
    /// 允许轻微重叠，但防止严重穿透
    /// </summary>
    private void ResolveCollisions()
    {
        for (int iteration = 0; iteration < PhysicsIterations; iteration++)
        {
            bool hasCollision = false;
            
            for (int i = 0; i < _cells.Count; i++)
            {
                var cellA = _cells[i];
                
                for (int j = i + 1; j < _cells.Count; j++)
                {
                    var cellB = _cells[j];
                    
                    if (ResolvePairCollision(cellA, cellB))
                    {
                        hasCollision = true;
                    }
                }
                
                // 边界碰撞
                ResolveBoundaryCollision(cellA);
            }
            
            // 如果没有碰撞，提前结束
            if (!hasCollision) break;
        }
    }

    /// <summary>
    /// 解决两个圆形之间的碰撞
    /// 返回是否发生了碰撞
    /// </summary>
    private bool ResolvePairCollision(HoneycombCell a, HoneycombCell b)
    {
        float dx = b.CurrentX - a.CurrentX;
        float dy = b.CurrentY - a.CurrentY;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
        
        // 最小间距 (允许轻微重叠)
        float minDistance = (a.MagnifiedRadius + b.MagnifiedRadius) * 0.9f;
        
        if (distance < minDistance && distance > 0.001f)
        {
            // 计算分离向量
            float overlap = minDistance - distance;
            float nx = dx / distance;
            float ny = dy / distance;
            
            // 按半径比例分配分离
            float totalRadius = a.MagnifiedRadius + b.MagnifiedRadius;
            float ratioA = b.MagnifiedRadius / totalRadius;
            float ratioB = a.MagnifiedRadius / totalRadius;
            
            // 应用分离 (弹簧阻尼效果)
            float damping = 0.5f;
            
            a.CurrentX -= nx * overlap * ratioA * damping;
            a.CurrentY -= ny * overlap * ratioA * damping;
            b.CurrentX += nx * overlap * ratioB * damping;
            b.CurrentY += ny * overlap * ratioB * damping;
            
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 解决边界碰撞
    /// </summary>
    private void ResolveBoundaryCollision(HoneycombCell cell)
    {
        float r = cell.MagnifiedRadius;
        
        // 左边界
        if (cell.CurrentX < r)
        {
            cell.CurrentX = r + (r - cell.CurrentX) * BoundaryElasticity;
        }
        // 右边界
        if (cell.CurrentX > _viewportWidth - r)
        {
            cell.CurrentX = _viewportWidth - r - (cell.CurrentX - (_viewportWidth - r)) * BoundaryElasticity;
        }
        // 上边界
        if (cell.CurrentY < r)
        {
            cell.CurrentY = r + (r - cell.CurrentY) * BoundaryElasticity;
        }
        // 下边界
        if (cell.CurrentY > _viewportHeight - r)
        {
            cell.CurrentY = _viewportHeight - r - (cell.CurrentY - (_viewportHeight - r)) * BoundaryElasticity;
        }
    }

    #endregion

    #region 位置更新

    /// <summary>
    /// 更新气泡的实际位置
    /// </summary>
    private void UpdateBubblePositions()
    {
        foreach (var cell in _cells)
        {
            if (_bubbles.TryGetValue(cell.BubbleId, out var bubble))
            {
                bubble.X = cell.CurrentX;
                bubble.Y = cell.CurrentY;
                // 更新视觉半径 (放大镜效果)
                bubble.Radius = cell.MagnifiedRadius;
            }
        }
    }

    /// <summary>
    /// 获取中心位置的气泡 (最大最清晰的)
    /// </summary>
    public BubbleItem? GetCenterBubble()
    {
        if (_cells.Count == 0) return null;
        
        // 找到离中心最近的
        var centerCell = _cells
            .OrderBy(c => 
            {
                float dx = c.CurrentX - _centerX;
                float dy = c.CurrentY - _centerY;
                return dx * dx + dy * dy;
            })
            .First();
        
        return _bubbles.GetValueOrDefault(centerCell.BubbleId);
    }

    #endregion

    #region 辅助类

    /// <summary>
    /// 六边形网格单元
    /// </summary>
    private class HoneycombCell
    {
        public long BubbleId { get; set; }
        public int Ring { get; set; }
        public int IndexInRing { get; set; }
        public int GridQ { get; set; }
        public int GridR { get; set; }
        
        public float BaseX { get; set; }
        public float BaseY { get; set; }
        public float BaseRadius { get; set; }
        
        public float CurrentX { get; set; }
        public float CurrentY { get; set; }
        public float MagnifiedRadius { get; set; }
    }

    #endregion
}

/// <summary>
/// 蜂窝布局配置
/// </summary>
public class HoneycombLayoutConfig
{
    /// <summary>基础圆形半径 (默认 40)</summary>
    public float BaseRadius { get; set; } = 40f;
    
    /// <summary>间距系数 (0.85 = 轻微重叠)</summary>
    public float SpacingFactor { get; set; } = 0.85f;
    
    /// <summary>放大镜强度 (0.5 = 中等)</summary>
    public float MagnificationStrength { get; set; } = 0.5f;
    
    /// <summary>放大镜半径 (像素)</summary>
    public float MagnificationRadius { get; set; } = 250f;
    
    /// <summary>启用物理碰撞</summary>
    public bool EnablePhysics { get; set; } = true;
    
    /// <summary>物理迭代次数</summary>
    public int PhysicsIterations { get; set; } = 3;
}
