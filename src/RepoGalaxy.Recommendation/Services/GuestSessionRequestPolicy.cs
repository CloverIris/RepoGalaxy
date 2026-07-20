namespace RepoGalaxy.Recommendation.Services;

/// <summary>Allows exactly one automatic anonymous request per application session.</summary>
public sealed class GuestSessionRequestPolicy
{
    private int _automaticRequestUsed;
    public bool TryConsume(bool manual) => manual || Interlocked.Exchange(ref _automaticRequestUsed, 1) == 0;
    public bool AutomaticRequestUsed => Volatile.Read(ref _automaticRequestUsed) == 1;
}
