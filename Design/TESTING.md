# RepoGalaxy 测试策略文档

> 版本: v1.0
> 状态: 已批准
> 最后更新: 2026-03-13

---

## 1. 测试哲学

> **"没有单元测试的代码是半成品，没有集成测试的系统是定时炸弹。"**

### 1.1 测试金字塔

```
        /\
       /  \     E2E 测试 (少量)
      /----\    
     /      \   集成测试 (中等)
    /--------\  
   /          \ 单元测试 (大量)
  /------------\
```

| 层级 | 比例 | 关注点 | 速度 |
|------|------|--------|------|
| 单元测试 | 70% | 函数/类逻辑 | < 10ms |
| 集成测试 | 20% | 组件协作 | < 100ms |
| E2E 测试 | 10% | 用户场景 | < 1s |

### 1.2 测试原则

1. **FIRST 原则**
   - **F**ast: 测试必须快速
   - **I**ndependent: 测试相互独立
   - **R**epeatable: 可重复执行
   - **S**elf-validating: 自动验证结果
   - **T**imely: 及时编写（先写测试）

2. **AAA 模式**
   - **A**rrange: 准备测试数据
   - **A**ct: 执行被测操作
   - **A**ssert: 验证结果

---

## 2. 单元测试规范

### 2.1 测试框架

- **主框架**: xUnit
- **Mock 框架**: Moq
- **断言库**: FluentAssertions
- **数据测试**: AutoFixture

### 2.2 项目结构

```
tests/
├── RepoGalaxy.Core.Tests/
│   ├── Models/
│   │   └── RepositoryTests.cs
│   └── Services/
│       └── DiscoveryScoreCalculatorTests.cs
│
├── RepoGalaxy.Data.Tests/
│   ├── Repositories/
│   │   └── RepositoryRepositoryTests.cs
│   ├── Services/
│   │   └── RepositoryServiceTests.cs
│   └── DbContexts/
│       └── RepoGalaxyDbContextTests.cs
│
├── RepoGalaxy.GitHub.Tests/
│   ├── Clients/
│   │   └── GitHubApiClientTests.cs
│   └── Auth/
│       └── GitHubAuthServiceTests.cs
│
└── RepoGalaxy.Recommendation.Tests/
    ├── Engine/
    │   └── RecommendationEngineTests.cs
    ├── Features/
    │   └── FeatureExtractorTests.cs
    └── Scoring/
        └── ScoreCalculatorTests.cs
```

### 2.3 测试类模板

```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace RepoGalaxy.Core.Tests.Models;

public class RepositoryTests
{
    private readonly Fixture _fixture;
    
    public RepositoryTests()
    {
        _fixture = new Fixture();
    }
    
    [Fact]
    public void CalculateSize_LessThan10Stars_ReturnsDust()
    {
        // Arrange
        var repo = new Repository { Stars = 5 };
        
        // Act
        repo.CalculateSize();
        
        // Assert
        repo.Size.Should().Be(MeteoriteSize.Dust);
    }
    
    [Theory]
    [InlineData(5, MeteoriteSize.Dust)]
    [InlineData(50, MeteoriteSize.Pebble)]
    [InlineData(500, MeteoriteSize.Rock)]
    [InlineData(5000, MeteoriteSize.Boulder)]
    [InlineData(50000, MeteoriteSize.Asteroid)]
    [InlineData(500000, MeteoriteSize.Moon)]
    public void CalculateSize_VariousStars_ReturnsCorrectSize(int stars, MeteoriteSize expected)
    {
        // Arrange
        var repo = new Repository { Stars = stars };
        
        // Act
        repo.CalculateSize();
        
        // Assert
        repo.Size.Should().Be(expected);
    }
}
```

### 2.4 Mock 使用规范

```csharp
// 正确：Mock 依赖接口
[Fact]
public async Task GetRecommendationsAsync_WithValidProfile_ReturnsRepositories()
{
    // Arrange
    var mockRepoService = new Mock<IRepositoryService>();
    var mockUserService = new Mock<IUserService>();
    
    mockRepoService.Setup(x => x.GetCachedAsync(It.IsAny<TimeSpan>()))
        .ReturnsAsync(new List<Repository> { /* test data */ });
    
    var engine = new RecommendationEngine(mockRepoService.Object, mockUserService.Object);
    
    // Act
    var result = await engine.GetRecommendationsAsync();
    
    // Assert
    result.Should().NotBeEmpty();
    mockRepoService.Verify(x => x.GetCachedAsync(It.IsAny<TimeSpan>()), Times.Once);
}

// 错误：不要 Mock 被测类本身
// ❌ var mockEngine = new Mock<RecommendationEngine>();
```

### 2.5 数据库测试

使用 SQLite In-Memory 进行集成测试：

```csharp
public class RepositoryRepositoryTests : IDisposable
{
    private readonly RepoGalaxyDbContext _context;
    private readonly RepositoryRepository _repository;
    
    public RepositoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        
        _context = new RepoGalaxyDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        
        _repository = new RepositoryRepository(_context);
    }
    
    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
    
    [Fact]
    public async Task AddOrUpdateAsync_NewRepository_AddsToDatabase()
    {
        // Arrange
        var entity = new RepositoryEntity
        {
            Owner = "test",
            Name = "repo",
            Stars = 100
        };
        
        // Act
        var result = await _repository.AddOrUpdateAsync(entity);
        
        // Assert
        var saved = await _context.Repositories.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved.Owner.Should().Be("test");
    }
}
```

---

## 3. 测试覆盖率要求

### 3.1 覆盖率标准

