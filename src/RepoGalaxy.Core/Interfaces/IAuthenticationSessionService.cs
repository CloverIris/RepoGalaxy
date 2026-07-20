using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

public enum AuthenticationSessionState { SignedOut, Authorizing, Validating, Initializing, SignedIn, ReauthenticationRequired, SigningOut }
public sealed record AuthenticationSessionSnapshot(AuthenticationSessionState State, User? User = null, string? ErrorCode = null)
{
    public bool IsAuthenticated => State == AuthenticationSessionState.SignedIn && User is not null;
}
public interface IAuthenticationSessionService
{
    AuthenticationSessionSnapshot Current { get; }
    event EventHandler<AuthenticationSessionSnapshot>? Changed;
    Task<AuthenticationSessionSnapshot> RestoreAsync(CancellationToken cancellationToken = default);
    Task<AuthenticationSessionSnapshot> SignInAsync(string accessToken, string method, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
