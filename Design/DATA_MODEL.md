# RepoGalaxy 数据模型设计

> 状态：规划中 | 最后更新：2026-03-13

---

## 1. 核心领域模型 (Core Layer)

### 1.1 仓库 (Repository / RepoMeteorite)

```csharp
/// <summary>
/// 仓库实体 - Feed 流中的"陨石"
/// </summary>
public class Repository
{
    // GitHub 基础信息
    public long Id { get; set; }
    public string GitHubId { get; set; }           // GitHub 返回的 node_id
    public string Owner { get; set; }              // 用户名/组织名
    public string Name { get; set; }               // 仓库名
    public string FullName => $"{Owner}/{Name}";
    
    // 内容信息
    public string Description { get; set; }
    public string PrimaryLanguage { get; set; }    // 主要编程语言
    public List<string> Topics { get; set; }       // 话题标签
    public string ReadmeContent { get; set; }      // README 内容（缓存）
    public string Homepage { get; set; }           // 项目主页
    
    // 社交指标 (GitHub API 提供)
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Watchers { get; set; }
    public int OpenIssues { get; set; }
    
    // 活跃度计算
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    
    // RepoGalaxy 特有属性
    public double DiscoveryScore { get; set; }     // 发现价值评分
    public MeteoriteSize Size { get; set; }        // 陨石大小分类
    public OrbitCategory Orbit { get; set; }       // 所属星轨分类
    
    // 本地状态
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
}

public enum MeteoriteSize
{
    Dust = 0,           // < 10 stars
    Pebble = 1,         // 10 - 100 stars
    Rock = 2,           // 100 - 1k stars
    Boulder = 3,        // 1k - 10k stars
    Asteroid = 4,       // 10k - 100k stars
    Moon = 5            // > 100k stars
}

public enum OrbitCategory
{
    Core = 0,           // 核心/基础设施
    Web = 1,            // Web 开发
    Mobile = 2,         // 移动开发
    AI = 3,             // 人工智能/机器学习
    Data = 4,           // 数据/数据库
    DevOps = 5,         // 运维/工具
    Design = 6,         // 设计/创意
    Learning = 7,       // 教程/学习
    Experimental = 8    // 实验性/新兴
}
```

### 1.2 开发者 (Developer / StarTraveler)

```csharp
/// <summary>
/// 开发者实体 - "星际旅行者"
/// </summary>
public class Developer
{
    public long Id { get; set; }
    public string GitHubId { get; set; }
    public string Login { get; set; }
    public string AvatarUrl { get; set; }
    public string Bio { get; set; }
    public string Company { get; set; }
    public string Location { get; set; }
    public string Blog { get; set; }
    public string TwitterUsername { get; set; }
    
    // GitHub 统计
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    // RepoGalaxy 码力评估 (多边形维度)
    public CodingPowerStats CodingStats { get; set; }
    
    // 关系
    public List<Repository> Repositories { get; set; }
    public List<ContributionActivity> ContributionHistory { get; set; }
}

/// <summary>
/// 码力统计 - 六边形能力图
/// </summary>
public class CodingPowerStats
{
    public long DeveloperId { get; set; }
    
    // 六边形维度 (0-100)
    public int CodeQuality { get; set; }        // 代码质量
    public int Consistency { get; set; }        // 提交稳定性
    public int Diversity { get; set; }          // 技术多样性
    public int Influence { get; set; }          // 项目影响力
    public int Collaboration { get; set; }      // 协作能力
    public int Documentation { get; set; }      // 文档能力
    
    // 计算时间
    public DateTimeOffset CalculatedAt { get; set; }
    public string CalculationVersion { get; set; }  // 算法版本
}
```

### 1.3 用户行为数据

```csharp
/// <summary>
/// 用户浏览记录
/// </summary>
public class ViewHistory
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
    public TimeSpan Duration { get; set; }      // 停留时长
    public ViewSource Source { get; set; }      // 来源
    public string ReferrerTopic { get; set; }   // 从哪个话题进入
}

public enum ViewSource
{
    Feed = 0,           // Feed 流
    Search = 1,         // 搜索
    Recommendation = 2, // 推荐
    Bookmark = 3,       // 书签
    External = 4        // 外部链接
}

/// <summary>
/// 用户偏好配置
/// </summary>
public class UserPreference
{
    public long Id { get; set; }
    
    // 兴趣标签
    public List<string> InterestedTopics { get; set; }
    public List<string> InterestedLanguages { get; set; }
    
    // 过滤设置
    public int MinStarsThreshold { get; set; }      // 最小 Star 数
    public int MaxStarsThreshold { get; set; }      // 最大 Star 数 (过滤超热门)
    public List<string> IgnoredTopics { get; set; } // 屏蔽话题
    
    // 推荐偏好
    public bool PreferFreshContent { get; set; }    // 偏好新内容
    public bool IncludeTrending { get; set; }       // 包含 Trending
    public bool PreferSmallProjects { get; set; }   // 偏好小而美项目
    
    // UI 偏好
    public bool DarkMode { get; set; }
    public int FeedPageSize { get; set; }
}

/// <summary>
/// 收藏夹
/// </summary>
public class Bookmark
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateTimeOffset BookmarkedAt { get; set; }
    public string CollectionName { get; set; }      // 收藏夹分类
    public string Notes { get; set; }               // 用户备注
    public int Priority { get; set; }               // 优先级排序
}
```

