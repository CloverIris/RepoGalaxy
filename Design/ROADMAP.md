# RepoGalaxy 开发路线图

> **当前版本**: v0.3.0 Alpha  
> **最后更新**: 2026-03-14  
> **平台**: macOS / Windows / Linux (Avalonia UI)

---

## 🎯 版本概览

| 版本 | 状态 | 目标 | 完成度 |
|------|------|------|--------|
| v0.1.0 | ✅ 已完成 | MVP 核心验证 | 100% |
| v0.2.0 | ✅ 已完成 | 推荐引擎框架 | 100% |
| v0.3.0 | ✅ 已完成 | Fluent UI + 气泡云可视化 | 100% |
| v0.3.5 | ✅ 已完成 | 高级交互 (拖拽聚类/长按破裂/多选面板) | 100% |
| v0.4.0 | ⏳ 待开始 | 开发者档案 | 0% |
| v0.5.0 | ⏳ 待开始 | 本地集成 | 0% |
| v1.0.0 | ⏳ 待开始 | 正式发布 | 规划中 |

---

## ✅ v0.1.0 - MVP 核心验证 (已完成)

**完成日期**: 2026-03-14  
**目标**: 验证数据模型和 GitHub API 集成可行性

### 已实现功能

- [x] **基础设施**
  - [x] 5层项目结构 (Core/Data/GitHub/Recommendation/Desktop)
  - [x] Fluent Theme 主题配置
  - [x] Microsoft.Extensions.DependencyInjection
  - [x] Serilog 日志系统
  - [x] SQLite + EF Core 数据库

- [x] **GitHub 集成**
  - [x] OAuth Device Flow 登录（主要方式）
  - [x] OAuth Code Flow 登录（备选）
  - [x] Personal Access Token 登录
  - [x] Token 安全存储（AES/DPAPI 加密）✅ **安全漏洞已修复**
  - [x] Token 过期预警机制
  - [x] Octokit API 封装
  - [x] RateLimiter 限流器

- [x] **数据模型**
  - [x] Repository 实体（含发现评分算法）
  - [x] User 实体
  - [x] Bookmark 收藏系统
  - [x] ViewHistory 浏览历史
  - [x] LocalRepository 本地仓库
  - [x] EF Core 配置 + 索引优化
  - [x] 本地缓存机制 (CachedAt)

- [x] **最小可用 UI**
  - [x] 主窗口框架 (72px Sidebar)
  - [x] 星空背景效果
  - [x] Fluent 卡片布局
  - [x] 探索页、收藏页、我的仓库、本地仓库
  - [x] 登录对话框（Device Flow 流程）
  - [x] 基础搜索功能

### 交付标准 ✅
- [x] 能登录 GitHub 账号
- [x] 能搜索和浏览仓库
- [x] 数据能本地缓存
- [x] 有基本的 Fluent Design 外观

---

## ✅ v0.2.0 - 推荐引擎 (已完成)

**完成日期**: 2026-03-14  
**目标**: 实现智能推荐算法

### 已实现

- [x] **协同过滤算法**
  - [x] 基于收藏的相似度计算
  - [x] 基于浏览历史的相似度计算
  - [x] 用户画像自动构建

- [x] **内容推荐**
  - [x] 主题匹配 (Topics)
  - [x] 语言匹配 (Languages)
  - [x] 关键词提取

- [x] **发现价值评分**
  - [x] Repository.CalculateDiscoveryScore() 算法
  - [x] 热度、新鲜度、Fork 比例三维度评分
  - [x] 陨石大小自动分级 (Dust → Moon)

- [x] **推荐引擎框架**
  - [x] 特征提取 (FeatureExtractor)
  - [x] 内容过滤 (ContentFilter)
  - [x] 评分计算 (ScoreCalculator)
  - [x] 多样性保证
  - [x] 实时反馈处理

- [x] **用户画像系统**
  - [x] 兴趣主题分析
  - [x] 兴趣语言分析
  - [x] 自动更新机制

- [x] **多数据源服务**
  - [x] Trending (GitHub API)
  - [x] Personalized (推荐引擎)
  - [x] Search (基于兴趣)
  - [x] Random (高质量抽样)
  - [x] Bookmarks (本地收藏)

- [x] **后台同步服务**
  - [x] 启动时自动同步
  - [x] 定时刷新 (30分钟检查/2小时完整)
  - [x] 智能刷新策略
  - [x] 同步状态跟踪

---

## ✅ v0.3.0 - Fluent UI + 气泡云可视化 (已完成)

**完成日期**: 2026-03-14  
**目标**: Apple Watch 风格气泡云 + 7维视觉编码

### 已实现

