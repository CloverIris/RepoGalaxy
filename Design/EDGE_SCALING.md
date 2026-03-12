# RepoGalaxy 边缘缩放设计文档

> 状态：已批准
> 最后更新：2026-03-13

---

## 1. 设计目标

解决气泡云在视口边缘被硬裁剪的问题，通过**非线性边缘缩放**创造流畅的视觉效果。

**核心效果**：
- 气泡靠近边缘时逐渐缩小
- 越靠近边缘，缩小越明显
- 到达边界时完全消失 (而非突然截断)
- 营造"无限延伸"的宇宙感

---

## 2. 边缘区域定义

### 2.1 缓冲区 (Edge Buffer Zone)

```
视口结构:

┌─────────────────────────────────────────────────────────────┐
│ ↑                                                           │
│ │  边缘缓冲区 (Edge Buffer)                                  │
│ │  高度: 60-100px (根据气泡最大尺寸动态)                     │
│ │  功能: 非线性缩放过渡区                                    │
│ ↓                                                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│              完全可见区 (Full Visibility Zone)              │
│                                                             │
│     ●━━━━●━━━●●                                             │
│    ━●━━━●━━━●━━━●━━●                                        │
│     ━━━●━━━●━━━●●                                           │
│    ━━●━━━●━━━━●━━━●●●                                       │
│      ━━━━●━━━●━━━━●━━●                                      │
│     ━━━●━━━━●━━━●━━━●                                       │
│                                                             │
│     气泡在此区域内保持 100% 大小和完整交互                   │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│ ↑                                                           │
│ │  边缘缓冲区 (Edge Buffer)                                  │
│ │  非线性缩放: 100% → 0%                                     │
│ ↓                                                           │
└─────────────────────────────────────────────────────────────┘

缓冲区尺寸计算:
- 最小: 60px (适应最小气泡 16px)
- 标准: 80px (适应中等气泡 40px)
- 最大: 100px (适应最大气泡 72px)
- 动态: 根据当前最大气泡尺寸调整
```

### 2.2 四边缓冲区

```
四边都有独立的缓冲区:

        顶部缓冲区
     ←───────────────→
    ┌───────────────────┐ ↑
    │                   │ │ 左
    │   完全可见区      │ │ 侧
    │                   │ │ 缓
    │                   │ │ 冲
    │                   │ ↓
    └───────────────────┘
     ←─────────────────→
        底部缓冲区
```

---

## 3. 非线性缩放算法

### 3.1 数学模型

```
使用指数衰减函数实现平滑过渡:

scale = f(distance, buffer_size)

其中:
- distance: 气泡中心到视口边缘的距离
- buffer_size: 缓冲区大小 (如 80px)

函数选择:

1. 指数衰减 (推荐)
   scale = 1 - e^(-k * (buffer_size - distance) / buffer_size)
   
   参数 k = 3 时的效果:
   - distance = 0 (边缘): scale ≈ 0.05 (几乎消失)
   - distance = 40 (一半): scale ≈ 0.78
   - distance = 80 (缓冲区边界): scale ≈ 0.95
   - distance > 80 (内部): scale = 1.0

2. 平滑阶梯 (备选)
   scale = 0.5 * (1 + tanh((distance - buffer_size/2) / (buffer_size/4)))
   
   效果更柔和，但控制性较差

3. 幂函数 (备选)
   scale = (distance / buffer_size)^power
   power = 2 时效果适中
```

### 3.2 缩放曲线可视化

```
缩放比例 vs 到边缘距离 (缓冲区 80px):

scale
 1.0 ┤                              ┌──────────
     │                         ┌────┘
 0.9 ┤                    ┌────┘
     │               ┌────┘
 0.7 ┤          ┌────┘
     │     ┌────┘
 0.4 ┤┌────┘
     │┘
 0.1 ┤
     └────┬────┬────┬────┬────┬────┬────┬────
          0   20   40   60   80  100  120  140
                    到边缘距离 (px)

关键点:
- 0px (边缘): scale ≈ 0.05 (消失)
- 40px (中间): scale ≈ 0.5 (半大)
- 80px (缓冲区结束): scale ≈ 0.95 (几乎正常)
- 100px+: scale = 1.0 (完全正常)

曲线特征:
- 边缘附近急剧缩小 (防止被裁剪)
- 中间区域平滑过渡
- 内部区域保持完整大小
```

### 3.3 伪代码实现

