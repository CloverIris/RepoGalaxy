# 🌌 RepoGalaxy

> 一个重新定义代码探索体验的 GitHub 第三方桌面客户端

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-11-blue)](https://avaloniaui.net/)
[![FluentAvalonia](https://img.shields.io/badge/FluentAvalonia-WinUI3%20Style-blueviolet)](https://github.com/amwx/FluentAvalonia)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 🚀 项目简介

**RepoGalaxy** 是一款专为开发者打造的 GitHub 第三方桌面客户端，采用 **Fluent Design System** 设计语言，将浩瀚的开源代码宇宙可视化呈现。

> 💻 **跨平台支持**：macOS · Windows · Linux

我们致力于解决一个核心问题：

> *"GitHub 上那些拿到很多 star 的项目是怎么火起来的？我也开发了很多质量很高的仓库，但为什么往往都比较冷清？"*

通过智能推荐算法和独特的视觉体验，RepoGalaxy 帮助开发者发现那些**值得被看见的优质冷门项目**。

---

## ✨ 核心概念

### 🐱 猫猫星球 (The Octo-Planet)
屏幕中央悬浮的 GitHub 星球，星光连线勾勒出 Octocat 轮廓，象征着开源社区的核心。

### 🪨 陨石 Feed 流 (Repo Meteorites)
每个仓库都是一颗漂浮的陨石，大小由 Star 数和活跃度决定，在星轨间穿梭。

### 🌌 星轨系统 (Orbit Paths)
不同技术领域（Rust、AI、Web、DevOps 等）分布在不同轨道深度，解决维度爆炸问题。

### 🧲 引力交互 (Gravity Interaction)
鼠标悬停产生引力效果，陨石放大并展示仓库概览，沉浸式浏览体验。

---

## 🛠️ 技术栈

| 层级 | 技术 |
|------|------|
| **UI 框架** | Avalonia UI + FluentAvalonia |
| **设计语言** | Fluent Design System (WinUI 3 Gallery 风格) |
| **开发语言** | C# 12 (.NET 8) |
| **数据访问** | Entity Framework Core + SQLite |
| **GitHub API** | Octokit.net |
| **架构模式** | MVVM + 分层架构 |

### 为什么选择 Avalonia？

- ✅ **跨平台**：一套代码跑 macOS / Windows / Linux
- ✅ **Fluent Design**：FluentAvalonia 完美复刻 WinUI 3 Gallery 风格
- ✅ **原生性能**：不是 Electron 套壳，真正原生渲染
- ✅ **开发友好**：在 Mac 上就能完整开发和调试
- ✅ **生态成熟**：活跃的社区，丰富的控件库

---

## 📁 项目结构

```
RepoGalaxy/
├── src/
│   ├── RepoGalaxy.Core/              # 领域模型与核心业务
│   ├── RepoGalaxy.Data/              # 数据访问与本地存储
│   ├── RepoGalaxy.GitHub/            # GitHub API 客户端
│   ├── RepoGalaxy.Recommendation/    # 推荐引擎
│   └── RepoGalaxy.Desktop/           # Avalonia 桌面应用
├── Design/                           # 设计文档
├── tests/                            # 单元测试
└── docs/                             # 用户文档
```

---

## 🎯 功能规划

### 第一阶段 - 基础设施
- [ ] GitHub OAuth Device Flow 登录
- [ ] 本地数据持久化 (SQLite)
- [ ] 基础仓库浏览与搜索
- [ ] Fluent Design UI 框架

### 第二阶段 - 智能推荐
- [ ] 个性化推荐算法
- [ ] 冷门项目发现
- [ ] 用户行为学习

### 第三阶段 - 猫猫星球视觉
- [ ] 星球背景可视化
- [ ] 星轨 Feed 流
- [ ] 引力交互效果

### 第四阶段 - 开发者档案
- [ ] 码力六边形能力图
- [ ] 分享卡片生成
- [ ] 蜂巢贡献可视化

---

## 🎨 设计理念

### 视觉语言
- **WinUI 3 Gallery 精致感**：Mica/Acrylic 材质、圆角、流畅动画
- **深邃空间感**：银河系背景、星光粒子效果
- **温暖科技感**：猫猫元素柔化技术感，降低认知门槛

### 交互原则
- **探索式发现**：像探索宇宙一样发现代码
- **沉浸式体验**：减少打断，流畅动画
- **高效实用**：一键 Clone、IDE 集成

---

## 🚧 开发状态

> ⚠️ 当前处于早期设计阶段，尚未发布可用版本。

详见 [Design/ROADMAP.md](./Design/ROADMAP.md) 了解详细开发计划。

---

## 🖥️ 开发环境 (Mac)

```bash
# 1. 安装 .NET 8 SDK
brew install dotnet

# 2. 安装 Avalonia 模板
dotnet new install Avalonia.Templates

# 3. 克隆仓库
git clone https://github.com/yourusername/RepoGalaxy.git
cd RepoGalaxy

# 4. 运行
dotnet run --project src/RepoGalaxy.Desktop
```

**推荐 IDE**：
- [JetBrains Rider](https://www.jetbrains.com/rider/) (最佳 Avalonia 支持)
- [Visual Studio Code](https://code.visualstudio.com/) + C# Dev Kit

---

## 🤝 参与贡献

我们欢迎所有形式的贡献！无论是功能建议、Bug 反馈还是代码提交。

请遵循以下步骤：
1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

---

## 📄 许可证

本项目采用 [MIT](LICENSE) 许可证开源。

---

## 🙏 致谢

- [Avalonia UI](https://avaloniaui.net/) - 优秀的跨平台 .NET UI 框架
- [FluentAvalonia](https://github.com/amwx/FluentAvalonia) - 让 Avalonia 拥有 WinUI 3 的美
- [Octokit.net](https://github.com/octokit/octokit.net) - GitHub API 客户端
- 所有开源社区贡献者

---

<p align="center">
  <strong>Made with 💜 by developers, for developers</strong>
  <br>
  <em>让每一个优质项目都被看见</em>
</p>
