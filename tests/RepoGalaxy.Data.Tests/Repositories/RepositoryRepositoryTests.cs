using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Repositories;
using Xunit;

namespace RepoGalaxy.Data.Tests.Repositories;

public class RepositoryRepositoryTests : IDisposable
{
    private readonly RepoGalaxyDbContext _context;
    private readonly RepositoryRepository _repository;
    private readonly SqliteConnection _connection;

    public RepositoryRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new RepoGalaxyDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new RepositoryRepository(new FactoryAdapter(options));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FactoryAdapter(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext>
    {
        public RepoGalaxyDbContext CreateDbContext() => new(options);
    }

    private RepositoryEntity CreateTestEntity(string owner, string name, int stars = 100)
    {
        return new RepositoryEntity
        {
            GitHubId = $"{owner}_{name}",
            Owner = owner,
            Name = name,
            Description = $"Test repo for {name}",
            Stars = stars,
            Forks = stars / 10,
            Watchers = stars,
            OpenIssues = 5,
            CreatedAt = DateTimeOffset.Now.AddYears(-1),
            UpdatedAt = DateTimeOffset.Now.AddDays(-7),
            CachedAt = DateTimeOffset.Now
        };
    }

    [Fact]
    public async Task AddOrUpdateAsync_NewRepository_AddsToDatabase()
    {
        // Arrange
        var entity = CreateTestEntity("test", "new-repo");

        // Act
        var result = await _repository.AddOrUpdateAsync(entity);

        // Assert
        result.Id.Should().BeGreaterThan(0);
        var saved = await _context.Repositories.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.Owner.Should().Be("test");
        saved.Name.Should().Be("new-repo");
    }

    [Fact]
    public async Task AddOrUpdateAsync_ExistingRepository_UpdatesFields()
    {
        // Arrange
        var entity = CreateTestEntity("test", "existing-repo", 100);
        var added = await _repository.AddOrUpdateAsync(entity);
        
        // Update the entity
        added.Stars = 200;
        added.Description = "Updated description";

        // Act
        var result = await _repository.AddOrUpdateAsync(added);

        // Assert
        result.Stars.Should().Be(200);
        result.Description.Should().Be("Updated description");
        
        var saved = await _context.Repositories.FindAsync(result.Id);
        saved!.Stars.Should().Be(200);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsRepository()
    {
        // Arrange
        var entity = CreateTestEntity("test", "get-by-id");
        var added = await _repository.AddOrUpdateAsync(entity);

        // Act
        var result = await _repository.GetByIdAsync(added.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Owner.Should().Be("test");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByFullNameAsync_ExistingRepo_ReturnsRepository()
    {
        // Arrange
        var entity = CreateTestEntity("microsoft", "vscode");
        await _repository.AddOrUpdateAsync(entity);

        // Act
        var result = await _repository.GetByFullNameAsync("microsoft", "vscode");

        // Assert
        result.Should().NotBeNull();
        result!.Owner.Should().Be("microsoft");
        result.Name.Should().Be("vscode");
    }

    [Fact]
    public async Task GetByFullNameAsync_NonExistingRepo_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByFullNameAsync("non", "existing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BookmarkAsync_Repository_AddsBookmark()
    {
        // Arrange
        var entity = CreateTestEntity("test", "bookmark-test");
        var added = await _repository.AddOrUpdateAsync(entity);

        // Act
        var result = await _repository.BookmarkAsync(added.Id, "My Collection");

        // Assert
        result.Should().BeTrue();
        
        var saved = await _context.Repositories.FindAsync(added.Id);
        saved!.IsBookmarked.Should().BeTrue();
        
        var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.RepositoryId == added.Id);
        bookmark.Should().NotBeNull();
        bookmark!.CollectionName.Should().Be("My Collection");
    }

    [Fact]
    public async Task UnbookmarkAsync_BookmarkedRepository_RemovesBookmark()
    {
        // Arrange
        var entity = CreateTestEntity("test", "unbookmark-test");
        var added = await _repository.AddOrUpdateAsync(entity);
        await _repository.BookmarkAsync(added.Id);

        // Act
        var result = await _repository.UnbookmarkAsync(added.Id);

        // Assert
        result.Should().BeTrue();
        
        var saved = await _context.Repositories.FindAsync(added.Id);
        saved!.IsBookmarked.Should().BeFalse();
    }

    [Fact]
    public async Task GetBookmarksAsync_WithBookmarks_ReturnsOnlyBookmarks()
    {
        // Arrange
        var repo1 = CreateTestEntity("test", "bookmarked");
        var repo2 = CreateTestEntity("test", "not-bookmarked");
        var added1 = await _repository.AddOrUpdateAsync(repo1);
        var added2 = await _repository.AddOrUpdateAsync(repo2);
        await _repository.BookmarkAsync(added1.Id);

        // Act
        var results = await _repository.GetBookmarksAsync();

        // Assert
        results.Should().HaveCount(1);
        results.First().Id.Should().Be(added1.Id);
    }

    [Fact]
    public async Task SearchAsync_WithMatchingKeyword_ReturnsResults()
    {
        // Arrange
        var repo1 = CreateTestEntity("microsoft", "vscode");
        repo1.Description = "Code editor";
        var repo2 = CreateTestEntity("jetbrains", "idea");
        repo2.Description = "Java IDE";
        await _repository.AddOrUpdateAsync(repo1);
        await _repository.AddOrUpdateAsync(repo2);

        // Act
        var results = await _repository.SearchAsync("code");

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("vscode");
    }

    [Fact]
    public async Task RecordViewAsync_Repository_IncrementsViewCount()
    {
        // Arrange
        var entity = CreateTestEntity("test", "view-test");
        var added = await _repository.AddOrUpdateAsync(entity);

        // Act
        await _repository.RecordViewAsync(added.Id, 0, TimeSpan.FromSeconds(30));

        // Assert
        var saved = await _context.Repositories.FindAsync(added.Id);
        saved!.ViewCount.Should().Be(1);
        saved.LastViewedAt.Should().NotBeNull();
        
        var history = await _context.ViewHistories.FirstOrDefaultAsync(v => v.RepositoryId == added.Id);
        history.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCachedAsync_WithRecentCaches_ReturnsCached()
    {
        // Arrange
        var recent = CreateTestEntity("test", "recent");
        var old = CreateTestEntity("test", "old");
        
        await _repository.AddOrUpdateAsync(recent);
        await _repository.AddOrUpdateAsync(old);
        
        // 手动更新 CachedAt（绕过 Repository 的自动更新）
        (await _context.Repositories.SingleAsync(x => x.Name == "recent")).CachedAt = DateTimeOffset.Now.AddHours(-1);
        (await _context.Repositories.SingleAsync(x => x.Name == "old")).CachedAt = DateTimeOffset.Now.AddDays(-10);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetCachedAsync(TimeSpan.FromDays(1));

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("recent");
    }

    [Fact]
    public async Task ClearOldCacheAsync_OldRecords_RemovesThem()
    {
        // Arrange
        var old = CreateTestEntity("test", "to-delete");
        old.IsBookmarked = false;
        await _repository.AddOrUpdateAsync(old);
        
        var bookmarked = CreateTestEntity("test", "bookmarked");
        bookmarked.IsBookmarked = true;
        await _repository.AddOrUpdateAsync(bookmarked);
        
        // 手动更新 CachedAt（绕过 Repository 的自动更新）
        (await _context.Repositories.SingleAsync(x => x.Name == "to-delete")).CachedAt = DateTimeOffset.Now.AddDays(-10);
        (await _context.Repositories.SingleAsync(x => x.Name == "bookmarked")).CachedAt = DateTimeOffset.Now.AddDays(-10);
        await _context.SaveChangesAsync();

        // Act
        var deleted = await _repository.ClearOldCacheAsync(TimeSpan.FromDays(7));

        // Assert
        deleted.Should().Be(1);
        
        var remaining = await _context.Repositories.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.First().Name.Should().Be("bookmarked");
    }
}
