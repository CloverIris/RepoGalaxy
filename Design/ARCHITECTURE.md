# RepoGalaxy 架构设计文档

> 状态：规划中 | 最后更新：2026-03-13

---

## 1. 技术栈选型 (Mac 开发适配)

| 层级 | 技术 | 说明 |
|------|------|------|
| **UI 框架** | Avalonia UI + FluentAvalonia | 跨平台 .NET UI，完美复刻 WinUI 3 Gallery 风格 |
| **开发语言** | C# 12 | .NET 8 |
| **数据访问** | Entity Framework Core + SQLite | 本地数据持久化 |
| **GitHub API** | Octokit.net | 官方 .NET SDK |
| **HTTP 客户端** | HttpClient / Refit | API 调用封装 |
| **序列化** | System.Text.Json | 高性能 JSON 处理 |
| **图表/可视化** | Avalonia.Skia / LiveCharts2 | 星轨、雷达图等视觉效果 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection | 原生 DI 支持 |
| **配置管理** | Microsoft.Extensions.Configuration | 应用配置 |

### 为什么选 Avalonia？

1. **Mac 原生支持**：开发、调试、运行都在 macOS 上无缝进行
2. **Fluent Design**：FluentAvalonia 主题包几乎 1:1 复刻 WinUI 3 Gallery
3. **单一代码库**：Windows 用户也能享受同样体验
4. **性能优秀**：原生渲染，不是 Electron 套壳
5. **生态成熟**：社区活跃，文档完善

---

## 2. 项目结构

```
RepoGalaxy/
├── RepoGalaxy.sln                    # 解决方案
├── src/
│   ├── RepoGalaxy.Core/              # 核心领域层 (纯 .NET)
│   │   ├── Models/                   # 数据模型
│   │   ├── Interfaces/               # 接口定义
│   │   ├── Services/                 # 核心业务逻辑
│   │   └── Constants/                # 常量定义
│   │
│   ├── RepoGalaxy.Data/              # 数据访问层 (跨平台)
│   │   ├── DbContexts/               # EF Core DbContext
│   │   ├── Entities/                 # 数据库实体
│   │   ├── Repositories/             # 仓储模式
│   │   └── Migrations/               # 数据库迁移
│   │
│   ├── RepoGalaxy.GitHub/            # GitHub API 层
│   │   ├── Clients/                  # API 客户端封装
│   │   ├── Auth/                     # OAuth / Token 管理
│   │   ├── Models/                   # DTO / API 响应模型
│   │   └── Extensions/               # 扩展方法
│   │
│   ├── RepoGalaxy.Recommendation/    # 推荐引擎层
│   │   ├── Engine/                   # 推荐算法核心
│   │   ├── Features/                 # 特征工程
│   │   ├── Filters/                  # 过滤策略
│   │   └── Scoring/                  # 评分算法
│   │
│   └── RepoGalaxy.Desktop/           # Avalonia 桌面应用
│       ├── App.axaml                 # 应用入口
│       ├── App.axaml.cs
│       ├── MainWindow.axaml          # 主窗口
│       ├── MainWindow.axaml.cs
│       ├── Views/                    # 页面/视图
│       ├── ViewModels/               # MVVM 视图模型
│       ├── Controls/                 # 自定义控件
│       ├── Behaviors/                # 交互行为
│       ├── Converters/               # 值转换器
│       ├── Styles/                   # 样式资源
│       ├── Themes/                   # FluentAvalonia 主题定制
│       └── Assets/                   # 图片、图标等资源
│
├── tests/                            # 单元测试项目
├── Design/                           # 设计文档
└── docs/                             # 用户文档
```

---

## 3. Avalonia + FluentAvalonia 视觉系统

### 3.1 FluentAvalonia 简介

FluentAvalonia 是一个 Avalonia UI 的主题包，提供了与 WinUI 3 / Windows App SDK 几乎一致的视觉体验：

- **Mica/Acrylic 材质**：毛玻璃背景效果
- **Fluent 控件样式**：Button、TextBox、NavigationView 等
- **Reveal 效果**：鼠标悬停聚光灯
- **圆角设计**：符合 Fluent Design 规范
- **深色/浅色模式**：自动切换

### 3.2 主题配置

```xml
<!-- App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:sty="using:FluentAvalonia.Styling">
    <Application.Styles>
        <!-- FluentAvalonia 主题 -->
        <sty:FluentAvaloniaTheme PreferSystemTheme="True" 
                                  PreferUserAccentColor="True" />
        
        <!-- 自定义样式 -->
        <StyleInclude Source="/Styles/RepoGalaxyStyles.axaml" />
    </Application.Styles>
</Application>
```

### 3.3 控件映射 (WinUI 3 → Avalonia)

