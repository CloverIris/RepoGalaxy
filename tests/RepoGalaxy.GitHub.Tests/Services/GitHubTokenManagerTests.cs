using FluentAssertions;
using NSubstitute;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.GitHub.Services;
using Xunit;

namespace RepoGalaxy.GitHub.Tests.Services;

public class GitHubTokenManagerTests
{
    private readonly ISecureStorage _secureStorage;
    private readonly GitHubTokenManager _tokenManager;

    public GitHubTokenManagerTests()
    {
        _secureStorage = Substitute.For<ISecureStorage>();
        _tokenManager = new GitHubTokenManager(_secureStorage);
    }

    [Fact]
    public async Task SaveTokenAsync_ValidToken_ReturnsTrue()
    {
        // Arrange
        var token = "ghp_test_token_123";
        _secureStorage.SetAsync("github_credential_envelope_v3", Arg.Any<string>()).Returns(true);

        // Act
        var result = await _tokenManager.SaveTokenAsync(token, DateTimeOffset.Now.AddHours(8));

        // Assert
        result.Should().BeTrue();
        await _secureStorage.Received(1).SetAsync(
            "github_credential_envelope_v3",
            Arg.Is<string>(value => value.Contains(token, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetTokenAsync_ExistingToken_ReturnsToken()
    {
        // Arrange
        var expectedToken = "ghp_existing_token";
        _secureStorage.GetAsync("github_access_token").Returns(expectedToken);

        // Act
        var result = await _tokenManager.GetTokenAsync();

        // Assert
        result.Should().Be(expectedToken);
    }

    [Fact]
    public async Task GetTokenAsync_NoToken_ReturnsNull()
    {
        // Arrange
        _secureStorage.GetAsync("github_access_token").Returns((string?)null);

        // Act
        var result = await _tokenManager.GetTokenAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HasTokenAsync_WithToken_ReturnsTrue()
    {
        // Arrange
        _secureStorage.GetAsync("github_access_token").Returns("ghp_token");

        // Act
        var result = await _tokenManager.HasTokenAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasTokenAsync_WithoutToken_ReturnsFalse()
    {
        // Arrange
        _secureStorage.GetAsync("github_access_token").Returns((string?)null);

        // Act
        var result = await _tokenManager.HasTokenAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, true)]   // 已过期1小时
    [InlineData(0, true)]    // 刚好过期
    [InlineData(1, false)]   // 还有1小时
    [InlineData(10, false)]  // 还有10小时
    public async Task IsTokenExpiredAsync_VariousExpiry_ReturnsExpected(int hoursFromNow, bool expectedExpired)
    {
        // Arrange
        var expiry = DateTimeOffset.Now.AddHours(hoursFromNow);
        _secureStorage.GetAsync("github_token_expires_at").Returns(expiry.ToString("O"));

        // Act
        var result = await _tokenManager.IsTokenExpiredAsync();

        // Assert
        result.Should().Be(expectedExpired);
    }

    [Fact]
    public async Task IsTokenExpiredAsync_NoExpiry_ReturnsFalse()
    {
        // Arrange
        _secureStorage.GetAsync("github_token_expires_at").Returns((string?)null);

        // Act
        var result = await _tokenManager.IsTokenExpiredAsync();

        // Assert
        result.Should().BeFalse(); // 无过期时间视为长期有效
    }

    [Fact]
    public async Task ClearTokenAsync_ClearsAllTokens()
    {
        // Arrange
        // Act
        var result = await _tokenManager.ClearTokenAsync();

        // Assert
        result.Should().BeTrue();
        await _secureStorage.Received(1).RemoveAsync("github_credential_envelope_v3");
        await _secureStorage.Received(1).RemoveAsync("github_access_token");
        await _secureStorage.Received(1).RemoveAsync("github_token_expires_at");
        await _secureStorage.Received(1).RemoveAsync("github_refresh_token");
    }

    [Fact]
    public async Task GetTokenExpiryAsync_ValidDate_ReturnsDate()
    {
        // Arrange
        var expectedDate = DateTimeOffset.Now.AddHours(8);
        _secureStorage.GetAsync("github_token_expires_at").Returns(expectedDate.ToString("O"));

        // Act
        var result = await _tokenManager.GetTokenExpiryAsync();

        // Assert
        result.Should().BeCloseTo(expectedDate, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetTokenExpiryAsync_InvalidDate_ReturnsNull()
    {
        // Arrange
        _secureStorage.GetAsync("github_token_expires_at").Returns("invalid_date");

        // Act
        var result = await _tokenManager.GetTokenExpiryAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IsTokenExpiredAsync_WithBuffer_RespectsBuffer()
    {
        // Arrange - 还有4分钟过期（小于5分钟缓冲）
        var expiry = DateTimeOffset.Now.AddMinutes(4);
        _secureStorage.GetAsync("github_token_expires_at").Returns(expiry.ToString("O"));

        // Act - 使用5分钟缓冲
        var result = await _tokenManager.IsTokenExpiredAsync(TimeSpan.FromMinutes(5));

        // Assert - 4分钟 < 5分钟缓冲，应视为过期
        result.Should().BeTrue();
    }
}