| 模块 | 行覆盖率 | 分支覆盖率 | 方法覆盖率 |
|------|---------|-----------|-----------|
| Core.Models | 95% | 90% | 100% |
| Core.Interfaces | 100% | 100% | 100% |
| Data.Services | 85% | 80% | 90% |
| Data.Repositories | 90% | 85% | 95% |
| GitHub.Clients | 70% | 65% | 80% |
| GitHub.Auth | 75% | 70% | 85% |
| Recommendation.Engine | 80% | 75% | 85% |
| Recommendation.Scoring | 90% | 85% | 95% |

### 3.2 覆盖率排除

```csharp
// 排除不可测试代码
[ExcludeFromCodeCoverage]
public class Program { }

[ExcludeFromCodeCoverage]
public void DebugLog(string message) { }
```

---

## 4. 集成测试策略

### 4.1 集成测试范围

**必须测试的集成点:**
1. Database + EF Core
2. GitHub API Client + HttpClient
3. Recommendation Engine + Repository Service
4. UI ViewModel + Service

### 4.2 GitHub API 测试

使用 Mock HttpClient 测试 API 客户端：

```csharp
[Fact]
public async Task GetRepositoryAsync_ValidRepo_ReturnsRepository()
{
    // Arrange
    var handler = new MockHttpMessageHandler();
    handler.When("https://api.github.com/repos/test/repo")
           .Respond("application/json", /* json response */);
    
    var httpClient = new HttpClient(handler);
    var client = new GitHubApiClient(httpClient);
    client.SetAccessToken("fake-token");
    
    // Act
    var result = await client.GetRepositoryAsync("test", "repo");
    
    // Assert
    result.Should().NotBeNull();
    result.Owner.Should().Be("test");
}
```

---

## 5. 性能测试

### 5.1 基准测试

使用 BenchmarkDotNet：

```csharp
[MemoryDiagnoser]
public class RecommendationEngineBenchmarks
{
    private RecommendationEngine _engine;
    private UserProfile _profile;
    
    [GlobalSetup]
    public void Setup()
    {
        // 初始化测试数据
    }
    
    [Benchmark]
    public async Task GetRecommendations_100Candidates()
    {
        await _engine.GetRecommendationsAsync(20);
    }
    
    [Benchmark]
    public double CalculateSimilarity()
    {
        return RecommendationEngine.CalculateSimilarity(_repo1, _repo2);
    }
}
```

### 5.2 性能基准

| 操作 | 目标时间 | 内存分配 |
|------|---------|---------|
| 计算相似度 | < 1ms | < 1KB |
| 生成推荐 (100候选) | < 50ms | < 10KB |
| 数据库查询 (单表) | < 10ms | < 5KB |
| API 调用 | < 500ms | - |

---

## 6. 持续集成

### 6.1 CI 流程

```yaml
# .github/workflows/test.yml
name: Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Test
        run: dotnet test --no-build --verbosity normal
      
      - name: Coverage
        run: |
          dotnet test /p:CollectCoverage=true \
                     /p:CoverletOutputFormat=opencover \
                     /p:Threshold=80
```

### 6.2 质量门禁

- 所有测试必须通过
- 代码覆盖率 >= 80%
- 无编译警告
- 静态分析通过 (SonarQube)

---

## 7. 测试数据管理

### 7.1 测试数据工厂

```csharp
public static class TestDataFactory
{
    public static Repository CreateRepository(
        string owner = "test",
        string name = "repo",
        int stars = 100)
    {
        return new Repository
        {
            Owner = owner,
            Name = name,
            Stars = stars,
            Forks = stars / 10,
            CreatedAt = DateTimeOffset.Now.AddYears(-1),
            UpdatedAt = DateTimeOffset.Now.AddDays(-7),
            PrimaryLanguage = "C#",
            Topics = new List<string> { "dotnet", "testing" }
        };
    }
    
    public static List<Repository> CreateRepositoryList(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateRepository($"owner{i}", $"repo{i}", i * 10))
            .ToList();
    }
}
```

### 7.2 种子数据

```csharp
// 测试数据库种子
public static class TestSeedData
{
    public static void Seed(RepoGalaxyDbContext context)
    {
        context.Repositories.AddRange(
            new RepositoryEntity { Owner = "microsoft", Name = "vscode", Stars = 150000 },
            new RepositoryEntity { Owner = "rust-lang", Name = "rust", Stars = 90000 },
            new RepositoryEntity { Owner = "golang", Name = "go", Stars = 120000 }
        );
        
        context.SaveChanges();
    }
}
```

---

## 8. 调试技巧

### 8.1 测试调试

```bash
# 运行指定测试
dotnet test --filter "FullyQualifiedName~RepositoryTests"

# 详细输出
dotnet test --verbosity detailed

# 调试模式
dotnet test --filter "TestName" --logger "console;verbosity=detailed"
```

### 8.2 日志输出

```csharp
// 测试中捕获日志
public class TestLogger<T> : ILogger<T>
{
    public List<string> Logs { get; } = new();
    
    public void Log<TState>(LogLevel level, EventId id, TState state, 
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add(formatter(state, exception));
    }
}
```

---

## 9. 测试清单

### 新增功能时

- [ ] 单元测试覆盖所有分支
- [ ] 边界条件测试
- [ ] 异常路径测试
- [ ] 集成测试（如有外部依赖）
- [ ] 性能测试（关键路径）

### 修复 Bug 时

- [ ] 先写重现测试（应该失败）
- [ ] 修复代码
- [ ] 测试通过
- [ ] 回归测试确保不引入新 Bug

---

## 10. 参考资源

- [xUnit 文档](https://xunit.net/)
- [Moq 文档](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [Microsoft Testing Guidelines](https://docs.microsoft.com/en-us/dotnet/core/testing/)

---

**记住**: 测试是产品质量的保险，不是负担。好的测试让重构变得安全，让协作变得顺畅。