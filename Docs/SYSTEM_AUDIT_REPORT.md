# RepoGalaxy 系统逻辑审计报告

**审计日期**: 2026-03-13  
**审计范围**: Core → Data → GitHub → Desktop 全链路  
**状态**: 功能完整，发现若干待优化点

---

## 一、数据基础建设完整性 ✅

### 1.1 领域模型 (Core.Models)

| 实体 | 状态 | 说明 |
|------|------|------|
| `User` | ✅ 完整 | 包含 GitHub 基础信息、Token 字段、统计信息 |
| `Repository` | ✅ 完整 | 含发现评分算法、陨石大小计算、轨道分类 |
| `Bookmark` | ✅ 完整 | 支持收藏夹分组、优先级、笔记 |
| `UserPreference` | ⚠️ 未使用 | 定义完整但未持久化到数据库 |
| `LocalRepository` | ✅ 完整 | 本地 Git 仓库关联 |

### 1.2 数据库实体 (Data.Entities)

| 实体 | 状态 | 与 Core 映射 |
|------|------|-------------|
| `UserEntity` | ✅ 完整 | 字段匹配，Token 明文存储 ⚠️ |
| `RepositoryEntity` | ✅ 完整 | Topics 和 Languages 使用 JSON 序列化 |
| `BookmarkEntity` | ✅ 完整 | 外键关联正确 |
| `ViewHistoryEntity` | ✅ 完整 | 包含访问来源追踪 |
| `LocalRepositoryEntity` | ✅ 完整 | 路径唯一索引 |

### 1.3 数据上下文 (RepoGalaxyDbContext)

**状态**: ✅ 配置正确
- SQLite 文件路径: `~/Library/Application Support/RepoGalaxy/repogalaxy.db`
- 索引设计合理（Owner+Name 唯一、Stars、UpdatedAt）
- 数据库初始化: `EnsureCreated()` 在 App.OnFrameworkInitializationCompleted 中调用

### 1.4 安全存储 (ISecureStorage / SecureStorage)

**状态**: ✅ 实现完整

| 平台 | 实现方式 | 状态 |
|------|---------|------|
| Windows | DPAPI | ✅ |
| macOS | AES + 文件权限 | ✅ (简化实现) |
| Linux | AES + 文件权限 | ✅ |

**密钥管理**: 
- Windows: 系统托管
- macOS/Linux: 生成 256-bit 主密钥存储在 `master.key`，文件权限 600

---

## 二、登录全生命周期逻辑

### 2.1 登录流程完整度 ✅

```
[用户点击登录]
    ↓
[LoginDialogViewModel.StartDeviceLogin]
    ↓
[GitHubAuthService.StartDeviceFlowAsync]
    POST https://github.com/login/device/code
    获取 device_code + user_code + interval + expires_in
    ↓
[显示 UserCode + 打开浏览器]
    ↓
[后台轮询 PollForTokenAsync]
    POST https://github.com/login/oauth/access_token
    grant_type=urn:ietf:params:oauth:grant-type:device_code
    ↓
[获取 access_token]
    ↓
[GitHubTokenManager.SaveTokenAsync]
    加密存储到 ISecureStorage
    ↓
[MainWindowViewModel.LoginAsync]
    _apiClient.SetAccessToken(token) ← 已修复
    ↓
[LoadCurrentUserAsync]
    获取用户信息 → 更新 UI
```

### 2.2 Token 生命周期管理

| 阶段 | 逻辑 | 状态 |
|------|------|------|
| 存储 | `SecureStorage.SetAsync` AES/DPAPI 加密 | ✅ |
| 获取 | `SecureStorage.GetAsync` 解密 | ✅ |
| 过期检查 | `IsTokenExpiredAsync` 5分钟缓冲 | ✅ |
| 自动刷新 | **缺失** | ❌ |
| 清除 | `ClearTokenAsync` 覆盖为空字符串 | ✅ |

**⚠️ 发现问题**: Token 刷新逻辑未实现，过期后需要重新登录

### 2.3 会话状态管理

| 检查点 | 实现 | 状态 |
|--------|------|------|
| 启动时恢复 | `InitializeAsync` 读取 Token → 设置 ApiClient | ✅ |
| UI 状态绑定 | `IsAuthenticated` 绑定到用户头像显示 | ✅ |
| 过期检测 | 无自动检测，API 调用失败时才知晓 | ⚠️ |
| 并发控制 | 单用户设计，无多账户切换 | ✅ |

---

## 三、账户系统细节

### 3.1 用户信息存储

**Core.Models.User** 与 **UserEntity** 字段对比:

| 字段 | Core | Entity | 差异 |
|------|------|--------|------|
| Id | long | long | ✅ 一致 |
| GitHubId | string | string | ✅ 一致 |
| Login | string | string | ✅ 一致 |
| AvatarUrl | string | string? | ⚠️ Entity 可空 |
| AccessToken | string? | string? | ⚠️ Entity 明文风险 |
| RefreshToken | string? | **缺失** | ❌ 未存储 |
| TokenExpiresAt | DateTimeOffset? | DateTimeOffset? | ✅ 一致 |

**⚠️ 安全问题**: 
1. `UserEntity.AccessToken` 明文存储，应使用 SecureStorage
2. `RefreshToken` 字段缺失，无法实现自动刷新

### 3.2 用户偏好设置

