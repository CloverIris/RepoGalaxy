# RepoGalaxy 交互设计文档

> 版本: v1.0  
> 状态: 设计中  
> 最后更新: 2026-03-14  
> 基于: 用户设计图 (Screenshot 2026-03-13)

---

## 1. 交互设计理念

### 1.1 核心原则

| 原则 | 描述 | 设计体现 |
|------|------|---------|
| **游戏化探索** | 让发现过程像游戏一样有趣 | 拖拽聚类、摇晃触发、长按破裂 |
| **物理反馈** | 真实的物理交互感受 | 慵懒漂浮、弹性碰撞、惯性运动 |
| **渐进式复杂度** | 默认简单，深度可探索 | 单层点击→详情卡→全屏页 |
| **数据可视化** | 视觉即信息 | 7维视觉编码同时呈现 |

### 1.2 隐喻系统

**开源宇宙 (Open Source Universe)**

| 概念 | 现实对应 | 交互方式 |
|------|---------|---------|
| 星球/陨石 | GitHub 仓库 | 圆形气泡，可拖拽 |
| 引力 | 项目相似性 | 摇晃后同类吸引 |
| 星团 | 技术领域聚类 | 大气泡包含小气泡 |
| 星光 | 活跃度 | 闪烁频率 |
| 探索 | 发现新项目 | 漂浮浏览、筛选 |

---

## 2. 气泡云交互系统

### 2.1 慵懒漂浮 (Android 4.3 彩蛋风格)

**设计灵感**: Android 4.3 Jelly Bean 彩蛋中的糖豆漂浮效果

#### 2.1.1 行为特征

```
默认状态:
• 每个气泡独立运动
• 速度范围: -0.5 ~ 0.5 px/frame (随机初始值)
• 方向: 随机向量，缓慢变化
• 边界: 柔和反弹 (弹性系数 0.8)
• 避让: 轻微排斥力防止重叠

视觉感受:
• 缓慢、慵懒、有机
• 像在水中漂浮
• 不规律的漂移路径
```

#### 2.1.2 物理参数

| 参数 | 值 | 说明 |
|------|-----|------|
| 最大速度 | 0.5 px/frame | 防止过快 |
| 摩擦力 | 0.99 | 几乎无阻力 |
| 随机力 | 0.02 | 每帧随机加速度 |
| 避让力 | 0.5 | 接近时的排斥 |
| 边界反弹 | 0.8 | 柔和弹性 |

#### 2.1.3 实现要点

```csharp
// 每帧更新
foreach (var bubble in bubbles)
{
    // 随机力
    bubble.VelocityX += (Random.NextDouble() - 0.5) * RandomForce;
    bubble.VelocityY += (Random.NextDouble() - 0.5) * RandomForce;
    
    // 摩擦力
    bubble.VelocityX *= Friction;
    bubble.VelocityY *= Friction;
    
    // 避让其他气泡
    foreach (var other in bubbles.Where(b => b != bubble))
    {
        var dist = CalculateDistance(bubble, other);
        if (dist < bubble.Radius + other.Radius + Padding)
        {
            ApplyRepulsionForce(bubble, other);
        }
    }
    
    // 更新位置
    bubble.X += bubble.VelocityX;
    bubble.Y += bubble.VelocityY;
    
    // 边界处理
    ApplyBoundaryBounce(bubble);
}
```

---

### 2.2 鼠标悬停

#### 2.2.1 悬停效果

```
用户动作 → 系统响应
─────────────────────────────────────────
鼠标进入气泡
    ↓
气泡停止漂浮运动
    ↓
放大动画 (1.0x → 1.1x, 200ms, EaseOut)
    ↓
显示 Tooltip 卡片
    ↓
其他气泡变暗 (透明度 60%)

鼠标离开
    ↓
缩小动画 (1.1x → 1.0x, 200ms, EaseOut)
    ↓
恢复漂浮运动
    ↓
隐藏 Tooltip
    ↓
其他气泡恢复亮度
```

#### 2.2.2 Tooltip 设计

```
┌─────────────────────────────┐
│  [语言色块] repo-name       │
│  👤 owner                   │
│  ⭐ 1.2k  🍴 234  👁️ 56     │
│  🏷️ rust • cli • tool       │
│  🕐 更新于 2小时前           │
│                             │
│  [⭐ Star] [🔖 收藏]        │
└─────────────────────────────┘
```

---

### 2.3 拖拽与摇晃聚类

#### 2.3.1 拖拽流程

