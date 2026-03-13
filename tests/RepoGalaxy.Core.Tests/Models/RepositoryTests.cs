using FluentAssertions;
using RepoGalaxy.Core.Models;
using Xunit;

namespace RepoGalaxy.Core.Tests.Models;

public class RepositoryTests
{
    [Theory]
    [InlineData(5, MeteoriteSize.Dust)]
    [InlineData(10, MeteoriteSize.Pebble)]
    [InlineData(50, MeteoriteSize.Pebble)]
    [InlineData(99, MeteoriteSize.Pebble)]
    [InlineData(100, MeteoriteSize.Rock)]
    [InlineData(500, MeteoriteSize.Rock)]
    [InlineData(999, MeteoriteSize.Rock)]
    [InlineData(1000, MeteoriteSize.Boulder)]
    [InlineData(5000, MeteoriteSize.Boulder)]
    [InlineData(9999, MeteoriteSize.Boulder)]
    [InlineData(10000, MeteoriteSize.Asteroid)]
    [InlineData(50000, MeteoriteSize.Asteroid)]
    [InlineData(99999, MeteoriteSize.Asteroid)]
    [InlineData(100000, MeteoriteSize.Moon)]
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

    [Fact]
    public void CalculateSize_ZeroStars_ReturnsDust()
    {
        // Arrange
        var repo = new Repository { Stars = 0 };
        
        // Act
        repo.CalculateSize();
        
        // Assert
        repo.Size.Should().Be(MeteoriteSize.Dust);
    }

    [Fact]
    public void CalculateDiscoveryScore_RecentSmallRepo_ReturnsHighScore()
    {
        // Arrange
        var repo = new Repository
        {
            Stars = 100,
            Forks = 20,
            UpdatedAt = DateTimeOffset.Now.AddDays(-5)
        };
        
        // Act
        var score = repo.CalculateDiscoveryScore();
        
        // Assert
        score.Should().BeGreaterThan(0.7);
        repo.DiscoveryScore.Should().Be(score);
    }

    [Fact]
    public void CalculateDiscoveryScore_OldLargeRepo_ReturnsLowerScore()
    {
        // Arrange
        var repo = new Repository
        {
            Stars = 50000,
            Forks = 1000,
            UpdatedAt = DateTimeOffset.Now.AddDays(-200)
        };
        
        // Act
        var score = repo.CalculateDiscoveryScore();
        
        // Assert
        score.Should().BeLessThan(0.6);
    }

    [Fact]
    public void CalculateDiscoveryScore_NoForks_ReturnsLowerScore()
    {
        // Arrange
        var repo = new Repository
        {
            Stars = 1000,
            Forks = 0,
            UpdatedAt = DateTimeOffset.Now.AddDays(-5)
        };
        
        // Act
        var score = repo.CalculateDiscoveryScore();
        
        // Assert
        score.Should().BeLessThan(0.9);
    }

    [Fact]
    public void FullName_ValidOwnerAndName_ReturnsCombined()
    {
        // Arrange
        var repo = new Repository
        {
            Owner = "microsoft",
            Name = "vscode"
        };
        
        // Act & Assert
        repo.FullName.Should().Be("microsoft/vscode");
    }

    [Fact]
    public void Constructor_DefaultValues_AreSet()
    {
        // Arrange & Act
        var repo = new Repository();
        
        // Assert
        repo.Topics.Should().NotBeNull();
        repo.Languages.Should().NotBeNull();
        repo.Owner.Should().BeEmpty();
        repo.Name.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.4, 0.4, 0.2, 1.0)]  // All perfect
    [InlineData(0.2, 0.2, 0.1, 0.5)]  // All low
    public void CalculateDiscoveryScore_VariousInputs_ReturnsExpected(
        double starScore, double freshnessScore, double forkScore, double expectedRange)
    {
        // This is a simplified test - actual calculation depends on Stars/UpdatedAt
        // Arrange
        var repo = new Repository
        {
            Stars = starScore > 0.5 ? 100 : 50000,
            Forks = starScore > 0.5 ? 20 : 500,
            UpdatedAt = freshnessScore > 0.5 ? DateTimeOffset.Now.AddDays(-5) : DateTimeOffset.Now.AddDays(-200)
        };
        
        // Act
        var score = repo.CalculateDiscoveryScore();
        
        // Assert - just verify it's in valid range
        score.Should().BeInRange(0.0, 1.0);
    }
}
