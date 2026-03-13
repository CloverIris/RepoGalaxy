using System;
using System.Collections.Generic;
using System.Linq;
using RepoGalaxy.Desktop.Models;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 气泡物理引擎
/// Android 4.3 彩蛋风格慵懒漂浮 + 摇一摇聚类交互
/// </summary>
public class BubblePhysicsEngine
{
    #region 物理参数

    /// <summary>最大速度 (px/frame)</summary>
    public float MaxSpeed { get; set; } = 0.5f;
    
    /// <summary>摩擦力 (0-1, 越接近1越滑)</summary>
    public float Friction { get; set; } = 0.99f;
    
    /// <summary>随机力强度</summary>
    public float RandomForce { get; set; } = 0.02f;
    
    /// <summary>避让力强度</summary>
    public float RepulsionStrength { get; set; } = 0.5f;
    
    /// <summary>避让检测半径 (像素)</summary>
    public float RepulsionRadius { get; set; } = 10f;
    
    /// <summary>边界弹性系数</summary>
    public float BoundaryElasticity { get; set; } = 0.8f;
    
    /// <summary>漂浮暂停时的阻尼</summary>
    public float HoverDamping { get; set; } = 0.8f;

    #endregion

    #region 状态

    private readonly Random _random = new();
    private float _viewportWidth;
    private float _viewportHeight;
    private List<BubbleItem> _bubbles = new();
    
    // 摇一摇检测
    private readonly Dictionary<long, ShakeState> _shakeStates = new();

    #endregion

    #region 初始化

    public void Initialize(float viewportWidth, float viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
    }

    public void SetBubbles(List<BubbleItem> bubbles)
    {
        _bubbles = bubbles;
        
        // 初始化随机速度
        foreach (var bubble in _bubbles)
        {
            if (Math.Abs(bubble.VelocityX) < 0.01f && Math.Abs(bubble.VelocityY) < 0.01f)
            {
                bubble.VelocityX = (float)(_random.NextDouble() - 0.5) * MaxSpeed * 2;
                bubble.VelocityY = (float)(_random.NextDouble() - 0.5) * MaxSpeed * 2;
            }
        }
    }

    #endregion

    #region 主更新循环

    /// <summary>
    /// 更新物理状态 (每帧调用)
    /// </summary>
    public void Update(float deltaTime = 1f)
    {
        foreach (var bubble in _bubbles)
        {
            if (bubble.IsDragging || bubble.IsStopped)
                continue;

            // 1. 应用随机力 (慵懒感)
            ApplyRandomForce(bubble, deltaTime);
            
            // 2. 应用避让力 (防止重叠)
            ApplyRepulsionForces(bubble, deltaTime);
            
            // 3. 应用摩擦力
            ApplyFriction(bubble, deltaTime);
            
            // 4. 限制最大速度
            ClampVelocity(bubble);
            
            // 5. 更新位置
            UpdatePosition(bubble, deltaTime);
            
            // 6. 边界处理
            ApplyBoundaryConstraints(bubble);
        }
        
        // 更新摇一摇检测
        UpdateShakeDetection();
    }

    #endregion

    #region 力的计算

    /// <summary>
    /// 应用随机力 - 创造慵懒漂浮感
    /// </summary>
    private void ApplyRandomForce(BubbleItem bubble, float deltaTime)
    {
        // Perlin Noise 风格的平滑随机 (简化版)
        // 使用累积随机来创造更自然的运动
        
        float noiseX = (float)(_random.NextDouble() - 0.5) * RandomForce * deltaTime;
        float noiseY = (float)(_random.NextDouble() - 0.5) * RandomForce * deltaTime;
        
        bubble.VelocityX += noiseX;
        bubble.VelocityY += noiseY;
    }

    /// <summary>
    /// 应用避让力 - 防止气泡严重重叠
    /// 但允许轻微接触 (为摇一摇聚类预留)
    /// </summary>
    private void ApplyRepulsionForces(BubbleItem bubble, float deltaTime)
    {
        foreach (var other in _bubbles)
        {
            if (other == bubble) continue;
            
            float dx = other.X - bubble.X;
            float dy = other.Y - bubble.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            
            // 检测距离 = 两圆半径和 + 额外检测半径
            float minDistance = bubble.Radius + other.Radius + RepulsionRadius;
            
            if (distance < minDistance && distance > 0.001f)
            {
                // 计算排斥方向
                float nx = dx / distance;
                float ny = dy / distance;
                
                // 排斥力与距离成反比 (越近越强烈)
                float force = RepulsionStrength * (1f - distance / minDistance);
                
                // 质量影响 (大质量气泡更难被推动)
                float massRatio = other.Mass / (bubble.Mass + other.Mass);
                
                bubble.VelocityX -= nx * force * massRatio * deltaTime;
                bubble.VelocityY -= ny * force * massRatio * deltaTime;
            }
        }
    }