| WinUI 3 | FluentAvalonia | 用途 |
|---------|----------------|------|
| `NavigationView` | `NavigationView` | 侧边导航 |
| `Frame` | `Frame` | 页面导航 |
| `TeachingTip` | `TeachingTip` | 提示气泡 |
| `ContentDialog` | `ContentDialog` | 对话框 |
| `ProgressRing` | `ProgressRing` | 加载动画 |
| `TreeView` | `TreeView` | 树形列表 |
| `MenuFlyout` | `MenuFlyout` | 右键菜单 |

---

## 4. 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                 RepoGalaxy.Desktop (Avalonia)               │
│              (Views + ViewModels + Fluent UI)               │
│                         ↑↓ MVVM                             │
├─────────────────────────────────────────────────────────────┤
│                  RepoGalaxy.Recommendation                  │
│           (推荐算法 + 内容过滤 + 排序策略)                     │
│                         ↑↓ 依赖                             │
├─────────────────────────────────────────────────────────────┤
│                    RepoGalaxy.GitHub                        │
│            (GitHub API Client + OAuth 认证)                 │
│                         ↑↓ 依赖                             │
├─────────────────────────────────────────────────────────────┤
│                     RepoGalaxy.Data                         │
│              (EF Core + SQLite + 本地缓存)                   │
│                         ↑↓ 依赖                             │
├─────────────────────────────────────────────────────────────┤
│                     RepoGalaxy.Core                         │
│              (领域模型 + 接口定义 + 业务规则)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Mac 开发环境配置

### 5.1 必备工具

```bash
# .NET 8 SDK
brew install dotnet

# IDE 选项 (任选其一):
# 1. JetBrains Rider (推荐) - 跨平台，Avalonia 支持好
# 2. Visual Studio Code + C# Dev Kit
# 3. Visual Studio for Mac (如果还在维护)
```

### 5.2 创建项目命令

```bash
# 安装 Avalonia 模板
dotnet new install Avalonia.Templates

# 创建解决方案
dotnet new sln -n RepoGalaxy

# 创建项目 (使用 FluentAvalonia 模板)
dotnet new avalonia.app -n RepoGalaxy.Desktop -o src/RepoGalaxy.Desktop

# 添加 FluentAvalonia 包
dotnet add src/RepoGalaxy.Desktop package FluentAvalonia

# 创建其他类库...
```

### 5.3 运行

```bash
dotnet run --project src/RepoGalaxy.Desktop
```

---

## 6. 关键设计原则

### 6.1 关注点分离
- **Core 层**：纯领域逻辑，不依赖任何 UI 框架
- **Data 层**：仅负责数据持久化
- **GitHub 层**：仅负责外部 API 通信
- **Desktop 层**：仅负责 UI 呈现和用户交互

### 6.2 依赖注入
- 所有服务通过 DI 容器管理生命周期
- 接口定义在 Core，实现在各层
- 便于单元测试和模块替换

### 6.3 响应式编程
- 使用 `ReactiveUI` 或 `CommunityToolkit.Mvvm`
- API 调用异步化，UI 非阻塞
- 进度反馈和取消令牌支持

### 6.4 离线优先
- GitHub 数据本地缓存
- 优先读取本地，后台同步更新
- 支持离线浏览历史数据

---

## 7. 第三方依赖规划

| 包名 | 用途 | 版本 |
|------|------|------|
| Avalonia | UI 框架 | 11.x |
| FluentAvalonia | Fluent Design 主题 | Latest |
| Avalonia.ReactiveUI | 响应式 MVVM | Latest |
| Octokit | GitHub API 客户端 | Latest |
| Microsoft.EntityFrameworkCore.Sqlite | 本地数据库 | Latest |
| CommunityToolkit.Mvvm | MVVM 工具包 | Latest |
| Microsoft.Extensions.DependencyInjection | DI 容器 | Latest |
| Serilog | 日志记录 | Latest |

---

## 8. 跨平台兼容性

| 功能 | Windows | macOS | Linux |
|------|---------|-------|-------|
| 基础 UI | ✅ | ✅ | ✅ |
| Fluent Design | ✅ | ✅ | ✅ |
| 系统托盘 | ✅ | ✅ | ✅ |
| 通知中心 | ✅ | ✅ | ✅ |
| 本地 IDE 集成 | ✅ | ⚠️ 需适配 | ⚠️ 需适配 |
| Token 安全存储 | ✅ DPAPI | ✅ Keychain | ✅ Secret Service |

---

## 9. 性能目标

| 指标 | 目标值 |
|------|--------|
| 应用冷启动 | < 3 秒 |
| 首页数据加载 | < 1 秒（从缓存）|
| 搜索响应 | < 500ms |
| 内存占用 | < 300MB |
| 数据库查询 | < 100ms |

---

## 10. 待决策事项

- [ ] 是否使用 ReactiveUI 还是纯 CommunityToolkit.Mvvm？
- [ ] Avalonia.Skia 还是 LiveCharts2 实现图表？
- [ ] 是否使用 Avalonia.Xaml.Behaviors？
- [ ] 打包方式：单文件发布 vs 安装包？