**状态**: ❌ 未持久化
- `UserPreference` 模型定义完整
- 但未在 DbContext 中注册
- 无对应的 PreferenceEntity
- 无读写接口

### 3.3 多设备同步

**状态**: ❌ 不支持
- 所有数据本地存储
- 无云端同步机制
- Token 绑定到设备

---

## 四、发现的问题汇总

### 🔴 严重问题

1. **Token 刷新机制缺失**
   - 影响: Token 过期后用户必须重新登录
   - 解决: 实现 `RefreshToken` 存储和自动刷新逻辑

2. **UserEntity 明文存储 Token**
   - 影响: 数据库文件被拷贝后 Token 泄露
   - 解决: 移除 Entity 中的 Token 字段，强制使用 SecureStorage

### 🟡 中等问题

3. **UserPreference 未实现**
   - 影响: 用户设置无法持久化
   - 解决: 添加 PreferenceEntity 和读写服务

4. **Token 过期无预警**
   - 影响: API 调用突然失败
   - 解决: 启动时检查 Token 过期时间，提前提醒

### 🟢 建议优化

5. **缺少登录状态变更事件**
   - 建议: 添加 `IAuthenticationStateProvider` 接口

6. **SecureStorage 密钥文件权限**
   - 建议: macOS 使用 Keychain 而非文件存储主密钥

---

## 五、功能实现状态 vs ROADMAP

### v0.1.0 MVP 检查

| 功能 | 设计文档 | 实现状态 | 备注 |
|------|---------|---------|------|
| 项目结构 | ✅ | ✅ | 5层架构清晰 |
| Fluent UI | ✅ | ✅ | 使用内置 FluentTheme |
| DI + 日志 | ✅ | ✅ | Microsoft DI + Serilog |
| SQLite | ✅ | ✅ | EF Core + SQLite |
| OAuth Device Flow | ✅ | ✅ | 完整实现 |
| Token 安全存储 | ✅ | ⚠️ | 部分正确，Entity 有冗余字段 |
| Octokit 封装 | ✅ | ✅ | GitHubApiClient |
| Rate Limit | ✅ | ✅ | RateLimiter 服务 |
| Repository CRUD | ✅ | ✅ | RepositoryService |
| 本地缓存 | ✅ | ✅ | CachedAt 字段 + EF 查询 |
| NavigationView | ✅ | ✅ | 72px Sidebar 实现 |
| Mica/Acrylic | ✅ | ⚠️ | 背景效果未完全实现 |
| 仓库列表 | ✅ | ✅ | 卡片式布局 |
| 搜索 | ⚠️ | ⚠️ | 基础搜索有，高级筛选待完善 |
| 设置页 | ✅ | ⚠️ | 占位状态，功能未完整 |

### v0.2.0 推荐引擎检查

| 功能 | 状态 | 备注 |
|------|------|------|
| 基础评分模型 | ✅ | Repository.CalculateDiscoveryScore |
| 用户行为追踪 | ⚠️ | ViewHistory 有记录，但未用于推荐 |
| 协同过滤 | ❌ | 未实现 |
| 内容标签匹配 | ❌ | Topics 字段有，但未实现匹配算法 |
| Trending API | ⚠️ | RepositorySyncService 有基础同步 |

---

## 六、APP 全逻辑检查

### 6.1 启动流程

```
Program.Main
    ↓
BuildAvaloniaApp
    ↓
CreateServiceProvider (DI 配置)
    ↓
App.OnFrameworkInitializationCompleted
    ↓
InitializeDatabase (EnsureCreated + Seed)
    ↓
MainWindow 实例化
    ↓
MainWindowViewModel.InitializeAsync (恢复登录状态)
```

**状态**: ✅ 流程正确

### 6.2 导航逻辑

- Sidebar 切换: `MainWindowViewModel.Navigate`
- 页面缓存: ViewModel 单例生命周期
- 数据加载: 页面切换时触发 `LoadViewDataAsync`

**状态**: ✅ 逻辑正确

### 6.3 数据流

```
GitHub API
    ↓ (Octokit)
GitHubApiClient
    ↓ (RepositoryService)
Database (SQLite)
    ↓ (ViewModel)
UI (Avalonia)
```

**状态**: ✅ 单向数据流清晰

---

## 七、测试覆盖情况

| 模块 | 测试数量 | 覆盖率 | 状态 |
|------|---------|--------|------|
| Core | 23 | - | ✅ 基础测试 |
| Data | 13 | - | ✅ 仓储测试 |
| GitHub | 20 | - | ✅ 服务测试 |
| Recommendation | 7 | - | ✅ 算法测试 |
| Desktop | **0** | - | ❌ **无 UI 测试** |

**总计**: 63 个测试，Desktop 层无测试

---

## 八、建议优先级

### P0 (立即修复)
1. 移除 UserEntity.AccessToken 字段，统一使用 SecureStorage

### P1 (近期实现)
2. 实现 Token 刷新机制（存储 RefreshToken）
3. 添加 Token 过期预警

### P2 (后续优化)
4. 实现 UserPreference 持久化
5. 添加 Desktop 层单元测试（使用 Avalonia.Headless）
6. macOS 主密钥迁移到 Keychain

---

**审计结论**: 系统整体架构合理，核心功能完整，数据流向清晰。主要问题是 Token 管理细节有待完善，部分设计文档中的功能（如用户偏好）尚未实现。