```
Phase 1: 拖拽开始
─────────────────
左键按下 → 记录起始位置
    ↓
气泡跟随鼠标移动
    ↓
放大 (1.0x → 1.1x)
    ↓
其他气泡变暗

Phase 2: 摇晃检测
─────────────────
鼠标快速左右移动 (>3次/秒)
    ↓
气泡发光 + 颜色偏移 (Accent色)
    ↓
显示"摇晃中..."提示
    ↓
触发相似项目检索

Phase 3: 松开鼠标
─────────────────
左键释放
    ↓
同类小气泡被吸引 (飞入动画)
    ↓
融合成大气泡 (1.5-2.5x)
    ↓
显示 "+N" 计数
    ↓
大气泡进入"聚类状态"
```

#### 2.3.2 摇晃检测算法

```csharp
public class ShakeDetector
{
    private Queue<(Vector2 Position, DateTime Time)> _history = new();
    private const int HistorySize = 10;
    private const double ShakeThreshold = 3; // 每秒方向改变次数
    
    public void Update(Vector2 mousePosition)
    {
        _history.Enqueue((mousePosition, DateTime.Now));
        if (_history.Count > HistorySize)
            _history.Dequeue();
    }
    
    public bool IsShaking()
    {
        if (_history.Count < 5) return false;
        
        var positions = _history.ToArray();
        int directionChanges = 0;
        
        for (int i = 2; i < positions.Length; i++)
        {
            var prevDirection = positions[i-1].Position - positions[i-2].Position;
            var currentDirection = positions[i].Position - positions[i-1].Position;
            
            // 检查方向是否改变 (点积为负)
            if (Vector2.Dot(prevDirection, currentDirection) < 0)
                directionChanges++;
        }
        
        var timeSpan = positions.Last().Time - positions.First().Time;
        var frequency = directionChanges / timeSpan.TotalSeconds;
        
        return frequency > ShakeThreshold;
    }
}
```

#### 2.3.3 相似项目检索

```csharp
// 基于主题的相似度
async Task<List<Repository>> FindSimilarReposAsync(Repository source)
{
    var candidates = new List<Repository>();
    
    // 1. 相同语言
    var sameLang = await _repoService.SearchAsync($"language:{source.PrimaryLanguage}");
    candidates.AddRange(sameLang);
    
    // 2. 共同主题
    foreach (var topic in source.Topics.Take(3))
    {
        var topicRepos = await _repoService.SearchAsync($"topic:{topic}");
        candidates.AddRange(topicRepos);
    }
    
    // 3. 计算相似度并排序
    return candidates
        .Distinct()
        .Select(r => new { Repo = r, Score = CalculateSimilarity(source, r) })
        .OrderByDescending(x => x.Score)
        .Take(15) // 最多15个
        .Select(x => x.Repo)
        .ToList();
}
```

#### 2.3.4 聚类动画

```
小气泡飞入大气泡:
• 时长: 500ms
• 缓动: EaseIn
• 路径: 贝塞尔曲线向中心
• 效果: 逐渐缩小 + 透明度降低

大气泡融合:
• 时长: 400ms
• 缓动: EaseOutBack
• 效果: 弹性放大到 1.5-2.5x
• 内部: 显示子项目缩略图
```

---

### 2.4 大气泡多选预览

#### 2.4.1 大气泡状态

```
聚类后的大气泡:
┌──────────────────────────────────┐
│  ┌──┐ ┌──┐ ┌──┐                 │
│  │● │ │● │ │● │  +12            │
│  └──┘ └──┘ └──┘                 │
│                                  │
│  内部显示最多 4 个缩略图         │
│  右下角显示 "+N" 计数            │
└──────────────────────────────────┘
```

#### 2.4.2 点击大气泡

```
点击 → 展开多选预览面板
    ↓
右侧滑入网格面板
    ↓
显示所有子项目 (网格布局)
    ↓
每个项目可勾选/取消
    ↓
底部操作栏:
    [全选] [反选] [批量Star] [批量收藏]
```

---

### 2.5 长按破裂

#### 2.5.1 长按流程

```
左键按住大气泡
    ↓
开始 3 秒倒计时
    ↓
边框颜色变化 (黄→橙→红)
    ↓
内部抖动动画
    ↓
显示倒计时数字
    ↓
达到 3 秒
    ↓
破裂动画触发
```

#### 2.5.2 倒计时视觉

```
时间进度:
0.0s ──────── 1.0s ──────── 2.0s ──────── 3.0s
  │            │            │            │
  ▼            ▼            ▼            ▼
边框: 黄色    橙色        深橙色      红色
抖动: 无      轻微        中等        剧烈
数字: 3       2           1           破裂!
```

#### 2.5.3 破裂动画

```
破裂效果:
• 时长: 800ms
• 粒子效果: 10-20 个小碎片向外飞溅
• 小气泡四散弹射 (随机方向)
• 每个小气泡恢复独立漂浮
• 大气泡逐渐缩小消失

物理参数:
• 碎片初速度: 2-5 px/frame
• 碎片减速度: 0.95
• 小气泡弹射力: 随机 ±3 px/frame
```

