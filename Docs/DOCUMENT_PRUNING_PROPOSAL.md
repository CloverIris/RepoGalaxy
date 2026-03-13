# 文档精简方案

**分析日期**: 2026-03-13  
**现状**: 文档分散、重复、部分内容已过时

---

## 一、当前文档清单

### 根目录文档

| 文档 | 大小 | 内容概要 | 建议 |
|------|------|---------|------|
| `README.md` | 11KB | 项目介绍、架构概览、快速开始 | **保留** (精简) |
| `ARCHITECTURE.md` | 16KB | 5层架构详细说明 | **合并** → README |
| `AGENTS.md` | 6KB | AI Agent 工作约定 | **保留** (可选) |
| `OAUTH_FLOW.md` | 6KB | OAuth 实现细节 | **合并** → 代码注释 |
| `STATUS.md` | 11KB | 开发状态追踪 | **删除** (用 Git 替代) |

### Design/ 目录文档

| 文档 | 大小 | 内容概要 | 建议 |
|------|------|---------|------|
| `README.md` | 3KB | Design 目录索引 | **保留** |
| `ROADMAP.md` | 6KB | 版本规划 | **保留** (精简) |
| `ARCHITECTURE.md` | 10KB | 架构设计 | **删除** (与根目录重复) |
| `DATA_MODEL.md` | 12KB | 数据模型 | **合并** → ROADMAP |
| `UI_DESIGN.md` | 18KB | UI 设计规范 | **保留** (精简) |
| `VISUALIZATION_SYSTEM.md` | 23KB | 可视化系统 | **合并** → UI_DESIGN |
| `INTERACTION_DESIGN.md` | 21KB | 交互设计 | **合并** → UI_DESIGN |
| `EDGE_SCALING.md` | 16KB | 边界扩展算法 | **合并** → 代码文档 |
| `GIT_INTEGRATION.md` | 10KB | Git 集成 | **合并** → ROADMAP |
| `PDR.md` | 16KB | 设计评审记录 | **归档** (移到 Archive/) |
| `TESTING.md` | 11KB | 测试策略 | **保留** |

### Docs/ 目录文档

| 文档 | 大小 | 建议 |
|------|------|------|
| `SYSTEM_AUDIT_REPORT.md` | 8KB | **保留** (定期更新) |

---

## 二、精简方案

### 方案 A: 保守精简 (推荐)

保留核心文档，合并重复内容

**保留文档** (精简后):
```
RepoGalaxy/
├── README.md                 # 项目入口 (合并 ARCHITECTURE 概要)
├── AGENTS.md                 # AI 工作约定 (可选)
├── Design/
│   ├── README.md             # 设计文档索引
│   ├── ROADMAP.md            # 产品路线图 (合并 DATA_MODEL + GIT_INTEGRATION)
│   ├── UI_DESIGN.md          # UI/UX 规范 (合并 VISUALIZATION + INTERACTION)
│   └── TESTING.md            # 测试策略
└── Docs/
    ├── SYSTEM_AUDIT_REPORT.md    # 系统审计
    └── API_REFERENCE.md          # 新增: API 文档
```

**删除/合并文档**:
- `ARCHITECTURE.md` → 合并到 README.md
- `OAUTH_FLOW.md` → 转为代码内 XML 注释
- `STATUS.md` → 使用 GitHub Issues/Projects
- `Design/ARCHITECTURE.md` → 删除（与根目录重复）
- `Design/DATA_MODEL.md` → 合并到 ROADMAP
- `Design/VISUALIZATION_SYSTEM.md` → 合并到 UI_DESIGN
- `Design/INTERACTION_DESIGN.md` → 合并到 UI_DESIGN
- `Design/EDGE_SCALING.md` → 转为代码文档
- `Design/GIT_INTEGRATION.md` → 合并到 ROADMAP
- `Design/PDR.md` → 移到 Archive/PDR.md

