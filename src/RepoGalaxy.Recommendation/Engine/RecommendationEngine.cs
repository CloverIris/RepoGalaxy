using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Recommendation.Features;
using RepoGalaxy.Recommendation.Filters;
using RepoGalaxy.Recommendation.Scoring;
using System.Text.Json;

namespace RepoGalaxy.Recommendation.Engine;

/// <summary>
/// 推荐引擎实现
/// 支持：个性化推荐、相似项目推荐、聚类推荐、协同过滤
/// </summary>
public class RecommendationEngine : IRecommendationEngine
{
    private readonly IRepositoryService _repositoryService;
    private readonly IUserService _userService;
    private readonly RepoGalaxyDbContext _dbContext;
    private readonly FeatureExtractor _featureExtractor;
    private readonly ContentFilter _contentFilter;
    private readonly ScoreCalculator _scoreCalculator;
    
    // 用户画像缓存
    private UserProfile? _userProfile;
    private DateTime _profileLastUpdated = DateTime.MinValue;
    private readonly TimeSpan _profileCacheDuration = TimeSpan.FromMinutes(30);
    
    // 用户反馈缓存
    private readonly Dictionary<long, FeedbackType> _recentFeedback = new();
    private readonly Queue<(long RepoId, DateTime Time)> _feedbackHistory = new();
    private readonly int _maxFeedbackHistory = 100;
    
    public RecommendationEngine(
        IRepositoryService repositoryService,
        IUserService userService,
        RepoGalaxyDbContext dbContext)
    {
        _repositoryService = repositoryService;
        _userService = userService;
        _dbContext = dbContext;
        _featureExtractor = new FeatureExtractor();
        _contentFilter = new ContentFilter();
        _scoreCalculator = new ScoreCalculator();
    }
    
    /// <summary>
    /// 获取个性化推荐 - 多策略混合
    /// </summary>
    public async Task<IEnumerable<Repository>> GetRecommendationsAsync(int count = 20)
    {
        var profile = await GetUserProfileAsync();
        var candidates = await BuildCandidatePoolAsync(profile);
        var filtered = _contentFilter.Apply(candidates, profile).ToList();
        
        var scoredRepos = new List<ScoredRepository>();
        
        foreach (var repo in filtered)
        {
            var features = _featureExtractor.Extract(repo, profile);
            var baseScore = _scoreCalculator.Calculate(features, profile);
            var cfScore = CalculateCollaborativeScore(repo, profile);
            var contentScore = CalculateContentScore(repo, profile);
            var discoveryBonus = repo.DiscoveryScore * 10;
            var feedbackMultiplier = GetFeedbackMultiplier(repo.Id);
            
            var finalScore = (baseScore * 0.4 + cfScore * 0.25 + contentScore * 0.25 + discoveryBonus * 0.1) 
                             * feedbackMultiplier;
            
            scoredRepos.Add(new ScoredRepository(repo, finalScore, features));
        }
        
        var recommendations = ApplyDiversity(scoredRepos, count);
        return recommendations.Select(r => r.Repository);
    }
    
    /// <summary>
    /// 获取相似仓库
    /// </summary>
    public async Task<IEnumerable<Repository>> GetSimilarAsync(long repositoryId, int count = 10)
    {
        var source = await _repositoryService.GetByIdAsync(repositoryId);
        if (source == null) return Enumerable.Empty<Repository>();
        
        var candidates = await FindSimilarCandidatesAsync(source);
        candidates = candidates.Where(r => r.Id != repositoryId).ToList();
        
        var similar = candidates.Select(repo =>
        {
            var similarity = CalculateDetailedSimilarity(source, repo);
            return new { Repository = repo, Similarity = similarity };
        });
        
        return similar
            .OrderByDescending(x => x.Similarity)
            .Take(count)
            .Select(x => x.Repository);
    }
    