#### 2.5.4 实现要点

```csharp
public class LongPressDetector
{
    private DateTime _pressStartTime;
    private bool _isPressed;
    private const int LongPressDurationMs = 3000;
    
    public event EventHandler<float>? ProgressChanged; // 0.0 ~ 1.0
    public event EventHandler? LongPressTriggered;
    
    public void OnPointerPressed()
    {
        _isPressed = true;
        _pressStartTime = DateTime.Now;
        _ = MonitorPressAsync();
    }
    
    public void OnPointerReleased()
    {
        _isPressed = false;
    }
    
    private async Task MonitorPressAsync()
    {
        while (_isPressed)
        {
            var elapsed = (DateTime.Now - _pressStartTime).TotalMilliseconds;
            var progress = Math.Min(1.0, elapsed / LongPressDurationMs);
            
            ProgressChanged?.Invoke(this, (float)progress);
            
            if (progress >= 1.0)
            {
                LongPressTriggered?.Invoke(this, EventArgs.Empty);
                break;
            }
            
            await Task.Delay(50);
        }
    }
}
```

---

## 3. 页面导航交互

### 3.1 Sidebar 导航

```
点击导航项:
• 选中态: 背景色变化 + 图标高亮
• 过渡动画: 300ms 淡入淡出
• 内容区: 对应页面切换

当前实现:
┌────────┐
│  🌌    │  ← 选中 (Acrylic 背景)
│  主页  │
├────────┤
│  ⭐    │  ← 未选中 (透明)
│  收藏  │
└────────┘
```

### 3.2 页面切换

```
切换动画:
• 旧页面: 淡出 (200ms)
• 新页面: 淡入 (200ms)
• 背景: 保持不变 (星空)

数据加载:
• 显示骨架屏
• 缓存数据先显示
• 后台刷新新数据
```

---

## 4. 筛选与搜索交互

### 4.1 Pill 标签筛选

#### 4.1.1 标签定义

```
┌─────────────────────────────────────────────┐
│  🔥 热门  ⭐ 推荐  📈 Trending  🎲 随机  🆕 最新  │
└─────────────────────────────────────────────┘

数据源映射:
• 热门 → Trending (GitHub API)
• 推荐 → Personalized (推荐引擎)
• Trending → GitHub Trending 实时
• 随机 → RandomDiscovery (高质量抽样)
• 最新 → Newest (created:>1week)
```

#### 4.1.2 切换动画

```
点击 Pill:
• 选中态切换: 背景色变化 (100ms)
• 数据刷新: 加载动画
• 气泡云: FLIP 动画重新排列
```

### 4.2 二元筛选定制

```
可定制的两个维度:

维度 A (时间):
• 全部时间
• 今天
• 本周
• 本月
• 今年

维度 B (排序):
• 智能推荐
• Star 最多
• 最新提交
• 最活跃
• 正在趋势

组合示例:
┌──────────┬──────────┐
│  本周    │  最活跃  │ → 本周最活跃的项目
└──────────┴──────────┘
```

---

## 5. 详情页交互

### 5.1 点击气泡流程

```
左键点击气泡
    ↓
按下反馈 (0.95x, 50ms)
    ↓
展开详情卡片 (右侧滑入)
    ↓
背景模糊 + 其他气泡变暗
    ↓
详情卡显示:
    • 仓库信息
    • README 预览
    • Star/收藏按钮
    • 打开 GitHub 链接

再次点击详情卡
    ↓
全屏展开仓库视图
    ↓
导航栏收缩
    ↓
加载完整信息

ESC / 点击返回
    ↓
退回气泡云
```

---

## 6. 手势总结

### 6.1 鼠标手势

| 操作 | 效果 | 场景 |
|------|------|------|
| 单击 | 打开详情卡 | 浏览 |
| 双击 | 重置视图 | 导航 |
| 拖拽 | 平移视图/拖拽气泡 | 探索 |
| 滚轮 | 垂直滚动 | 时间轴 |
| Shift+滚轮 | 水平滚动 | Star轴 |
| Ctrl+滚轮 | 缩放视图 | 概览/细节 |
| 长按 | 破裂聚类 | 聚类管理 |

### 6.2 触控板手势

| 操作 | 效果 |
|------|------|
| 双指滑动 | 平移视图 |
| 双指捏合 | 缩放视图 |
| 单指点击 | 选择/打开 |

---

## 7. 动画时序规范

### 7.1 动画时长表