### 方案 B: 激进精简

只保留最核心文档，其他全部归档

**保留文档**:
```
RepoGalaxy/
├── README.md                 # 包含：简介、架构、快速开始
├── Design/
│   ├── ROADMAP.md            # 包含：版本规划 + 数据模型
│   └── UI_DESIGN.md          # 包含：视觉 + 交互
└── Archive/                  # 新建：历史文档归档
    ├── PDR.md
    ├── VISUALIZATION_SYSTEM.md
    └── ...
```

---

## 三、具体合并建议

### 1. README.md 结构 (精简后)

```markdown
# RepoGalaxy

## 简介
项目简介 + 截图

## 快速开始
安装和运行

## 架构 (从 ARCHITECTURE 合并)
- 5层架构图
- 各层职责简述
- 依赖关系

## 功能特性 (从 STATUS 合并)
已实现 vs 规划中

## 开发
链接到 Design/ROADMAP.md
```

### 2. ROADMAP.md 结构 (合并后)

```markdown
# RepoGalaxy 路线图

## 版本规划
...现有内容...

## 数据模型 (从 DATA_MODEL 合并)
ER 图 + 关键实体说明

## Git 集成 (从 GIT_INTEGRATION 合并)
本地仓库扫描功能说明

## 当前进度
用表格替代 STATUS.md
```

### 3. UI_DESIGN.md 结构 (合并后)

```markdown
# UI/UX 设计

## 视觉系统 (从 VISUALIZATION 合并)
- 配色
- 字体
- 图标

## 布局规范 (从 INTERACTION 合并)
- 72px Sidebar
- 卡片布局
- 间距系统

## 交互设计 (从 INTERACTION 合并)
- 动画规范
- 手势支持
- 反馈机制

## 核心视觉概念
- 陨石大小分级
- 轨道分类
- 星场背景
```

---

## 四、待澄清问题

请在实施精简前确认以下问题：

### 问题 1: AGENTS.md 是否保留?
- **选项 A**: 保留 (对 AI 协作者有用)
- **选项 B**: 删除 (放入 .cursorrules 或类似)
- **选项 C**: 合并到 README.md

### 问题 2: OAUTH_FLOW.md 处理方式?
- **选项 A**: 转为代码内 XML 注释 (推荐)
- **选项 B**: 保留 (作为实现文档)
- **选项 C**: 合并到 SYSTEM_AUDIT_REPORT

### 问题 3: STATUS.md 是否保留?
- **选项 A**: 删除 (用 GitHub Issues 替代)
- **选项 B**: 保留 (作为开发进度跟踪)
- **选项 C**: 合并到 ROADMAP

### 问题 4: PDR.md 如何处理?
- **选项 A**: 移到 Archive/ 目录
- **选项 B**: 删除 (历史记录用 Git)
- **选项 C**: 保留 (设计决策需要追溯)

### 问题 5: TESTING.md 是否保留独立?
- **选项 A**: 保留独立 (测试策略重要)
- **选项 B**: 合并到 ROADMAP
- **选项 C**: 转为 CONTRIBUTING.md 的一部分

### 问题 6: 是否创建 CONTRIBUTING.md?
- **选项 A**: 创建 (包含测试、代码规范)
- **选项 B**: 不创建 (在 README 简要说明)

### 问题 7: API 文档如何处理?
- **选项 A**: 创建 Docs/API_REFERENCE.md
- **选项 B**: 使用 XML 注释 + DocFX/Swagger
- **选项 C**: 暂不处理

---

## 五、实施步骤

1. **确认方案** (等待回答以上问题)
2. **创建 Archive/** 目录
3. **移动待归档文档**
4. **合并文档** (按上述结构)
5. **更新 README** 链接
6. **清理旧文档**

---

**建议**: 采用 **方案 A (保守精简)**，在保留核心信息的前提下减少重复，使文档结构更清晰。
