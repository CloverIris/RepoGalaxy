using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.DbContexts;

/// <summary>
/// 数据库种子数据
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(RepoGalaxyDbContext context)
    {
        if (context.Repositories.Any())
            return; // 已有数据，不重复种子

        // 添加示例仓库数据
        var repositories = new[]
        {
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnkx",
                Owner = "microsoft",
                Name = "vscode",
                Description = "Visual Studio Code",
                PrimaryLanguage = "TypeScript",
                TopicsJson = "[\"editor\",\"ide\",\"typescript\"]",
                Stars = 150000,
                Forks = 25000,
                Watchers = 150000,
                OpenIssues = 5000,
                CreatedAt = DateTimeOffset.Parse("2015-09-03T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-2),
                LastPushedAt = DateTimeOffset.Now.AddDays(-1),
                HtmlUrl = "https://github.com/microsoft/vscode",
                OrbitCategoryId = 1, // Web
                DiscoveryScore = 0.5,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"TypeScript\",\"Percentage\":0.85,\"Bytes\":85000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnky",
                Owner = "torvalds",
                Name = "linux",
                Description = "Linux kernel source tree",
                PrimaryLanguage = "C",
                TopicsJson = "[\"kernel\",\"linux\",\"c\"]",
                Stars = 160000,
                Forks = 50000,
                Watchers = 160000,
                OpenIssues = 400,
                CreatedAt = DateTimeOffset.Parse("2011-09-04T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-12),
                HtmlUrl = "https://github.com/torvalds/linux",
                OrbitCategoryId = 0, // Core
                DiscoveryScore = 0.4,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"C\",\"Percentage\":0.98,\"Bytes\":980000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnkz",
                Owner = "rust-lang",
                Name = "rust",
                Description = "Empowering everyone to build reliable and efficient software.",
                PrimaryLanguage = "Rust",
                TopicsJson = "[\"rust\",\"compiler\",\"systems\"]",
                Stars = 90000,
                Forks = 12000,
                Watchers = 90000,
                OpenIssues = 8000,
                CreatedAt = DateTimeOffset.Parse("2010-01-01T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-3),
                LastPushedAt = DateTimeOffset.Now.AddDays(-1),
                HtmlUrl = "https://github.com/rust-lang/rust",
                OrbitCategoryId = 0, // Core
                DiscoveryScore = 0.7,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Rust\",\"Percentage\":0.95,\"Bytes\":95000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk0",
                Owner = "golang",
                Name = "go",
                Description = "The Go programming language",
                PrimaryLanguage = "Go",
                TopicsJson = "[\"go\",\"golang\",\"compiler\"]",
                Stars = 115000,
                Forks = 17000,
                Watchers = 115000,
                OpenIssues = 9000,
                CreatedAt = DateTimeOffset.Parse("2014-08-19T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-6),
                HtmlUrl = "https://github.com/golang/go",
                OrbitCategoryId = 0, // Core
                DiscoveryScore = 0.6,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Go\",\"Percentage\":0.98,\"Bytes\":98000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk1",
                Owner = "facebook",
                Name = "react",
                Description = "A declarative, efficient, and flexible JavaScript library",
                PrimaryLanguage = "JavaScript",
                TopicsJson = "[\"javascript\",\"ui\",\"frontend\"]",
                Stars = 215000,
                Forks = 45000,
                Watchers = 215000,
                OpenIssues = 1200,
                CreatedAt = DateTimeOffset.Parse("2013-05-24T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-3),
                HtmlUrl = "https://github.com/facebook/react",
                OrbitCategoryId = 1, // Web
                DiscoveryScore = 0.4,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"JavaScript\",\"Percentage\":0.90,\"Bytes\":90000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk2",
                Owner = "python",
                Name = "cpython",
                Description = "The Python programming language",
                PrimaryLanguage = "Python",
                TopicsJson = "[\"python\",\"interpreter\",\"language\"]",
                Stars = 55000,
                Forks = 28000,
                Watchers = 55000,
                OpenIssues = 10000,
                CreatedAt = DateTimeOffset.Parse("2017-02-10T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-2),
                HtmlUrl = "https://github.com/python/cpython",
                OrbitCategoryId = 0, // Core
                DiscoveryScore = 0.6,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Python\",\"Percentage\":0.60,\"Bytes\":60000000},{\"Name\":\"C\",\"Percentage\":0.38,\"Bytes\":38000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk3",
                Owner = "jetbrains",
                Name = "kotlin",
                Description = "The Kotlin Programming Language",
                PrimaryLanguage = "Kotlin",
                TopicsJson = "[\"kotlin\",\"jvm\",\"android\"]",
                Stars = 46000,
                Forks = 5600,
                Watchers = 46000,
                OpenIssues = 500,
                CreatedAt = DateTimeOffset.Parse("2012-02-13T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-2),
                LastPushedAt = DateTimeOffset.Now.AddDays(-1),
                HtmlUrl = "https://github.com/JetBrains/kotlin",
                OrbitCategoryId = 2, // Mobile
                DiscoveryScore = 0.75,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Kotlin\",\"Percentage\":0.95,\"Bytes\":95000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk4",
                Owner = "apple",
                Name = "swift",
                Description = "The Swift Programming Language",
                PrimaryLanguage = "Swift",
                TopicsJson = "[\"swift\",\"ios\",\"language\"]",
                Stars = 64000,
                Forks = 10000,
                Watchers = 64000,
                OpenIssues = 6000,
                CreatedAt = DateTimeOffset.Parse("2015-10-23T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-8),
                HtmlUrl = "https://github.com/apple/swift",
                OrbitCategoryId = 2, // Mobile
                DiscoveryScore = 0.65,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Swift\",\"Percentage\":0.92,\"Bytes\":92000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnk5",
                Owner = "vuejs",
                Name = "vue",
                Description = "Vue.js is a progressive JavaScript framework",
                PrimaryLanguage = "TypeScript",
                TopicsJson = "[\"vue\",\"javascript\",\"frontend\"]",
                Stars = 205000,
                Forks = 34000,
                Watchers = 205000,
                OpenIssues = 600,
                CreatedAt = DateTimeOffset.Parse("2013-07-29T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-3),
                LastPushedAt = DateTimeOffset.Now.AddDays(-1),
                HtmlUrl = "https://github.com/vuejs/vue",
                OrbitCategoryId = 1, // Web
                DiscoveryScore = 0.45,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"TypeScript\",\"Percentage\":0.80,\"Bytes\":80000000}]"
            },
            new RepositoryEntity
            {
                GitHubId = "MDEwOlJlcG9zaXRvcnkxMA==",
                Owner = "denoland",
                Name = "deno",
                Description = "A modern runtime for JavaScript and TypeScript",
                PrimaryLanguage = "Rust",
                TopicsJson = "[\"deno\",\"rust\",\"typescript\"]",
                Stars = 90000,
                Forks = 5000,
                Watchers = 90000,
                OpenIssues = 2000,
                CreatedAt = DateTimeOffset.Parse("2018-05-15T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                LastPushedAt = DateTimeOffset.Now.AddHours(-4),
                HtmlUrl = "https://github.com/denoland/deno",
                OrbitCategoryId = 1, // Web
                DiscoveryScore = 0.8,
                CachedAt = DateTimeOffset.Now,
                LanguagesJson = "[{\"Name\":\"Rust\",\"Percentage\":0.85,\"Bytes\":85000000}]"
            }
        };

        context.Repositories.AddRange(repositories);
        context.SaveChanges();
    }
}