    /// <summary>
    /// 基于聚类的推荐
    /// </summary>
    public async Task<IEnumerable<Repository>> GetRelatedRecommendationsAsync(IEnumerable<long> seedIds, int count = 15)
    {
        var seeds = new List<Repository>();
        foreach (var id in seedIds)
        {
            var repo = await _repositoryService.GetByIdAsync(id);
            if (repo != null) seeds.Add(repo);
        }
        
        if (!seeds.Any()) return Enumerable.Empty<Repository>();
        
        var profile = BuildInterestProfile(seeds);
        var candidates = await FindInterestCandidatesAsync(profile, seedIds);
        
        var scored = candidates.Select(repo =>
        {
            var score = CalculateInterestMatchScore(repo, profile);
            return new { Repository = repo, Score = score };
        });
        
        return scored
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Repository);
    }
    
    /// <summary>
    /// 记录用户反馈
    /// </summary>
    public async Task RecordFeedbackAsync(long repositoryId, FeedbackType type)
    {
        _recentFeedback[repositoryId] = type;
        _feedbackHistory.Enqueue((repositoryId, DateTime.Now));
        
        while (_feedbackHistory.Count > _maxFeedbackHistory)
        {
            var old = _feedbackHistory.Dequeue();
            _recentFeedback.Remove(old.RepoId);
        }
        
        await PersistFeedbackAsync(repositoryId, type);
        
        if (type == FeedbackType.Bookmark)
        {
            await UpdateProfileFromBookmarkAsync(repositoryId);
        }
    }
    
    /// <summary>
    /// 更新用户画像
    /// </summary>
    public async Task UpdateUserProfileAsync()
    {
        _userProfile = null;
        _profileLastUpdated = DateTime.MinValue;
        await GetUserProfileAsync();
    }
    
    private async Task<UserProfile> GetUserProfileAsync()
    {
        if (_userProfile != null && DateTime.Now - _profileLastUpdated < _profileCacheDuration)
        {
            return _userProfile;
        }
        
        var preferences = await _userService.GetPreferencesAsync();
        var bookmarks = await _repositoryService.GetBookmarksAsync();
        var history = await _repositoryService.GetCachedAsync(TimeSpan.FromDays(30));
        
        var topicDistribution = await AnalyzeTopicDistributionAsync();
        var languageDistribution = await AnalyzeLanguageDistributionAsync();
        
        _userProfile = new UserProfile
        {
            InterestedTopics = preferences.InterestedTopics.Any() 
                ? preferences.InterestedTopics 
                : topicDistribution.Keys.ToList(),
            InterestedLanguages = preferences.InterestedLanguages.Any()
                ? preferences.InterestedLanguages
                : languageDistribution.Keys.ToList(),
            BookmarkedRepos = bookmarks.ToList(),
            ViewedRepos = history.Take(50).ToList(),
            MinStars = preferences.MinStarsThreshold,
            MaxStars = preferences.MaxStarsThreshold,
            PreferFreshContent = preferences.PreferFreshContent,
            PreferSmallProjects = preferences.PreferSmallProjects,
            TopicWeights = topicDistribution,
            LanguageWeights = languageDistribution
        };
        
        _profileLastUpdated = DateTime.Now;
        return _userProfile;
    }
    
    private async Task<List<Repository>> BuildCandidatePoolAsync(UserProfile profile)
    {
        var candidates = new HashSet<Repository>(new RepositoryComparer());
        
        var cached = await _repositoryService.GetCachedAsync(TimeSpan.FromDays(7));
        foreach (var repo in cached) candidates.Add(repo);
        
        foreach (var lang in profile.InterestedLanguages.Take(3))
        {
            try
            {
                var langRepos = await _repositoryService.SearchAsync($"language:{lang}");
                foreach (var repo in langRepos.Take(20)) candidates.Add(repo);
            }
            catch { }
        }
        
        foreach (var topic in profile.InterestedTopics.Take(5))
        {
            try
            {
                var topicRepos = await _repositoryService.SearchAsync($"topic:{topic}");
                foreach (var repo in topicRepos.Take(15)) candidates.Add(repo);
            }
            catch { }
        }
        
        foreach (var bookmark in profile.BookmarkedRepos.Take(5))
        {
            try
            {
                var similar = await GetSimilarAsync(bookmark.Id, 10);
                foreach (var repo in similar) candidates.Add(repo);
            }
            catch { }
        }
        
        return candidates.ToList();
    }
    
    private double CalculateCollaborativeScore(Repository repo, UserProfile profile)
    {
        var score = 0.0;
        
        foreach (var bookmark in profile.BookmarkedRepos)
        {
            var similarity = CalculateSimilarity(bookmark, repo);
            score += similarity * 20;
        }
        
        foreach (var viewed in profile.ViewedRepos.Take(20))
        {
            var similarity = CalculateSimilarity(viewed, repo);
            score += similarity * 5;
        }
        
        return Math.Min(score, 30);
    }
    
    private double CalculateContentScore(Repository repo, UserProfile profile)
    {
        var score = 0.0;
        
        foreach (var topic in repo.Topics)
        {
            if (profile.TopicWeights.TryGetValue(topic, out var weight))
            {
                score += weight * 3;
            }
        }
        
        if (profile.LanguageWeights.TryGetValue(repo.PrimaryLanguage, out var langWeight))
        {
            score += langWeight * 5;
        }
        
        return Math.Min(score, 25);
    }
    
    private double CalculateDetailedSimilarity(Repository a, Repository b)
    {
        var score = 0.0;
        
        if (a.PrimaryLanguage == b.PrimaryLanguage)
            score += 0.3;
        
        var commonTopics = a.Topics.Intersect(b.Topics, StringComparer.OrdinalIgnoreCase).Count();
        score += Math.Min(commonTopics * 0.1, 0.4);
        
        if (a.Stars > 0 && b.Stars > 0)
        {
            var starRatio = Math.Min(a.Stars, b.Stars) / (double)Math.Max(a.Stars, b.Stars);
            score += starRatio * 0.15;
        }
        
        if (!string.IsNullOrEmpty(a.Description) && !string.IsNullOrEmpty(b.Description))
        {
            var aWords = ExtractKeywords(a.Description);
            var bWords = ExtractKeywords(b.Description);
            var commonWords = aWords.Intersect(bWords).Count();
            score += Math.Min(commonWords * 0.03, 0.15);
        }
        
        return score;
    }
    
    private double CalculateSimilarity(Repository a, Repository b)
    {
        var score = 0.0;
        
        if (a.PrimaryLanguage == b.PrimaryLanguage)
            score += 0.3;
        
        var commonTopics = a.Topics.Intersect(b.Topics).Count();
        score += commonTopics * 0.15;
        
        var starRatio = Math.Min(a.Stars, b.Stars) / (double)Math.Max(a.Stars, b.Stars);
        score += starRatio * 0.2;
        
        return Math.Min(score, 1.0);
    }
    
    private double GetFeedbackMultiplier(long repositoryId)
    {
        if (_recentFeedback.TryGetValue(repositoryId, out var feedback))
        {
            return feedback switch
            {
                FeedbackType.Bookmark => 1.5,
                FeedbackType.View => 1.1,
                FeedbackType.Click => 1.05,
                FeedbackType.Ignore => 0.0,
                FeedbackType.Dismiss => 0.3,
                _ => 1.0
            };
        }
        return 1.0;
    }
    
    private async Task<List<Repository>> FindSimilarCandidatesAsync(Repository source)
    {
        var candidates = new List<Repository>();
        
        if (source.Topics.Any())
        {
            var topicQuery = string.Join(" ", source.Topics.Take(3));
            var topicResults = await _repositoryService.SearchAsync(topicQuery);
            candidates.AddRange(topicResults);
        }
        
        if (!string.IsNullOrEmpty(source.PrimaryLanguage) && source.PrimaryLanguage != "Unknown")
        {
            var langResults = await _repositoryService.SearchAsync($"language:{source.PrimaryLanguage}");
            candidates.AddRange(langResults);
        }
        
        return candidates.Distinct(new RepositoryComparer()).ToList();
    }
    
    private InterestProfile BuildInterestProfile(List<Repository> seeds)
    {
        var profile = new InterestProfile();
        
        profile.CommonTopics = seeds
            .SelectMany(s => s.Topics)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();
        
        profile.CommonLanguages = seeds
            .Select(s => s.PrimaryLanguage)
            .Where(l => !string.IsNullOrEmpty(l) && l != "Unknown")
            .GroupBy(l => l)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();
        
        profile.AvgStars = (int)seeds.Average(s => s.Stars);
        profile.MinStars = Math.Max(0, profile.AvgStars / 10);
        profile.MaxStars = profile.AvgStars * 10;
        
        return profile;
    }
    
    private async Task<List<Repository>> FindInterestCandidatesAsync(InterestProfile profile, IEnumerable<long> excludeIds)
    {
        var candidates = new HashSet<Repository>(new RepositoryComparer());
        var excludeSet = new HashSet<long>(excludeIds);
        
        foreach (var topic in profile.CommonTopics)
        {
            var results = await _repositoryService.SearchAsync($"topic:{topic}");
            foreach (var r in results.Where(r => !excludeSet.Contains(r.Id)).Take(10))
                candidates.Add(r);
        }
        
        foreach (var lang in profile.CommonLanguages)
        {
            var results = await _repositoryService.SearchAsync($"language:{lang}");
            foreach (var r in results.Where(r => !excludeSet.Contains(r.Id)).Take(10))
                candidates.Add(r);
        }
        
        return candidates.ToList();
    }
    
    private double CalculateInterestMatchScore(Repository candidate, InterestProfile profile)
    {
        var score = 0.0;
        
        var matchingTopics = candidate.Topics
            .Intersect(profile.CommonTopics, StringComparer.OrdinalIgnoreCase)
            .Count();
        score += matchingTopics * 5;
        
        if (profile.CommonLanguages.Contains(candidate.PrimaryLanguage))
            score += 10;
        
        if (candidate.Stars >= profile.MinStars && candidate.Stars <= profile.MaxStars)
            score += 5;
        
        return score;
    }
    
    private List<ScoredRepository> ApplyDiversity(List<ScoredRepository> scored, int count)
    {
        var sorted = scored.OrderByDescending(s => s.Score).ToList();
        var selected = new List<ScoredRepository>();
        var usedLanguages = new HashSet<string>();
        var usedTopics = new HashSet<string>();
        
        foreach (var item in sorted)
        {
            if (selected.Count >= count) break;
            
            var repo = item.Repository;
            var lang = repo.PrimaryLanguage;
            var topics = repo.Topics.Take(2).ToList();
            
            var langPenalty = usedLanguages.Count(l => l == lang) * 5;
            var topicPenalty = topics.Count(t => usedTopics.Contains(t)) * 3;
            
            var adjustedScore = item.Score - langPenalty - topicPenalty;
            
            if (adjustedScore > 0 || selected.Count < count / 2)
            {
                selected.Add(item);
                usedLanguages.Add(lang);
                foreach (var t in topics) usedTopics.Add(t);
            }
        }
        
        if (selected.Count < count)
        {
            var remaining = sorted.Where(s => !selected.Any(sel => sel.Repository.Id == s.Repository.Id))
                                 .Take(count - selected.Count);
            selected.AddRange(remaining);
        }
        
        return selected.OrderByDescending(s => s.Score).ToList();
    }
    
    private async Task<Dictionary<string, double>> AnalyzeTopicDistributionAsync()
    {
        var distribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        var histories = (await _dbContext.ViewHistories.AsNoTracking().ToListAsync())
            .OrderByDescending(v => v.ViewedAt)
            .Take(100)
            .ToList();
        
        foreach (var history in histories)
        {
            var repo = await _dbContext.Repositories
                .FirstOrDefaultAsync(r => r.Id == history.RepositoryId);
            
            if (repo?.TopicsJson != null)
            {
                try
                {
                    var topics = JsonSerializer.Deserialize<List<string>>(repo.TopicsJson);
                    if (topics != null)
                    {
                        foreach (var topic in topics)
                        {
                            if (!distribution.ContainsKey(topic))
                                distribution[topic] = 0;
                            distribution[topic]++;
                        }
                    }
                }
                catch { }
            }
        }
        
        var total = distribution.Values.Sum();
        if (total == 0) return new Dictionary<string, double>();
        
        return distribution.ToDictionary(
            kv => kv.Key,
            kv => (double)kv.Value / total,
            StringComparer.OrdinalIgnoreCase);
    }
    
    private async Task<Dictionary<string, double>> AnalyzeLanguageDistributionAsync()
    {
        var distribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        var histories = (await _dbContext.ViewHistories.AsNoTracking().ToListAsync())
            .OrderByDescending(v => v.ViewedAt)
            .Take(100)
            .ToList();
        
        foreach (var history in histories)
        {
            var repo = await _dbContext.Repositories
                .FirstOrDefaultAsync(r => r.Id == history.RepositoryId);
            
            if (!string.IsNullOrEmpty(repo?.PrimaryLanguage))
            {
                var lang = repo.PrimaryLanguage;
                if (!distribution.ContainsKey(lang))
                    distribution[lang] = 0;
                distribution[lang]++;
            }
        }
        
        var total = distribution.Values.Sum();
        if (total == 0) return new Dictionary<string, double>();
        
        return distribution.ToDictionary(
            kv => kv.Key,
            kv => (double)kv.Value / total,
            StringComparer.OrdinalIgnoreCase);
    }
    
    private async Task PersistFeedbackAsync(long repositoryId, FeedbackType type)
    {
        try
        {
            var history = new Data.Entities.ViewHistoryEntity
            {
                RepositoryId = repositoryId,
                ViewedAt = DateTimeOffset.Now,
                DurationSeconds = (int)type,
                Source = 999
            };
            
            _dbContext.ViewHistories.Add(history);
            await _dbContext.SaveChangesAsync();
        }
        catch { }
    }
    
    private async Task UpdateProfileFromBookmarkAsync(long repositoryId)
    {
        try
        {
            var repo = await _repositoryService.GetByIdAsync(repositoryId);
            if (repo == null) return;
            
            var preferences = await _userService.GetPreferencesAsync();
            
            foreach (var topic in repo.Topics)
            {
                if (!preferences.InterestedTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
                {
                    preferences.InterestedTopics.Add(topic);
                }
            }
            
            if (!string.IsNullOrEmpty(repo.PrimaryLanguage) && 
                !preferences.InterestedLanguages.Contains(repo.PrimaryLanguage, StringComparer.OrdinalIgnoreCase))
            {
                preferences.InterestedLanguages.Add(repo.PrimaryLanguage);
            }
            
            await _userService.SavePreferencesAsync(preferences);
        }
        catch { }
    }
    
    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", 
            "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", 
            "would", "could", "should", "may", "might", "must", "shall", "can", "need",
            "for", "and", "nor", "but", "or", "yet", "so", "in", "on", "at", "to", "of" };
        
        return text.ToLower()
            .Split(new[] { ' ', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .ToHashSet();
    }
}

