using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Desktop.Models;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// 摇晃视觉状态
/// </summary>
public class ShakeVisualState
{
    public long BubbleId { get; }
    public DateTime StartTime { get; }
    public float Intensity { get; set; }
    public float Duration { get; set; } = 2.0f; // 持续2秒
    
    public ShakeVisualState(long bubbleId, float intensity = 1.0f)
    {
        BubbleId = bubbleId;
        StartTime = DateTime.Now;
        Intensity = intensity;
    }
    
    /// <summary>
    /// 获取当前帧的发光强度 (0-1)
    /// </summary>
    public float GetGlowIntensity()
    {
        var elapsed = (float)(DateTime.Now - StartTime).TotalSeconds;
        if (elapsed >= Duration) return 0;
        
        // 前0.3秒快速上升到最大，然后缓慢衰减
        if (elapsed < 0.3f)
            return Intensity * (elapsed / 0.3f);
        
        // 衰减阶段
        var decayProgress = (elapsed - 0.3f) / (Duration - 0.3f);
        return Intensity * (1 - decayProgress) * 0.7f;
    }
    
    public bool IsExpired => (DateTime.Now - StartTime).TotalSeconds >= Duration;
}

/// <summary>
/// 粒子效果 (聚类破裂时使用)
/// </summary>
public class Particle
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Size { get; set; }
    public float Life { get; set; } = 1.0f; // 0-1
    public float Decay { get; set; } = 0.02f;
    public SKColor Color { get; set; }
    
    public bool Update()
    {
        X += VelocityX;
        Y += VelocityY;
        VelocityY += 0.2f; // 重力
        Life -= Decay;
        return Life > 0;
    }
}

/// <summary>
/// 气泡云可视化控件 (Apple Watch 风格蜂窝布局 + 拖拽摇晃聚类)
/// </summary>
public class BubbleCloudControl : Control
{
    #region 依赖属性

    public static readonly StyledProperty<IEnumerable<BubbleItem>> ItemsProperty =
        AvaloniaProperty.Register<BubbleCloudControl, IEnumerable<BubbleItem>>(nameof(Items));

    public IEnumerable<BubbleItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly StyledProperty<BubbleItem?> SelectedItemProperty =
        AvaloniaProperty.Register<BubbleCloudControl, BubbleItem?>(nameof(SelectedItem));

    public BubbleItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly StyledProperty<LayoutMode> LayoutModeProperty =
        AvaloniaProperty.Register<BubbleCloudControl, LayoutMode>(nameof(LayoutMode), LayoutMode.Honeycomb);

    public LayoutMode LayoutMode
    {
        get => GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    #endregion

    #region 字段

    private readonly List<BubbleItem> _bubbles = new();
    private readonly HoneycombLayoutEngine _layoutEngine = new();
    private readonly BubblePhysicsEngine _physicsEngine = new();
    private ClusterManager? _clusterManager;
    private readonly DispatcherTimer _animationTimer;
    private readonly Random _random = new();
    
    private BubbleItem? _hoveredBubble;
    private bool _isDragging;
    private BubbleItem? _draggedBubble;
    private SKPoint _lastMousePosition;
    private DateTime _dragStartTime;
    private bool _isShaking;
    
    private DateTime _lastFrameTime;
    private bool _isAnimating;
    private bool _layoutInitialized;

    // 聚类相关
    private ClusterGroup? _activeCluster;
    private bool _isClusterExpanded;
    
    // 摇晃视觉反馈
    private readonly Dictionary<long, ShakeVisualState> _shakeVisualStates = new();
    private float _shakeIntensity;
    
    // Toast
    private string? _toastMessage;
    private float _toastOpacity;
    private DateTime _toastShowTime;
    
    // 长按解散相关
    private ClusterGroup? _longPressCluster;
    private DateTime _longPressStartTime;
    private readonly float _longPressDuration = 3.0f; // 3秒长按
    private bool _isLongPressing;
    private float _longPressProgress; // 0-1
    
    // 粒子效果
    private readonly List<Particle> _particles = new();
    private readonly Random _particleRandom = new();
    
    // 多选面板
    private ClusterSelectionPanel? _selectionPanel;
    private INotificationService? _notificationService;

    #endregion

    #region 构造函数

    public BubbleCloudControl()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAnimationTick;
        
        ItemsProperty.Changed.AddClassHandler<BubbleCloudControl>((c, e) => c.OnItemsChanged(e));
        LayoutModeProperty.Changed.AddClassHandler<BubbleCloudControl>((c, e) => c.OnLayoutModeChanged(e));
        
        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        
        _physicsEngine.OnShakeDetected += OnBubbleShakeDetected;
    }