### 1.4 推荐相关模型

```csharp
/// <summary>
/// 推荐候选池
/// </summary>
public class RecommendationCandidate
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public double BaseScore { get; set; }           // 基础分
    public double UserAffinityScore { get; set; }   // 用户偏好分
    public double NoveltyScore { get; set; }        // 新鲜度分
    public double FinalScore { get; set; }          // 最终综合分
    public DateTimeOffset GeneratedAt { get; set; }
    public bool IsConsumed { get; set; }            // 是否已推送给用户
}

/// <summary>
/// 内容过滤规则
/// </summary>
public class FilterRule
{
    public long Id { get; set; }
    public string RuleType { get; set; }            // "keyword", "language", "author"
    public string Pattern { get; set; }             // 匹配模式
    public bool IsRegex { get; set; }
    public FilterAction Action { get; set; }        // Block / Boost
    public int Priority { get; set; }
}

public enum FilterAction
{
    Block = 0,          // 屏蔽
    Mute = 1,           // 降权
    Boost = 2,          // 加权
    Pin = 3             // 置顶
}
```

---

## 2. 数据库实体设计 (Data Layer)

### 2.1 Entity Framework 实体

```csharp
// 数据库实体与领域模型映射
// 使用 Fluent API 配置

public class RepositoryEntity
{
    public long Id { get; set; }
    public string GitHubId { get; set; }
    public string Owner { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string PrimaryLanguage { get; set; }
    public string TopicsJson { get; set; }          // JSON 序列化
    public int Stars { get; set; }
    public int Forks { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    public int OrbitCategoryId { get; set; }
    public double DiscoveryScore { get; set; }
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public DateTimeOffset CachedAt { get; set; }    // 缓存时间戳
}

public class UserActivityEntity
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public string ActivityType { get; set; }        // "view", "bookmark", "ignore"
    public DateTimeOffset Timestamp { get; set; }
    public string MetadataJson { get; set; }        // 额外数据 JSON
}
```

---

## 3. API 数据传输对象 (GitHub Layer)

```csharp
// Octokit 返回的模型直接可用，但我们需要一些自定义 DTO

public class GitHubSearchResult
{
    public int TotalCount { get; set; }
    public bool IncompleteResults { get; set; }
    public List<RepositoryDto> Items { get; set; }
}

public class TrendingFilterOptions
{
    public DateTimeOffset? Since { get; set; }      // 时间范围
    public string Language { get; set; }            // 语言过滤
    public string SpokenLanguage { get; set; }      // 文档语言 (若支持)
}

public class StarVelocityResult
{
    public long RepositoryId { get; set; }
    public int StarsToday { get; set; }
    public int StarsThisWeek { get; set; }
    public int StarsThisMonth { get; set; }
    public double VelocityScore { get; set; }       // 增长速度评分
}
```

---

## 4. 数据流设计

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub API                                  │
└───────────────────┬─────────────────────────────────────────────────┘
                    │ Octokit.net
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   GitHub Layer (DTOs)                               │
│         - Repository DTO → Domain Model 映射                        │
│         - Rate Limit 处理                                           │
└───────────────────┬─────────────────────────────────────────────────┘
                    │ 映射转换
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Core Layer (Domain Models)                       │
│         - 业务逻辑处理                                               │
│         - 推荐算法计算                                               │
└───────────────────┬─────────────────────────────────────────────────┘
                    │ EF Core
                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Data Layer (Entities)                            │
│         - SQLite 持久化                                              │
│         - 查询优化                                                   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. 缓存策略

| 数据类型 | 缓存位置 | 过期时间 | 策略 |
|----------|----------|----------|------|
| 仓库基础信息 | SQLite | 24 小时 | 后台更新，先返回缓存 |
| README 内容 | SQLite | 72 小时 | 懒加载 |
| 用户头像 | 本地文件 | 7 天 | LRU 淘汰 |
| Trending 列表 | 内存 + SQLite | 1 小时 | 定时刷新 |
| 推荐结果 | 内存 | 30 分钟 | 实时计算 + 缓存 |
| 用户配置 | SQLite | 永久 | 即时保存 |

---

## 6. 待完善事项

- [ ] 确定 Repository.Id 是自增还是使用 GitHub 的 NodeId
- [ ] 设计贡献历史的数据结构和存储方式
- [ ] 定义码力统计各维度的具体计算算法
- [ ] 设计用户画像的更新频率和触发条件
