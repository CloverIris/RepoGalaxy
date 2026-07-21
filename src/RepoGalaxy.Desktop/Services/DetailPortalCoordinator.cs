using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class DetailPortalCoordinator : IDetailPortalCoordinator
{
    public const double RevealThreshold = .65;
    public const double SnapThreshold = .92;
    public const double ExitThreshold = .82;

    public DetailPortalDecision Evaluate(DetailPresentationState current, bool hasFocus, double fitRatio)
    {
        if (!hasFocus) return new(DetailPresentationState.Board, false, current is DetailPresentationState.Full or DetailPresentationState.Snapping, false);
        if (current == DetailPresentationState.Peek) return new(current, false, false, false);
        if (current == DetailPresentationState.Snapping) return new(current, false, false, true);
        if (current == DetailPresentationState.Full)
            return fitRatio < ExitThreshold
                ? new(DetailPresentationState.Exiting, false, true, false)
                : new(DetailPresentationState.Full, false, false, true);
        if (fitRatio < RevealThreshold) return new(DetailPresentationState.Board, false, false, false);
        if (fitRatio >= SnapThreshold) return new(DetailPresentationState.Snapping, true, false, true);
        return new(DetailPresentationState.Portal, false, false, false);
    }
}