```csharp
public float CalculateEdgeScale(Vector2 bubblePosition, Vector2 viewportSize, float bufferSize)
{
    // 计算到四边的最小距离
    float distToLeft = bubblePosition.X;
    float distToRight = viewportSize.X - bubblePosition.X;
    float distToTop = bubblePosition.Y;
    float distToBottom = viewportSize.Y - bubblePosition.Y;
    
    // 取最小距离
    float minDistance = Math.Min(Math.Min(distToLeft, distToRight), 
                                 Math.Min(distToTop, distToBottom));
    
    // 如果在完全可见区，返回 1.0
    if (minDistance >= bufferSize)
        return 1.0f;
    
    // 如果完全在边缘外，返回接近 0
    if (minDistance <= 0)
        return 0.05f; // 最小保留 5%，避免完全消失导致闪烁
    
    // 非线性缩放 (指数衰减)
    float normalizedDist = minDistance / bufferSize; // 0.0 ~ 1.0
    float k = 3.0f; // 衰减系数，越大边缘收缩越快
    
    // 使用平滑指数函数
    float scale = 1.0f - (float)Math.Exp(-k * normalizedDist);
    
    // 确保最小值
    return Math.Max(scale, 0.05f);
}

// 变体：使用幂函数 (更平滑)
public float CalculateEdgeScalePower(Vector2 bubblePosition, Vector2 viewportSize, float bufferSize)
{
    float minDistance = CalculateMinDistanceToEdge(bubblePosition, viewportSize);
    
    if (minDistance >= bufferSize)
        return 1.0f;
    
    if (minDistance <= 0)
        return 0.05f;
    
    float normalizedDist = minDistance / bufferSize;
    float power = 2.0f; // 幂次，越大边缘收缩越快
    
    return (float)Math.Pow(normalizedDist, power);
}
```

---

## 4. 视觉与交互效果

### 4.1 边缘气泡状态

```
不同阶段的气泡表现:

阶段 1: 完全可见区 (距离 > 80px)
┌─────────────────────────────────────┐
│                                     │
│     ╭────────────╮                  │
│    │  ●───●      │  scale = 1.0    │
│    │  │repo│     │  完整大小        │
│    │  ●───●      │  完整交互        │
│     ╰────────────╯                  │
│                                     │
└─────────────────────────────────────┘

阶段 2: 缓冲区中部 (距离 40px)
┌─────────────────────────────────────┐
│                         ╭──────╮   │
│                        │ ●───● │   │
│                        │ │repo│ │   │ scale ≈ 0.5
│                        │ ●───● │   │ 半透明 + 缩小
│                         ╰──────╯   │ 仍可交互
└─────────────────────────────────────┘

阶段 3: 缓冲区边缘 (距离 10px)
┌─────────────────────────────────────┐
│                                  ╭─╮│
│                                 │●─●││ scale ≈ 0.15
│                                 │r│ │ 很小 + 半透明
│                                 │●─●││ 仍可点击
│                                  ╰─╯│
└─────────────────────────────────────┘

阶段 4: 视口边缘 (距离 0px)
┌─────────────────────────────────────┐
│                                     │
│                                     │
│                                     │ scale ≈ 0.05
│                                     │ 几乎消失
│                                     │ 但逻辑仍存在
└─────────────────────────────────────┘
```

### 4.2 透明度配合 (可选)

```
可以与透明度结合，增强消失感:

opacity = scale * 0.8 + 0.2

这样:
- scale = 1.0 → opacity = 1.0
- scale = 0.5 → opacity = 0.6
- scale = 0.1 → opacity = 0.28
- scale = 0.05 → opacity = 0.24 (不完全透明)

保持最小透明度 0.2，确保用户知道那里有内容
```

### 4.3 交互处理

```
边缘气泡的交互:

点击检测:
- 使用视觉缩放后的大小进行命中测试
- 或者保持原始大小，但视觉缩小

推荐方案:
- 命中测试使用视觉大小 (缩小后的)
- 防止误触
- 但确保仍可点击

Hover 效果:
- 边缘气泡 Hover 时:
  1. 轻微放大 (1.1x 基础上再 × 当前 scale)
  2. 透明度增加
  3. 可显示 Tooltip

拖拽:
- 从边缘往中心拖拽时，气泡逐渐变大
- 创造"从边缘拉回"的视觉效果
```

---

## 5. 性能优化

### 5.1 计算优化

```
避免每帧对每个气泡计算:

优化策略:
1. 空间分区 (Spatial Hashing)
   - 只计算靠近边缘的气泡
   - 内部气泡直接 scale = 1.0

2. 缓动更新
   - scale 变化时应用平滑过渡
   - 避免突兀的大小跳跃

3. 阈值裁剪
   - scale < 0.05 的气泡停止渲染
   - 减少 GPU 负担

4. 批量计算
   - 使用 GPU Shader 计算边缘缩放
   - 顶点着色器中处理
```

### 5.2 渲染优化

```csharp
// 渲染时跳过太小的气泡
if (edgeScale < 0.05f)
    return; // 不渲染

// 或者使用透明度测试
if (edgeScale < 0.1f)
{
    // 降低更新频率
    if (frameCount % 3 != 0) return;
}
```

---

## 6. 与其他效果的配合

### 6.1 与漂浮动画的配合

```
边缘气泡的漂浮:

正常漂浮 + 边缘缩放 = 动态进出效果

气泡从中心漂向边缘:
- 逐渐缩小
- 透明度降低
- 给用户"飘向远方"的感觉

气泡从边缘漂向中心:
- 逐渐放大
- 透明度增加
- 给用户"从远处飘来"的感觉

这增强了"无限宇宙"的感觉
```