    /// <summary>
    /// 应用摩擦力
    /// </summary>
    private void ApplyFriction(BubbleItem bubble, float deltaTime)
    {
        // 如果是悬停状态，使用更强的阻尼
        float friction = bubble.IsHovered ? HoverDamping : Friction;
        
        bubble.VelocityX *= (float)Math.Pow(friction, deltaTime);
        bubble.VelocityY *= (float)Math.Pow(friction, deltaTime);
    }

    /// <summary>
    /// 限制最大速度
    /// </summary>
    private void ClampVelocity(BubbleItem bubble)
    {
        float speed = (float)Math.Sqrt(bubble.VelocityX * bubble.VelocityX + bubble.VelocityY * bubble.VelocityY);
        
        if (speed > MaxSpeed)
        {
            float ratio = MaxSpeed / speed;
            bubble.VelocityX *= ratio;
            bubble.VelocityY *= ratio;
        }
    }

    /// <summary>
    /// 更新位置
    /// </summary>
    private void UpdatePosition(BubbleItem bubble, float deltaTime)
    {
        bubble.X += bubble.VelocityX * deltaTime;
        bubble.Y += bubble.VelocityY * deltaTime;
    }

    /// <summary>
    /// 边界约束 - 柔和反弹
    /// </summary>
    private void ApplyBoundaryConstraints(BubbleItem bubble)
    {
        float r = bubble.Radius;
        bool bounced = false;
        
        // 左边界
        if (bubble.X < r)
        {
            bubble.X = r + (r - bubble.X) * BoundaryElasticity;
            bubble.VelocityX = Math.Abs(bubble.VelocityX) * BoundaryElasticity;
            bounced = true;
        }
        // 右边界
        else if (bubble.X > _viewportWidth - r)
        {
            bubble.X = _viewportWidth - r - (bubble.X - (_viewportWidth - r)) * BoundaryElasticity;
            bubble.VelocityX = -Math.Abs(bubble.VelocityX) * BoundaryElasticity;
            bounced = true;
        }
        
        // 上边界
        if (bubble.Y < r)
        {
            bubble.Y = r + (r - bubble.Y) * BoundaryElasticity;
            bubble.VelocityY = Math.Abs(bubble.VelocityY) * BoundaryElasticity;
            bounced = true;
        }
        // 下边界
        else if (bubble.Y > _viewportHeight - r)
        {
            bubble.Y = _viewportHeight - r - (bubble.Y - (_viewportHeight - r)) * BoundaryElasticity;
            bubble.VelocityY = -Math.Abs(bubble.VelocityY) * BoundaryElasticity;
            bounced = true;
        }
        
        // 如果反弹了，加一点随机扰动 (避免卡在角落)
        if (bounced)
        {
            bubble.VelocityX += (float)(_random.NextDouble() - 0.5) * 0.1f;
            bubble.VelocityY += (float)(_random.NextDouble() - 0.5) * 0.1f;
        }
    }

    #endregion

    #region 摇一摇检测

    /// <summary>
    /// 摇一摇状态
    /// </summary>
    private class ShakeState
    {
        public Queue<(float X, float Y, DateTime Time)> PositionHistory = new();
        public bool IsShaking { get; set; }
        public DateTime LastShakeTime { get; set; }
    }

    /// <summary>
    /// 记录气泡位置用于摇一摇检测
    /// </summary>
    public void RecordBubblePosition(BubbleItem bubble, float x, float y)
    {
        if (!_shakeStates.TryGetValue(bubble.Id, out var state))
        {
            state = new ShakeState();
            _shakeStates[bubble.Id] = state;
        }
        
        state.PositionHistory.Enqueue((x, y, DateTime.Now));
        
        // 保持最近10个位置
        while (state.PositionHistory.Count > 10)
            state.PositionHistory.Dequeue();
    }

