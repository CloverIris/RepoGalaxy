# RepoGalaxy - GitHub 

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-11.3.12-blue)](https://avaloniaui.net/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**RepoGalaxy** 是一款跨平台的 GitHub 仓库探索工具，采用 Fluent UI 2 设计风格，通过多维度的数据可视化和智能推荐算法，帮助开发者发现被埋没的优质开源项目。

![Concept](Design/concept.png)

---

## 🚀 快速开始

### 环境要求
- .NET 9.0 SDK

### 构建运行
```bash
git clone <repo-url>
cd RepoGalaxy
dotnet restore
dotnet build
dotnet run --project src/RepoGalaxy.Desktop
```

---

## 🏗️ 架构概览

### 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation    │ RepoGalaxy.Desktop (Avalonia + MVVM)     │
├─────────────────────────────────────────────────────────────┤
│  Application     │ RepoGalaxy.GitHub (OAuth, API, Sync)     │
├─────────────────────────────────────────────────────────────┤
│  Domain          │ RepoGalaxy.Core + Recommendation         │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure  │ RepoGalaxy.Data (EF Core + SQLite)       │
└─────────────────────────────────────────────────────────────┘
```

### 关键特性
- **Clean Architecture**: 接口驱动，便于测试
- **MVVM 模式**: CommunityToolkit.Mvvm 源生成器
- **安全存储**: 平台原生加密 (Windows DPAPI / macOS&Linux AES)
- **OAuth 支持**: Device Flow (推荐) / Code Flow / PAT

---

## 📁 项目结构

```
src/
├── RepoGalaxy.Core              # 领域模型、接口
├── RepoGalaxy.Data              # EF Core、仓储
├── RepoGalaxy.GitHub            # GitHub API、认证
├── RepoGalaxy.Recommendation    # 推荐算法
└── RepoGalaxy.Desktop           # Avalonia UI

tests/                           # 单元测试 (63个)
Design/                          # 设计文档
├── ROADMAP.md                   # 产品路线图
├── UI_DESIGN.md                 # UI/UX 规范
└── TESTING.md                   # 测试策略
```

---

## 🔐 安全设计

### Token 存储
| 平台 | 加密方式 |
|------|----------|
| Windows | DPAPI |
| macOS | AES + 文件权限 |
| Linux | AES + 文件权限 |

### OAuth 流程
- ✅ Device Flow（无需 Client Secret，推荐）
- ✅ Code Flow（需 Client Secret）
- ✅ Personal Access Token（备用）

---

## 🎯 当前状态

**版本**: v0.3.0 Alpha

### 已完成功能
- ✅ Fluent UI 2 主界面 (72px Sidebar)
- ✅ OAuth Device Flow 登录
- ✅ 跨平台 Token 安全存储
- ✅ 仓库探索、收藏、本地管理
- ✅ 63个单元测试

### 进行中
- ⏳ 设置页面
- ⏳ 推荐引擎接入

详细路线图见 [Design/ROADMAP.md](Design/ROADMAP.md)

---

## 🛠️ 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| Framework | .NET | 9.0 |
| UI | Avalonia UI | 11.3.12 |
| Database | SQLite + EF Core | 9.0.3 |
| GitHub API | Octokit | Latest |
| MVVM | CommunityToolkit.Mvvm | 8.2.1 |

---

## 📄 许可证

MIT License

---

<p align="center">Made with ❤️ for the Open Source Community</p>