### 6.2 与拖拽的配合

```
拖拽气泡到边缘:

拖拽时:
- 气泡跟随鼠标
- 实时计算边缘缩放
- 给用户视觉反馈

释放时:
- 如果在缓冲区外，弹回
- 如果在缓冲区内，继续漂浮
- 弹性动画
```

### 6.3 与缩放的配合

```
全局缩放视图时:

用户放大视图:
- 更多气泡进入缓冲区
- 边缘有更多缩小气泡
- 营造"视野变窄"的感觉

用户缩小视图:
- 气泡相对变大
- 缓冲区容纳更多完整气泡
- 营造"视野变宽"的感觉
```

---

## 7. 参数配置

### 7.1 可调参数

```csharp
public class EdgeScalingConfig
{
    // 缓冲区大小
    public float BufferSize { get; set; } = 80.0f;
    
    // 衰减系数 (指数函数)
    public float DecayCoefficient { get; set; } = 3.0f;
    
    // 幂次 (幂函数模式)
    public float Power { get; set; } = 2.0f;
    
    // 最小缩放
    public float MinScale { get; set; } = 0.05f;
    
    // 最小透明度
    public float MinOpacity { get; set; } = 0.2f;
    
    // 是否启用
    public bool Enabled { get; set; } = true;
    
    // 函数类型
    public EdgeScalingFunction Function { get; set; } = EdgeScalingFunction.Exponential;
}

public enum EdgeScalingFunction
{
    Exponential,  // 指数衰减 (推荐)
    Power,        // 幂函数
    SmoothStep,   // 平滑阶梯
    Linear        // 线性 (不推荐)
}
```

### 7.2 预设配置

```
预设配置:

1. 柔和模式 (Soft)
   BufferSize: 100px
   DecayCoefficient: 2.0
   效果: 非常平滑，边缘渐变更长

2. 标准模式 (Standard) - 默认
   BufferSize: 80px
   DecayCoefficient: 3.0
   效果: 平衡视觉和交互

3. 紧凑模式 (Compact)
   BufferSize: 60px
   DecayCoefficient: 4.0
   效果: 快速消失，适合小屏幕

4. 戏剧模式 (Dramatic)
   BufferSize: 120px
   DecayCoefficient: 5.0
   MinScale: 0.0
   效果: 强烈的景深效果
```

---

## 8. 实现注意事项

### 8.1 边界情况

```
需要处理的情况:

1. 超大屏幕
   - 缓冲区相对比例可能过小
   - 解决方案: 使用百分比而非固定像素
   - BufferSize = Min(80, viewportHeight * 0.1)

2. 超小窗口
   - 缓冲区可能占据大部分视口
   - 解决方案: 限制缓冲区最大比例
   - BufferSize = Min(80, viewportHeight * 0.25)

3. 气泡尺寸差异
   - 大气泡和小气泡使用相同缓冲区
   - 可能显得不公平
   - 可选: 基于气泡半径动态调整缓冲区

4. 快速移动
   - 气泡快速穿过缓冲区
   - 可能导致闪烁
   - 解决方案: 应用平滑过渡 (Lerp)
```

### 8.2 无障碍考虑

```
无障碍支持:

1. 减少动画模式
   - 关闭边缘缩放动画
   - 直接硬裁剪或使用透明度

2. 高对比度模式
   - 边缘气泡保持更高透明度
   - 确保可见性

3. 屏幕阅读器
   - 即使视觉缩小，仍报告完整信息
   - 气泡数量不变
```

---

## 9. 总结

### 9.1 关键设计点

| 要点 | 说明 |
|------|------|
| 缓冲区 | 视口边缘 60-100px 区域 |
| 缩放函数 | 指数衰减 (推荐) 或幂函数 |
| 最小缩放 | 保留 5%，不完全消失 |
| 配合透明 | 可选，增强消失感 |
| 性能 | 空间分区 + 阈值裁剪 |

### 9.2 预期效果

```
最终实现效果:

用户看到的:
┌───────────────────────────────────────────────┐
│ · ·      ·                                   │
│     ·                              ·         │
│          ·      ●━━━●━━━●●                    │
│     ·          ━●━━━●━━━●━━━●━━●              │
│   ·      ·      ━━━●━━━●━━━●●                 │
│                ━━●━━━●━━━━●━━━●●●             │
│   ·    ·          ━━━━●━━━●━━━━●━━●           │
│       ·          ━━━●━━━━●━━━●━━━●            │
│              ·                                │
│    ·                               ·          │
└───────────────────────────────────────────────┘

- 边缘有小气泡逐渐消失
- 中心气泡完整大小
- 过渡自然流畅
- 营造无限宇宙感
```

---

*此文档应与 VISUALIZATION_SYSTEM.md 和 INTERACTION_DESIGN.md 一起阅读。*
