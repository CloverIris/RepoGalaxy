# RepoGalaxy 设计文档目录

产品设计与规划文档索引。

---

## 📋 核心文档

| 文档 | 内容 | 状态 |
|------|------|------|
| [ROADMAP.md](./ROADMAP.md) | 产品路线图、版本规划、数据模型、Git 集成 | ✅ 已更新 |
| [UI_DESIGN.md](./UI_DESIGN.md) | UI/UX 规范、视觉系统、动画系统 | ✅ 已更新 |
| [INTERACTION_DESIGN.md](./INTERACTION_DESIGN.md) | 详细交互规范 (漂浮/拖拽聚类/长按破裂) | ✅ 新增 |
| [VISUALIZATION_SYSTEM.md](./VISUALIZATION_SYSTEM.md) | 可视化算法、布局系统、七维编码 | ✅ 新增 |
| [TESTING.md](./TESTING.md) | 测试策略 | ✅ |

---

## 🎯 快速导航

- **产品路线图** → [ROADMAP.md](./ROADMAP.md)
- **UI 设计规范** → [UI_DESIGN.md](./UI_DESIGN.md)
- **交互设计规范** → [INTERACTION_DESIGN.md](./INTERACTION_DESIGN.md)
- **系统审计报告** → [../Docs/SYSTEM_AUDIT_REPORT.md](../Docs/SYSTEM_AUDIT_REPORT.md)

---

## 🎨 设计概览

### 核心创新点

1. **气泡云可视化**: 
   - Apple Watch App Library 风格圆形网格
   - 7维视觉编码 (位置/大小/颜色/亮度/闪烁/呼吸)
   - 时间+Star 双维度排序

2. **慵懒漂浮**: 
   - Android 4.3 彩蛋糖豆风格
   - 物理引擎驱动 (速度/摩擦力/避让)
   - 有机、慵懒的漂浮感

3. **游戏化交互**:
   - 拖拽摇晃聚类 (发现相似项目)
   - 长按破裂 (解散聚类)
   - 3秒倒计时 + 粒子效果

4. **72px Sidebar**: Fluent UI 2 风格导航

### 产品定位

**RepoGalaxy** = GitHub 仓库发现工具 + 数据可视化 + 游戏化交互

### 技术栈

- **UI**: Avalonia UI + Fluent Theme
- **语言**: C# (.NET 9)
- **数据**: EF Core + SQLite
- **GitHub**: Octokit
- **图形**: SkiaSharp

---

## 🔄 数据流程

```
应用启动 → 数据库检查 → Token验证 → 后台同步 → 多数据源混合 → 推荐计算 → UI展示
```

**数据源类型**:
- 🔥 Trending (GitHub 实时趋势)
- ⭐ Personalized (协同过滤+内容推荐)
- 🔍 Search (兴趣语言/主题驱动)
- 🎲 Random (高质量冷门发现)
- 🔖 Bookmarks (用户收藏)

---

## 📝 决策日志

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-03-13 | Avalonia + Fluent | 跨平台，WinUI 3 风格 |
| 2026-03-13 | SQLite 本地存储 | 离线优先，无需后端 |
| 2026-03-13 | 5层架构 | 可测试，可扩展 |
| 2026-03-14 | Apple Watch 网格布局 | 圆形图标+紧密排列=直观+美观 |
| 2026-03-14 | Android 4.3 漂浮风格 | 慵懒有机，降低探索焦虑 |
| 2026-03-14 | 拖拽摇晃聚类 | 游戏化发现相似项目 |

---

## 🗂️ 归档文档

历史文档见 [Archive/](../Archive/)

- PDR.md (产品设计评审)
- ARCHITECTURE.md (已合并到 README)
- DATA_MODEL.md (已合并到 ROADMAP)
- VISUALIZATION_SYSTEM.md (已合并到 UI_DESIGN)
- INTERACTION_DESIGN.md (已从 Archive 恢复并更新)
- GIT_INTEGRATION.md (已合并到 ROADMAP)

---

**设计文档最后更新: 2026-03-14**
