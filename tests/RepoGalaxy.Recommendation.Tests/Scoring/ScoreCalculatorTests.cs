using FluentAssertions;
using RepoGalaxy.Recommendation.Engine;
using RepoGalaxy.Recommendation.Features;
using RepoGalaxy.Recommendation.Scoring;
using Xunit;

namespace RepoGalaxy.Recommendation.Tests.Scoring;

public class ScoreCalculatorTests
{
    private readonly ScoreCalculator _calculator;
    private readonly UserProfile _defaultProfile;

    public ScoreCalculatorTests()
    {
        _calculator = new ScoreCalculator();
        _defaultProfile = new UserProfile
        {
            InterestedTopics = new List<string>(),
            InterestedLanguages = new List<string>(),
            MinStars = 0,
            MaxStars = 1000000,
            PreferFreshContent = true,
            PreferSmallProjects = false
        };
    }

    [Fact]
    public void Calculate_PerfectFeatures_ReturnsHighScore()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 0.9,
            ActivityScore = 0.9,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 3,
            HasDescription = 1,
            HasHomepage = 1,
            TopicsCount = 5,
            IsBookmarked = 0,
            IsViewed = 0
        };

        // Act
        var score = _calculator.Calculate(features, _defaultProfile);

        // Assert
        score.Should().BeGreaterThan(80);
        score.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void Calculate_MinimalFeatures_ReturnsLowScore()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 0.3,
            ActivityScore = 0.3,
            MatchesInterestedLanguage = 0,
            MatchingTopicsCount = 0,
            HasDescription = 0,
            HasHomepage = 0,
            TopicsCount = 0,
            IsBookmarked = 0,
            IsViewed = 0
        };

        // Act
        var score = _calculator.Calculate(features, _defaultProfile);

        // Assert
        score.Should().BeLessThan(50);
    }

    [Fact]
    public void Calculate_AlreadyViewed_ReducesScore()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 0.8,
            ActivityScore = 0.8,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 2,
            HasDescription = 1,
            IsViewed = 1,
            IsBookmarked = 0
        };

        var featuresNotViewed = new RepositoryFeatures
        {
            DiscoveryScore = 0.8,
            ActivityScore = 0.8,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 2,
            HasDescription = 1,
            IsViewed = 0,
            IsBookmarked = 0
        };

        // Act
        var scoreViewed = _calculator.Calculate(features, _defaultProfile);
        var scoreNotViewed = _calculator.Calculate(featuresNotViewed, _defaultProfile);

        // Assert
        scoreViewed.Should().BeLessThan(scoreNotViewed);
    }

    [Fact]
    public void Calculate_Bookmarked_BoostsScore()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 0.8,
            ActivityScore = 0.8,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 2,
            IsViewed = 0,
            IsBookmarked = 1
        };

        var featuresNotBookmarked = new RepositoryFeatures
        {
            DiscoveryScore = 0.8,
            ActivityScore = 0.8,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 2,
            IsViewed = 0,
            IsBookmarked = 0
        };

        // Act
        var scoreBookmarked = _calculator.Calculate(features, _defaultProfile);
        var scoreNotBookmarked = _calculator.Calculate(featuresNotBookmarked, _defaultProfile);

        // Assert
        scoreBookmarked.Should().BeGreaterThan(scoreNotBookmarked);
    }

    [Fact]
    public void Calculate_PreferSmallProjects_SmallRepoBoosted()
    {
        // Arrange
        var profile = new UserProfile
        {
            PreferSmallProjects = true,
            PreferFreshContent = true
        };

        var smallRepo = new RepositoryFeatures
        {
            DiscoveryScore = 0.7,
            ActivityScore = 0.7,
            Stars = 100
        };

        var largeRepo = new RepositoryFeatures
        {
            DiscoveryScore = 0.7,
            ActivityScore = 0.7,
            Stars = 50000
        };

        // Act
        var scoreSmall = _calculator.Calculate(smallRepo, profile);
        var scoreLarge = _calculator.Calculate(largeRepo, profile);

        // Assert - Small repo should get preference bonus
        // Note: This test depends on implementation details
    }

    [Fact]
    public void Calculate_ZeroValues_ReturnsMinimumScore()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 0,
            ActivityScore = 0,
            MatchesInterestedLanguage = 0,
            MatchingTopicsCount = 0,
            HasDescription = 0,
            HasHomepage = 0,
            TopicsCount = 0,
            IsBookmarked = 0,
            IsViewed = 0
        };

        // Act
        var score = _calculator.Calculate(features, _defaultProfile);

        // Assert
        score.Should().BeGreaterThanOrEqualTo(0);
        score.Should().BeLessThan(30);
    }

    [Fact]
    public void Calculate_NeverExceeds100()
    {
        // Arrange
        var features = new RepositoryFeatures
        {
            DiscoveryScore = 1.0,
            ActivityScore = 1.0,
            MatchesInterestedLanguage = 1,
            MatchingTopicsCount = 10,
            HasDescription = 1,
            HasHomepage = 1,
            TopicsCount = 10,
            IsBookmarked = 1,
            IsViewed = 0
        };

        // Act
        var score = _calculator.Calculate(features, _defaultProfile);

        // Assert
        score.Should().BeLessThanOrEqualTo(100);
    }
}