| 动画 | 时长 | 缓动 |
|------|------|------|
| Hover 放大 | 200ms | EaseOut |
| 详情展开 | 400ms | EaseOutBack |
| 全屏展开 | 500ms | EaseInOutCubic |
| 页面切换 | 300ms | EaseInOut |
| 聚类飞入 | 500ms | EaseIn |
| 破裂散开 | 800ms | Custom |
| Pill 切换 | 100ms | EaseOut |
| 气泡漂浮 | 20-40s/cycle | Linear |

### 7.2 性能目标

```
目标:
• 交互响应: < 16ms (60fps)
• 动画帧率: 60fps
• 首次点击反馈: < 100ms
• 详情展开: < 500ms
```

---

## 8. 待确认问题

| 问题 | 选项 | 建议 |
|------|------|------|
| 摇晃灵敏度 | 高/中/低 | 默认中，可设置 |
| 长按时长 | 2s/3s/5s | 默认 3s |
| 最大聚类数 | 10/15/20 | 默认 15 |
| 气泡漂浮速度 | 快/中/慢 | 默认中，可设置 |
| 低性能模式 | 开关 | 关闭复杂动画 |

---

**本文档详细定义了 RepoGalaxy 的创新交互系统，是实现游戏化探索体验的核心规范。**

---

## 9. 实现状态 (Updated 2026-03-13)

### ✅ 已实现的交互功能

#### A. 拖拽摇晃聚类 (Drag-and-Shake Clustering)
| 组件 | 文件路径 | 状态 |
|------|----------|------|
| 摇晃检测 | `BubblePhysicsEngine.cs` | ✅ 每秒>3次方向改变触发 |
| 视觉反馈 | `ShakeVisualState` + `BubbleCloudRenderOperation` | ✅ 金黄/橙色三层发光效果 |
| 聚类管理 | `ClusterManager.cs` | ✅ 最多15个相似项目 |
| 成员飞入 | `ClusterAnimationState` | ✅ EaseOutBack 动画 500ms |
| DI 注入 | `ExploreView.axaml.cs` | ✅ Scoped 生命周期管理 |

#### B. 长按解散聚类 (Long-Press to Break)
| 组件 | 文件路径 | 状态 |
|------|----------|------|
| 长按检测 | `BubbleCloudControl.cs` | ✅ 3秒按住触发 |
| 进度指示 | `BubbleCloudRenderOperation.cs` | ✅ 黄→橙→红进度环 |
| 粒子爆炸 | `Particle` 类 | ✅ 30-50个粒子，重力效果 |
| 成员四散 | `ClusterManager.BreakCluster()` | ✅ 随机速度弹射 |

#### C. 多选面板 (Multi-Select Panel)
| 组件 | 文件路径 | 状态 |
|------|----------|------|
| 面板控件 | `ClusterSelectionPanel.axaml` | ✅ 400px宽度，暗黑主题 |
| 成员卡片 | `ClusterMemberItem.axaml` | ✅ 170x80px 紧凑卡片 |
| 批量操作 | 收藏/比较/导出 | ✅ Toast 通知反馈 |
| 选择状态 | 代码隐藏实现 | ✅ Checkbox + 卡片点击 |

### 📁 新增/修改的文件

```
src/RepoGalaxy.Desktop/
├── Controls/
│   ├── BubbleCloudControl.cs          [修改] 集成聚类逻辑
│   ├── BubbleCloudRenderOperation.cs  [修改] 添加摇晃/长按/粒子渲染
│   ├── BubblePhysicsEngine.cs         [现有] 摇晃检测
│   ├── ClusterManager.cs              [现有] 聚类生命周期
│   ├── ClusterSelectionPanel.axaml    [新增] 多选面板
│   ├── ClusterSelectionPanel.axaml.cs [新增] 面板逻辑
│   ├── ClusterMemberItem.axaml        [新增] 成员卡片
│   ├── ClusterMemberItem.axaml.cs     [新增] 卡片逻辑
│   └── Particle (class)               [新增] 粒子效果
├── Services/
│   ├── INotificationService.cs        [新增] 通知接口
│   └── ToastNotificationService.cs    [新增] Toast实现
├── ViewModels/
│   └── ClusterSelectionViewModel.cs   [新增] 面板VM (备用)
└── Views/Pages/
    └── ExploreView.axaml.cs           [修改] ClusterManager DI
```

### 🎯 使用方式

**拖拽摇晃形成聚类:**
1. 按住气泡拖拽
2. 快速来回摇晃 (>3次/秒)
3. 黄色发光提示
4. 释放后显示聚类

**长按解散聚类:**
1. 聚类形成后，按住聚类中心
2. 观察圆环进度（黄→橙→红）
3. 保持3秒后自动破裂
4. 粒子爆炸效果，成员四散

**多选面板:**
1. 单击聚类展开成员
2. 点击成员卡片选择/取消
3. 全选按钮一键选择
4. 底部操作栏批量处理