internal class ScoredRepository
{
    public Repository Repository { get; }
    public double Score { get; }
    public RepositoryFeatures Features { get; }
    
    public ScoredRepository(Repository repo, double score, RepositoryFeatures features)
    {
        Repository = repo;
        Score = score;
        Features = features;
    }
}

internal class RepositoryComparer : IEqualityComparer<Repository>
{
    public bool Equals(Repository? x, Repository? y)
    {
        if (x == null || y == null) return false;
        return x.Id == y.Id;
    }
    
    public int GetHashCode(Repository obj)
    {
        return obj.Id.GetHashCode();
    }
}

public class UserProfile
{
    public List<string> InterestedTopics { get; set; } = new();
    public List<string> InterestedLanguages { get; set; } = new();
    public Dictionary<string, double> TopicWeights { get; set; } = new();
    public Dictionary<string, double> LanguageWeights { get; set; } = new();
    public List<Repository> BookmarkedRepos { get; set; } = new();
    public List<Repository> ViewedRepos { get; set; } = new();
    public int MinStars { get; set; }
    public int MaxStars { get; set; }
    public bool PreferFreshContent { get; set; }
    public bool PreferSmallProjects { get; set; }
}

internal class InterestProfile
{
    public List<string> CommonTopics { get; set; } = new();
    public List<string> CommonLanguages { get; set; } = new();
    public int AvgStars { get; set; }
    public int MinStars { get; set; }
    public int MaxStars { get; set; }
}