- [x] **视觉效果**
  - [x] 星空背景 (SkiaSharp)
  - [x] Octocat 呼吸效果
  - [x] 星光粒子
  - [x] 卡片阴影和圆角
  - [x] Acrylic 材质

- [x] **导航与布局**
  - [x] 72px Sidebar (Microsoft Store 风格)
  - [x] 5项导航 (主页/收藏/仓库/本地/设置)
  - [x] Pill 标签筛选 (热门/推荐/Trending/随机/最新)
  - [x] 页面切换动画

- [x] **基础交互**
  - [x] Hover 效果 (放大/变暗/Tooltip)
  - [x] 点击打开详情卡
  - [x] 收藏/取消收藏
  - [x] 语言筛选
  - [x] 排序切换 (Star/更新时间)

- [x] **数据源切换**
  - [x] ExploreViewModel 多数据源支持
  - [x] 智能推荐接入
  - [x] Trending 接入
  - [x] 收藏/历史接入

- [x] **Apple Watch 风格圆形网格布局**
  - [x] 时间+Star 双维度排序
  - [x] 蜂窝网格布局 (HoneycombLayoutEngine)
  - [x] 中心放大镜效果 (EaseOutQuad)

- [x] **7维视觉编码完整实现**
  - [x] 半径大小 → Star 数量 (非线性阶梯)
  - [x] 内部扇形 → 语言比例 (饼图)
  - [x] 亮度 → 悬停高亮
  - [x] 呼吸缩放 → 气泡脉动

- [x] **慵懒漂浮动画**
  - [x] Android 4.3 彩蛋风格漂浮
  - [x] 物理引擎 (速度/摩擦力/避让/边界)

---

## ✅ v0.3.5 - 高级交互 (已完成)

**完成日期**: 2026-03-14  
**目标**: 实现游戏化交互 (拖拽聚类/长按破裂/多选面板)

### 已实现功能

- [x] **拖拽摇晃聚类**
  - [x] 拖拽气泡跟随鼠标
  - [x] 摇晃检测算法 (>3次/秒方向改变)
  - [x] 金黄色发光效果 (3层发光)
  - [x] 相似项目检索 (推荐引擎+本地相似度)
  - [x] 成员飞入动画 (500ms, EaseOutBack)
  - [x] 大气泡呼吸动画 (±3%)
  - [x] 显示 "+N" 计数徽章

- [x] **多选面板**
  - [x] 点击聚类展开面板
  - [x] 网格显示成员 (最多15个)
  - [x] 卡片选择 (点击/Checkbox)
  - [x] 全选/取消全选
  - [x] 批量收藏功能
  - [x] 批量比较功能
  - [x] 批量导出功能 (Markdown)
  - [x] Toast 通知反馈

- [x] **长按破裂**
  - [x] 3秒长按检测
  - [x] 进度环显示 (黄→橙→红)
  - [x] 百分比文字提示
  - [x] 粒子爆炸效果 (30-50粒子)
  - [x] 成员四散弹射动画
  - [x] 恢复独立漂浮状态

### 新增文件

```
src/RepoGalaxy.Desktop/
├── Controls/
│   ├── ClusterSelectionPanel.axaml      # 多选面板UI
│   ├── ClusterSelectionPanel.axaml.cs   # 面板逻辑
│   ├── ClusterMemberItem.axaml          # 成员卡片UI
│   ├── ClusterMemberItem.axaml.cs       # 卡片逻辑
│   └── BubblePhysicsEngine.cs           # 摇晃检测+物理
├── Services/
│   ├── INotificationService.cs          # 通知接口
│   └── ToastNotificationService.cs      # Toast实现
└── ViewModels/
    └── ClusterSelectionViewModel.cs     # 面板ViewModel
```

---

## ⏳ v0.4.0 - 开发者档案 (规划中)

**目标**: 个人页面和码力系统

### 功能清单
- [ ] **开发者页面**
  - [ ] 个人信息展示
  - [ ] 仓库列表 (卡牌式)
  - [ ] 贡献统计

- [ ] **码力系统**
  - [ ] 六边形能力图 (自定义 Control)
  - [ ] 算法实现
  - [ ] 历史趋势

- [ ] **分享功能**
  - [ ] 生成分享卡片
  - [ ] 导出图片
  - [ ] 复制到剪贴板

---

## ⏳ v0.5.0 - 本地集成 (规划中)

**目标**: 桌面端特有功能

### 功能清单
- [ ] **本地工具集成**
  - [ ] IDE 检测 (VS Code, VS, IDEA, Xcode 等)
  - [ ] 一键 Clone
  - [ ] 用本地 IDE 打开

- [ ] **增强功能**
  - [ ] 通知中心 (Native Notifications)
  - [ ] 后台同步
  - [ ] 数据导出/导入