    /// <summary>
    /// 更新摇一摇检测
    /// </summary>
    private void UpdateShakeDetection()
    {
        foreach (var kvp in _shakeStates)
        {
            var state = kvp.Value;
            var positions = state.PositionHistory.ToArray();
            
            if (positions.Length < 5)
            {
                state.IsShaking = false;
                continue;
            }
            
            // 计算方向改变次数
            int directionChanges = 0;
            
            for (int i = 2; i < positions.Length; i++)
            {
                var prev = (positions[i-1].X - positions[i-2].X, positions[i-1].Y - positions[i-2].Y);
                var curr = (positions[i].X - positions[i-1].X, positions[i].Y - positions[i-1].Y);
                
                // 点积为负表示方向改变
                float dot = prev.Item1 * curr.Item1 + prev.Item2 * curr.Item2;
                if (dot < 0)
                    directionChanges++;
            }
            
            // 计算频率
            var timeSpan = positions.Last().Time - positions.First().Time;
            float frequency = timeSpan.TotalSeconds > 0 ? directionChanges / (float)timeSpan.TotalSeconds : 0;
            
            // 阈值: 3次/秒
            bool wasShaking = state.IsShaking;
            state.IsShaking = frequency > 3.0f;
            
            if (state.IsShaking && !wasShaking)
            {
                state.LastShakeTime = DateTime.Now;
                OnShakeDetected?.Invoke(this, kvp.Key);
            }
        }
    }

    /// <summary>
    /// 检查气泡是否正在摇晃
    /// </summary>
    public bool IsShaking(long bubbleId)
    {
        return _shakeStates.TryGetValue(bubbleId, out var state) && state.IsShaking;
    }

    /// <summary>
    /// 摇一摇检测事件
    /// </summary>
    public event EventHandler<long>? OnShakeDetected;

    #endregion

    #region 聚类交互

    /// <summary>
    /// 吸引气泡向目标位置移动 (用于聚类效果)
    /// </summary>
    public void AttractTo(BubbleItem bubble, float targetX, float targetY, float strength = 0.1f)
    {
        float dx = targetX - bubble.X;
        float dy = targetY - bubble.Y;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
        
        if (distance > 0.001f)
        {
            // 吸引力与距离成正比 (弹簧效果)
            float force = strength * distance;
            
            bubble.VelocityX += (dx / distance) * force;
            bubble.VelocityY += (dy / distance) * force;
        }
    }

    /// <summary>
    /// 施加排斥力 (用于破裂散开效果)
    /// </summary>
    public void RepelFrom(BubbleItem bubble, float sourceX, float sourceY, float strength = 5f)
    {
        float dx = bubble.X - sourceX;
        float dy = bubble.Y - sourceY;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
        
        if (distance > 0.001f && distance < 300f) // 影响范围300px
        {
            // 排斥力与距离成反比
            float force = strength * (1f - distance / 300f);
            
            bubble.VelocityX += (dx / distance) * force;
            bubble.VelocityY += (dy / distance) * force;
        }
    }

    /// <summary>
    /// 爆炸效果 (破裂聚类时使用)
    /// </summary>
    public void ExplodeFrom(BubbleItem bubble, float centerX, float centerY, float power = 10f)
    {
        float dx = bubble.X - centerX;
        float dy = bubble.Y - centerY;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
        
        if (distance > 0.001f)
        {
            // 爆炸力
            float force = power / (distance + 10f); // +10防止除零
            
            bubble.VelocityX += (dx / distance) * force;
            bubble.VelocityY += (dy / distance) * force;
        }
        else
        {
            // 如果在中心，随机方向
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            bubble.VelocityX += (float)Math.Cos(angle) * power;
            bubble.VelocityY += (float)Math.Sin(angle) * power;
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 立即停止所有气泡
    /// </summary>
    public void StopAll()
    {
        foreach (var bubble in _bubbles)
        {
            bubble.VelocityX = 0;
            bubble.VelocityY = 0;
            bubble.IsStopped = true;
        }
    }

    /// <summary>
    /// 恢复漂浮
    /// </summary>
    public void ResumeAll()
    {
        foreach (var bubble in _bubbles)
        {
            bubble.IsStopped = false;
        }
    }

    /// <summary>
    /// 重置所有速度 (随机初始)
    /// </summary>
    public void ResetVelocities()
    {
        foreach (var bubble in _bubbles)
        {
            bubble.VelocityX = (float)(_random.NextDouble() - 0.5) * MaxSpeed * 2;
            bubble.VelocityY = (float)(_random.NextDouble() - 0.5) * MaxSpeed * 2;
        }
    }

    #endregion
}

/// <summary>
/// 物理引擎配置
/// </summary>
public class PhysicsConfig
{
    /// <summary>最大速度 (默认 0.5)</summary>
    public float MaxSpeed { get; set; } = 0.5f;
    
    /// <summary>摩擦力 (默认 0.99)</summary>
    public float Friction { get; set; } = 0.99f;
    
    /// <summary>随机力 (默认 0.02)</summary>
    public float RandomForce { get; set; } = 0.02f;
    
    /// <summary>避让力 (默认 0.5)</summary>
    public float RepulsionStrength { get; set; } = 0.5f;
    
    /// <summary>边界弹性 (默认 0.8)</summary>
    public float BoundaryElasticity { get; set; } = 0.8f;
}
