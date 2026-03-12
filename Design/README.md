# RepoGalaxy 设计文档目录

本目录包含 RepoGalaxy 项目的所有设计文档和架构决策记录。

## 📋 文档索引

### 核心文档

| 文档 | 路径 | 说明 |
|------|------|------|
| **PDR** | [./PDR.md](./PDR.md) | **产品设计需求文档 (Product Design Requirements)** - 项目的权威规范 |
| 架构设计 | [./ARCHITECTURE.md](./ARCHITECTURE.md) | 技术架构、技术栈选型、项目结构 |
| 数据模型 | [./DATA_MODEL.md](./DATA_MODEL.md) | 领域模型、数据库实体、API DTO |
| 路线图 | [./ROADMAP.md](./ROADMAP.md) | 版本规划、里程碑、当前任务 |

### 详细设计文档

| 文档 | 路径 | 说明 |
|------|------|------|
| 交互设计 | [./INTERACTION_DESIGN.md](./INTERACTION_DESIGN.md) | 完整的交互系统规范 (气泡云、拖拽聚类、破裂动画) |
| 可视化系统 | [./VISUALIZATION_SYSTEM.md](./VISUALIZATION_SYSTEM.md) | 7维视觉编码系统 (大小/颜色/亮度/闪烁/呼吸等) |
| 边缘缩放 | [./EDGE_SCALING.md](./EDGE_SCALING.md) | 视口边缘非线性缩放设计 |
| UI 设计 | [./UI_DESIGN.md](./UI_DESIGN.md) | 整体UI框架、导航结构、页面布局 |
| Git 集成 | [./GIT_INTEGRATION.md](./GIT_INTEGRATION.md) | 轻量级Git功能设计 |

## 🎯 快速导航

### 如果你是产品经理
→ 先看 [PDR.md](./PDR.md) 了解产品全貌

### 如果你是设计师
→ 先看 [INTERACTION_DESIGN.md](./INTERACTION_DESIGN.md) 和 [VISUALIZATION_SYSTEM.md](./VISUALIZATION_SYSTEM.md)

### 如果你是开发者
→ 先看 [ARCHITECTURE.md](./ARCHITECTURE.md) 和 [PDR.md](./PDR.md) 的技术要求部分

### 如果你是项目经理
→ 先看 [PDR.md](./PDR.md) 和 [ROADMAP.md](./ROADMAP.md)

## 📊 设计概览

### 核心创新点

1. **7维数据可视化**: 一个圆形同时展示 Star/语言/活跃度/新旧/Fork/时间/流行度
2. **慵懒漂浮气泡云**: Android 彩蛋风格的物理漂浮效果
3. **拖拽聚类**: 摇晃鼠标触发相似项目检索，形成大气泡
4. **长按破裂**: 3秒长按倒计时后炸裂散开
5. **分层导航**: 气泡 → 详情卡 → 全屏仓库

### 产品定位

**RepoGalaxy** = GitHub 第三方客户端 + 数据可视化工具 + 游戏化探索体验

### 目标平台

- **主要**: macOS (开发主力平台)
- **次要**: Windows / Linux (Avalonia 跨平台)

### 技术栈

- **UI**: Avalonia UI + FluentAvalonia (WinUI 3 风格)
- **语言**: C# 12 (.NET 8)
- **数据**: EF Core + SQLite
- **GitHub**: Octokit.net
- **渲染**: SkiaSharp

## 🎨 设计原则

1. **数据可视化优先** - 用视觉而非列表呈现信息
2. **游戏化交互** - 有趣的拖拽、摇晃、聚类操作
3. **沉浸式体验** - 星空背景贯穿始终
4. **渐进式复杂度** - 默认简单，高级功能可选
5. **性能优先** - 60fps 流畅体验

## 📝 决策日志

| 日期 | 决策 | 文档 | 理由 |
|------|------|------|------|
| 2026-03-13 | 选择 Avalonia + FluentAvalonia | ARCHITECTURE.md | Mac 开发友好，完美复刻 WinUI 3 |
| 2026-03-13 | 气泡云可视化方案 | VISUALIZATION_SYSTEM.md | 7维数据同时呈现 |
| 2026-03-13 | 拖拽聚类交互 | INTERACTION_DESIGN.md | 创新的项目发现方式 |
| 2026-03-13 | 轻量级 Git 集成 | GIT_INTEGRATION.md | 不做重型客户端 |
| 2026-03-13 | 分层架构 | ARCHITECTURE.md | 便于测试和扩展 |

## 🚧 项目状态

- **阶段**: 设计完成，待开发
- **PDR 版本**: v1.0
- **最后更新**: 2026-03-13

---

> 💡 **提示**: 所有设计文档都是活的，会随开发进度持续更新。建议定期回顾 PDR 确保一致性。