---

## ⏳ v0.6.0 - 蜂巢贡献图 (规划中)

**目标**: 提交历史可视化

### 功能清单
- [ ] **数据处理**
  - [ ] Commit 历史获取
  - [ ] Tree 结构构建
  - [ ] 时间轴处理

- [ ] **可视化**
  - [ ] 力导向图 (Skia 绘制)
  - [ ] 生长动画
  - [ ] 交互探索

---

## ⏳ v1.0.0 - 正式发布 (规划中)

**目标**: 稳定可用的产品

### 发布前检查
- [ ] 性能优化
- [ ] 错误处理完善
- [ ] 单元测试覆盖 (当前 63 个)
- [ ] 文档完善
- [ ] 安装包制作 (DMG / MSI / AppImage)

---

## 📊 当前实现状态

| 模块 | 状态 | 测试 |
|------|------|------|
| Core 领域模型 | ✅ 完成 | 23 个测试 ✅ |
| Data 数据层 | ✅ 完成 | 13 个测试 ✅ |
| GitHub API 层 | ✅ 完成 | 20 个测试 ✅ |
| Recommendation | ✅ 完成 | 7 个测试 ✅ |
| Desktop UI 基础 | ✅ 完成 | 无测试 ❌ |
| Desktop 高级交互 | ✅ 完成 | 无测试 ❌ |

**总计**: 63 个单元测试，全部通过
**代码文件**: 13 个新文件，3 个修改文件

---

## 🔄 数据流程

```
应用启动
    ↓
数据库初始化 (SQLite)
    ↓
Token 检查 (SecureStorage)
    ↓
已登录? → 启动后台同步服务
    ↓
多数据源获取
    ├── Trending (GitHub API)
    ├── Personalized (推荐引擎)
    ├── Search (兴趣驱动)
    ├── Random (高质量抽样)
    └── Bookmarks (本地)
    ↓
推荐计算 (协同过滤+内容+发现)
    ↓
数据持久化 (SQLite)
    ↓
UI 展示 (气泡云/卡片)
    ↓
用户交互
    ├── 拖拽气泡 → 摇晃检测 → 聚类形成
    ├── 长按聚类 → 进度环 → 粒子破裂
    └── 点击聚类 → 多选面板 → 批量操作
```

### 聚类交互数据流

```
拖拽气泡
    ↓
记录位置历史 (10帧)
    ↓
计算方向改变频率
    ↓
频率 > 3次/秒?
    ├── 是 → 触发摇晃事件
    │           ↓
    │       金黄色发光效果
    │           ↓
    │       调用推荐引擎
    │           ↓
    │       获取相似项目 (最多15个)
    │           ↓
    │       创建聚类 (Forming状态)
    │           ↓
    │       成员飞入动画 (EaseOutBack)
    │           ↓
    │       稳定状态 (呼吸动画)
    │           ↓
    │       等待用户操作
    │           ↓
    │       ├─ 单击 → 展开多选面板
    │       │           ↓
    │       │       显示成员网格
    │       │           ↓
    │       │       批量操作 (收藏/比较/导出)
    │       │           ↓
    │       │       Toast 通知结果
    │       │
    │       └─ 长按3秒 → 显示进度环
    │                   ↓
    │               颜色变化 (黄→橙→红)
    │                   ↓
    │               触发破裂
    │                   ↓
    │               粒子爆炸 (30-50粒子)
    │                   ↓
    │               成员四散弹射
    │                   ↓
    │               聚类解散
    │
    └── 否 → 继续漂浮
```

---

## 🚧 技术债务跟踪

| 债务项 | 状态 | 备注 |
|--------|------|------|
| ~~UserEntity 明文存储 Token~~ | ✅ **已修复** | 改用 SecureStorage |
| ~~Token 过期无预警~~ | ✅ **已修复** | 添加过期检查 |
| ~~推荐引擎算法~~ | ✅ **已完成** | 协同过滤+内容+发现 |
| ~~气泡云漂浮动画~~ | ✅ **已完成** | Android 4.3 风格物理引擎 |
| ~~拖拽摇晃聚类~~ | ✅ **已完成** | 游戏化交互实现 |
| ~~长按破裂效果~~ | ✅ **已完成** | 粒子系统+进度环 |
| ~~多选面板~~ | ✅ **已完成** | 批量操作+Toast通知 |
| Token 自动刷新 | ⏳ 待实现 | 需 RefreshToken 支持 |
| 缺少单元测试 (Desktop) | ⏳ 待实现 | 计划使用 Avalonia.Headless |
| 虚拟化渲染优化 | ⏳ 待实现 | 大数据量时性能优化 |

---

## 📅 更新后的时间线