    #endregion

    #region 依赖注入

    /// <summary>
    /// 设置聚类管理器 (由 ViewModel 注入)
    /// </summary>
    public void SetClusterManager(ClusterManager manager)
    {
        _clusterManager = manager;
    }

    #endregion

    #region 数据变化

    private void OnItemsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _bubbles.Clear();
        _layoutInitialized = false;
        
        if (e.NewValue is IEnumerable<BubbleItem> items)
        {
            _bubbles.AddRange(items);
            _clusterManager?.SetBubbles(_bubbles);
            InitializeLayout();
        }
        
        InvalidateVisual();
    }

    private void OnLayoutModeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _layoutInitialized = false;
        InitializeLayout();
        InvalidateVisual();
    }

    private void InitializeLayout()
    {
        if (_bubbles.Count == 0 || _layoutInitialized) return;
        
        float width = (float)Bounds.Width;
        float height = (float)Bounds.Height;
        
        if (width < 100 || height < 100) return;

        switch (LayoutMode)
        {
            case LayoutMode.Honeycomb:
                InitializeHoneycombLayout(width, height);
                break;
            case LayoutMode.Spiral:
                InitializeSpiralLayout(width, height);
                break;
            case LayoutMode.Grid:
                InitializeGridLayout(width, height);
                break;
        }
        
        if (!_isAnimating)
        {
            _isAnimating = true;
            _lastFrameTime = DateTime.Now;
            _animationTimer.Start();
        }
        
        _layoutInitialized = true;
    }

    private void InitializeHoneycombLayout(float width, float height)
    {
        _layoutEngine.Initialize(width, height);
        _layoutEngine.Layout(_bubbles);
        
        _physicsEngine.Initialize(width, height);
        _physicsEngine.SetBubbles(_bubbles);
        
        foreach (var bubble in _bubbles)
        {
            bubble.VelocityX = 0;
            bubble.VelocityY = 0;
        }
    }

    private void InitializeSpiralLayout(float width, float height)
    {
        float centerX = width / 2;
        float centerY = height / 2;
        var sorted = _bubbles.OrderByDescending(b => b.Radius).ToList();
        
        float angle = 0, angleStep = 0.5f, radiusStep = 5f;
        
        foreach (var bubble in sorted)
        {
            bool placed = false;
            int attempts = 0;
            float currentRadius = bubble.Radius + 10;
            
            while (!placed && attempts < 500)
            {
                float x = centerX + (float)Math.Cos(angle) * currentRadius;
                float y = centerY + (float)Math.Sin(angle) * currentRadius;
                
                bool overlaps = sorted.Take(sorted.IndexOf(bubble)).Any(b =>
                {
                    float dx = x - b.X, dy = y - b.Y;
                    return Math.Sqrt(dx * dx + dy * dy) < bubble.Radius + b.Radius + 5;
                });
                
                if (!overlaps)
                {
                    bubble.X = x; bubble.Y = y;
                    placed = true;
                }
                else
                {
                    angle += angleStep;
                    currentRadius += radiusStep * 0.1f;
                    attempts++;
                }
            }
            
            if (!placed)
            {
                bubble.X = centerX + (float)(_random.NextDouble() - 0.5) * 200;
                bubble.Y = centerY + (float)(_random.NextDouble() - 0.5) * 200;
            }
            
            bubble.VelocityX = (float)(_random.NextDouble() - 0.5) * 0.5f;
            bubble.VelocityY = (float)(_random.NextDouble() - 0.5) * 0.5f;
            bubble.TwinklePhase = (float)_random.NextDouble() * (float)Math.PI * 2;
            bubble.BreathPhase = (float)_random.NextDouble() * (float)Math.PI * 2;
        }
        
        _physicsEngine.Initialize(width, height);
        _physicsEngine.SetBubbles(_bubbles);
    }

    private void InitializeGridLayout(float width, float height)
    {
        int cols = (int)Math.Sqrt(_bubbles.Count) + 1;
        int rows = (_bubbles.Count + cols - 1) / cols;
        
        float cellWidth = width / cols;
        float cellHeight = height / rows;
        
        for (int i = 0; i < _bubbles.Count; i++)
        {
            var bubble = _bubbles[i];
            bubble.X = (i % cols) * cellWidth + cellWidth / 2;
            bubble.Y = (i / cols) * cellHeight + cellHeight / 2;
            bubble.VelocityX = 0;
            bubble.VelocityY = 0;
        }
        
        _physicsEngine.Initialize(width, height);
        _physicsEngine.SetBubbles(_bubbles);
    }

    #endregion

    #region 摇晃聚类

    private async void OnBubbleShakeDetected(object? sender, long bubbleId)
    {
        if (_clusterManager == null) return;
        
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var bubble = _bubbles.FirstOrDefault(b => b.Id == bubbleId);
            if (bubble == null) return;

            // 视觉反馈：发光
            TriggerShakeVisual(bubble);
            
            // 创建聚类
            var cluster = await _clusterManager.CreateClusterAsync(bubble);
            if (cluster != null)
            {
                _activeCluster = cluster;
                _isClusterExpanded = false;
                
                // 显示提示
                ShowToast($"发现 {cluster.Members.Count} 个相似项目");
            }
        });
    }

    private void TriggerShakeVisual(BubbleItem bubble)
    {
        // 创建摇晃视觉状态
        _shakeVisualStates[bubble.Id] = new ShakeVisualState(bubble.Id, 1.0f);
        _isShaking = true;
        
        // 2秒后检查并清理
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(2000);
            _shakeVisualStates.Remove(bubble.Id);
            if (_shakeVisualStates.Count == 0)
                _isShaking = false;
        }, DispatcherPriority.Background);
    }

    private void ShowToast(string message)
    {
        // TODO: 实现 Toast 提示
    }

    #endregion

    #region 多选面板

    /// <summary>
    /// 打开聚类多选面板
    /// </summary>
    private void OpenSelectionPanel(ClusterGroup cluster)
    {
        // 如果面板已存在，先关闭
        CloseSelectionPanel();

        // 初始化通知服务
        _notificationService ??= new ToastNotificationService(this);

        // 创建面板
        _selectionPanel = new ClusterSelectionPanel();
        
        // 初始化面板
        _selectionPanel.Initialize(cluster, _notificationService, CloseSelectionPanel);
        
        // 添加到父容器（假设有一个 Overlay 层）
        if (this.Parent is Panel parentPanel)
        {
            // 创建覆盖层容器
            var overlayContainer = new Panel
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            };
            
            // 点击背景关闭
            overlayContainer.PointerPressed += (s, e) =>
            {
                if (e.Source == overlayContainer)
                {
                    CloseSelectionPanel();
                }
            };
            
            // 将面板居中
            _selectionPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            _selectionPanel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            
            overlayContainer.Children.Add(_selectionPanel);
            
            // 找到最顶层的容器
            var root = parentPanel;
            while (root.Parent is Panel next)
            {
                root = next;
            }
            
            // 添加到 Grid 的顶层（如果是 Grid）
            if (root is Grid grid)
            {
                // 确保有足够的行/列
                Grid.SetRowSpan(overlayContainer, grid.RowDefinitions.Count > 0 ? grid.RowDefinitions.Count : 1);
                Grid.SetColumnSpan(overlayContainer, grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1);
                overlayContainer.ZIndex = 100;
                grid.Children.Add(overlayContainer);
            }
            else
            {
                overlayContainer.ZIndex = 100;
                root.Children.Add(overlayContainer);
            }
            
            // 保存引用以便关闭
            _selectionPanel.Tag = overlayContainer;
        }
    }
    
    /// <summary>
    /// 关闭多选面板
    /// </summary>
    private void CloseSelectionPanel()
    {
        if (_selectionPanel?.Tag is Panel overlayContainer)
        {
            if (overlayContainer.Parent is Panel parent)
            {
                parent.Children.Remove(overlayContainer);
            }
        }
        
        _selectionPanel = null;
    }

    #endregion

    #region 动画循环

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_bubbles.Count == 0) return;

        var now = DateTime.Now;
        float deltaTime = Math.Min((float)(now - _lastFrameTime).TotalSeconds, 0.05f);
        _lastFrameTime = now;
        
        // 更新物理
        _physicsEngine.Update(deltaTime);
        
        // 更新聚类动画
        _clusterManager?.Update(deltaTime);
        
        // 更新基础动画
        UpdateBaseAnimations(deltaTime);
        
        // 更新长按进度
        UpdateLongPressProgress(deltaTime);
        
        // 更新粒子
        UpdateParticles(deltaTime);
        
        // 摇晃检测记录
        if (_draggedBubble != null)
        {
            _physicsEngine.RecordBubblePosition(_draggedBubble, _draggedBubble.X, _draggedBubble.Y);
        }
        
        InvalidateVisual();
    }
    
    /// <summary>
    /// 更新长按进度
    /// </summary>
    private void UpdateLongPressProgress(float deltaTime)
    {
        if (!_isLongPressing || _longPressCluster == null) return;
        
        var elapsed = (float)(DateTime.Now - _longPressStartTime).TotalSeconds;
        _longPressProgress = Math.Min(elapsed / _longPressDuration, 1.0f);
        
        // 检查是否完成长按
        if (_longPressProgress >= 1.0f)
        {
            // 触发破裂
            TriggerClusterBreak(_longPressCluster);
            _isLongPressing = false;
            _longPressProgress = 0f;
            _longPressCluster = null;
        }
    }
    
    /// <summary>
    /// 触发聚类破裂
    /// </summary>
    private void TriggerClusterBreak(ClusterGroup cluster)
    {
        // 创建爆炸粒子
        CreateExplosionParticles(cluster.CenterX, cluster.CenterY, cluster.CurrentRadius);
        
        // 执行破裂动画
        _clusterManager?.BreakCluster(cluster.Id);
        _activeCluster = null;
        _isClusterExpanded = false;
    }
    
    /// <summary>
    /// 创建爆炸粒子效果
    /// </summary>
    private void CreateExplosionParticles(float centerX, float centerY, float radius)
    {
        int particleCount = 30 + _particleRandom.Next(20); // 30-50个粒子
        
        for (int i = 0; i < particleCount; i++)
        {
            float angle = (float)(2 * Math.PI * i / particleCount + _particleRandom.NextDouble() * 0.5);
            float speed = 3f + (float)_particleRandom.NextDouble() * 5f;
            float size = 2f + (float)_particleRandom.NextDouble() * 4f;
            
            // 颜色：黄色 -> 橙色 -> 红色
            SKColor color;
            float colorRatio = (float)i / particleCount;
            if (colorRatio < 0.33f)
                color = new SKColor(255, 200, 0); // 黄色
            else if (colorRatio < 0.66f)
                color = new SKColor(255, 150, 0); // 橙色
            else
                color = new SKColor(255, 80, 0);  // 红色
            
            _particles.Add(new Particle
            {
                X = centerX + (float)(_particleRandom.NextDouble() - 0.5) * radius * 0.5f,
                Y = centerY + (float)(_particleRandom.NextDouble() - 0.5) * radius * 0.5f,
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed - 2f, // 初始向上偏移
                Size = size,
                Color = color,
                Decay = 0.015f + (float)_particleRandom.NextDouble() * 0.015f
            });
        }
    }
    
    /// <summary>
    /// 更新粒子
    /// </summary>
    private void UpdateParticles(float deltaTime)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            if (!_particles[i].Update())
            {
                _particles.RemoveAt(i);
            }
        }
    }

    private void UpdateBaseAnimations(float deltaTime)
    {
        foreach (var bubble in _bubbles)
        {
            if (bubble.TwinkleFrequency > 0)
                bubble.TwinklePhase += bubble.TwinkleFrequency * (float)Math.PI * 2 * deltaTime;
            
            bubble.BreathPhase += (float)Math.PI * 2 * deltaTime / bubble.BreathPeriod;
            
            float targetScale = bubble.IsHovered ? 1.1f : 1.0f;
            bubble.HoverScale += (targetScale - bubble.HoverScale) * 10 * deltaTime;
        }
    }

    #endregion

    #region 渲染

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_bubbles.Count == 0) return;

        var bounds = Bounds;
        var clusters = _clusterManager?.GetAllClusters().ToList() ?? new List<ClusterGroup>();
        
        context.Custom(new BubbleCloudRenderOperation(
            _bubbles.ToList(), 
            clusters,
            bounds, 
            _hoveredBubble,
            _isShaking,
            _shakeVisualStates,
            _longPressCluster,
            _longPressProgress,
            _particles));
    }

    #endregion

    #region 交互处理

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        _lastMousePosition = new SKPoint((float)position.X, (float)position.Y);
        
        // 检查是否悬停在聚类上
        if (_activeCluster != null && !_isClusterExpanded)
        {
            float dx = (float)position.X - _activeCluster.CenterX;
            float dy = (float)position.Y - _activeCluster.CenterY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (dist < _activeCluster.CurrentRadius)
            {
                // 悬停在聚类上
                return;
            }
        }
        
        // 查找悬停气泡
        BubbleItem? newHovered = null;
        foreach (var bubble in _bubbles)
        {
            float dx = (float)position.X - bubble.X;
            float dy = (float)position.Y - bubble.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < bubble.Radius + 10)
            {
                newHovered = bubble;
                break;
            }
        }
        
        if (newHovered != _hoveredBubble)
        {
            if (_hoveredBubble != null)
            {
                _hoveredBubble.IsHovered = false;
                _hoveredBubble.IsStopped = false;
            }
            
            _hoveredBubble = newHovered;
            
            if (_hoveredBubble != null)
            {
                _hoveredBubble.IsHovered = true;
                _hoveredBubble.IsStopped = true;
            }
        }
        
        if (_isDragging && _draggedBubble != null)
        {
            _draggedBubble.X = (float)position.X;
            _draggedBubble.Y = (float)position.Y;
            _draggedBubble.VelocityX = 0;
            _draggedBubble.VelocityY = 0;
            _physicsEngine.RecordBubblePosition(_draggedBubble, _draggedBubble.X, _draggedBubble.Y);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var position = e.GetPosition(this);
        
        // 检查是否点击在聚类上 (长按破裂)
        if (_activeCluster != null && !_isClusterExpanded)
        {
            float dx = (float)position.X - _activeCluster.CenterX;
            float dy = (float)position.Y - _activeCluster.CenterY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (dist < _activeCluster.CurrentRadius)
            {
                // 开始长按计时
                _longPressCluster = _activeCluster;
                _longPressStartTime = DateTime.Now;
                _isLongPressing = true;
                _longPressProgress = 0f;
                return;
            }
        }
        
        // 检查是否点击聚类展开模式 (单击展开)
        if (_activeCluster != null && _isClusterExpanded)
        {
            float dx = (float)position.X - _activeCluster.CenterX;
            float dy = (float)position.Y - _activeCluster.CenterY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (dist < _activeCluster.CurrentRadius * 1.5f)
            {
                // 点击聚类展开区域 - 打开多选面板
                OpenSelectionPanel(_activeCluster);
                return;
            }
        }
        
        // 查找点击的气泡
        foreach (var bubble in _bubbles)
        {
            float dx = (float)position.X - bubble.X;
            float dy = (float)position.Y - bubble.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < bubble.Radius)
            {
                if (e.ClickCount == 1)
                {
                    SelectedItem = bubble;
                    _isDragging = true;
                    _draggedBubble = bubble;
                    _dragStartTime = DateTime.Now;
                    bubble.IsDragging = true;
                    bubble.IsStopped = true;
                }
                else if (e.ClickCount == 2)
                {
                    // 双击打开详情
                }
                break;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 取消长按
        if (_isLongPressing)
        {
            _isLongPressing = false;
            _longPressCluster = null;
            _longPressProgress = 0f;
        }
        
        if (_draggedBubble != null)
        {
            _draggedBubble.IsDragging = false;
            _draggedBubble.IsStopped = false;
            _draggedBubble = null;
        }
        _isDragging = false;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // TODO: 缩放视图
    }

    #endregion

    #region 生命周期

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer.Stop();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_bubbles.Count > 0 && e.NewSize.Width > 100 && e.NewSize.Height > 100)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _layoutInitialized = false;
                InitializeLayout();
            }, DispatcherPriority.Background);
        }
    }

    #endregion

    #region 公共方法

    public void Relayout()
    {
        _layoutInitialized = false;
        InitializeLayout();
    }

    public BubbleItem? GetCenterBubble()
    {
        if (LayoutMode == LayoutMode.Honeycomb)
            return _layoutEngine.GetCenterBubble();
        
        float cx = (float)Bounds.Width / 2;
        float cy = (float)Bounds.Height / 2;
        
        return _bubbles.OrderBy(b =>
        {
            float dx = b.X - cx, dy = b.Y - cy;
            return dx * dx + dy * dy;
        }).FirstOrDefault();
    }

    #endregion
}

/// <summary>
/// 布局模式
/// </summary>
public enum LayoutMode
{
    Honeycomb,
    Spiral,
    Grid
}