```
2026 Q1 (3月)
├── ✅ v0.1.0 MVP 已完成
│   └── 基础设施 + GitHub 登录 + 安全存储
├── ✅ v0.2.0 推荐引擎已完成
│   └── 协同过滤 + 内容推荐 + 后台同步
├── ✅ v0.3.0 Fluent UI 已完成
│   └── Apple Watch 网格 + 7维视觉编码
└── ✅ v0.3.5 高级交互 已完成
    └── 拖拽聚类 + 长按破裂 + 多选面板

2026 Q2 (4-5月)
├── 🚧 v0.4.0 开发者档案 (进行中)
│   └── 码力系统 + 六边形能力图
├── v0.5.0 本地集成
│   └── 本地仓库扫描 + IDE 集成
└── v0.6.0 蜂巢贡献图
    └── Commit 历史可视化

2026 Q3-Q4
├── Beta 测试
├── 性能优化
└── v1.0.0 正式发布
```

---

## 📝 决策日志

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-03-13 | Avalonia + Fluent Theme | 跨平台，WinUI 3 风格 |
| 2026-03-13 | 5层架构 | 可测试，可扩展 |
| 2026-03-13 | SQLite 本地存储 | 离线优先 |
| 2026-03-13 | Device Flow 为主 | 无需 Client Secret，开箱即用 |
| 2026-03-14 | 移除 Entity Token 字段 | 安全漏洞修复 |
| 2026-03-14 | 文档精简方案 A | 减少重复，清晰结构 |
| 2026-03-14 | Apple Watch 网格布局 | 圆形图标+紧密排列=直观+美观 |
| 2026-03-14 | Android 4.3 漂浮风格 | 慵懒有机，降低探索焦虑 |
| 2026-03-14 | 拖拽摇晃聚类 | 游戏化发现相似项目 |
| 2026-03-14 | 长按3秒破裂 | 平衡确认与便捷 |
| 2026-03-14 | 聚类最大15个 | 避免屏幕拥挤 |
| 2026-03-14 | 粒子爆炸效果 | 强化破裂反馈 |

---

## 📊 数据模型

### 核心实体

```csharp
// Repository - Feed 流中的"陨石"
public class Repository
{
    public long Id { get; set; }
    public string GitHubId { get; set; }
    public string Owner { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string PrimaryLanguage { get; set; }
    public List<string> Topics { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    // RepoGalaxy 特有
    public double DiscoveryScore { get; set; }
    public MeteoriteSize Size { get; set; }  // Dust -> Moon
    public OrbitCategory Orbit { get; set; } // Core, Web, AI...
}

// 陨石大小分级
public enum MeteoriteSize { Dust, Pebble, Rock, Boulder, Asteroid, Moon }

// 星轨分类
public enum OrbitCategory { Core, Web, Mobile, AI, Data, DevOps, Design, Learning, Experimental }
```

### 数据库表
- **Repositories** - 仓库缓存
- **Bookmarks** - 用户收藏
- **ViewHistories** - 浏览历史
- **Users** - 用户信息（✅ Token 使用 SecureStorage，不再明文存储）
- **UserPreferences** - 用户偏好设置 (新增)
- **LocalRepositories** - 本地 Git 仓库关联

---

## 🔧 Git 集成规划

### 定位：GitHub 浏览器 + 轻量 Git 集成

**核心功能**：
- 扫描本地仓库（自动检测 ~/Code, ~/Projects）
- 关联 GitHub 仓库（读取 remote URL）
- 显示仓库状态（分支、未提交更改、最后提交）
- 快捷打开（VS Code, JetBrains IDE, 终端）

**不做**：完整 Git 客户端功能（commit, push, merge）
- 理由：与 GitHub Desktop/Fork 差异化，专注发现体验

---

## 🔄 迭代原则

1. **数据优先**：先跑通数据流，再优化 UI
2. **小步快跑**：每个版本都有可演示的功能
3. **用户反馈**：早期版本收集反馈调整方向
4. **技术债务**：每个版本预留 20% 时间还技术债
5. **跨平台意识**：功能开发时考虑三平台兼容性

---

## 📚 文档索引

| 文档 | 内容 | 状态 |
|------|------|------|
| [README.md](./README.md) | 设计文档目录 | ✅ |
| [ROADMAP.md](./ROADMAP.md) | 产品路线图、版本规划 | ✅ |
| [UI_DESIGN.md](./UI_DESIGN.md) | UI/UX 规范、视觉系统、动画系统 | ✅ |
| [INTERACTION_DESIGN.md](./INTERACTION_DESIGN.md) | 详细交互规范 (漂浮/拖拽/破裂) | ✅ |
| [TESTING.md](./TESTING.md) | 测试策略 | ✅ |
