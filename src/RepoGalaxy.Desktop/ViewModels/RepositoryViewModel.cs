using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed class RepositoryViewModel
{
    public RepositoryViewModel(Repository repository) => Repository = repository;
    public Repository Repository { get; }
    public long Id => Repository.Id;
    public string Name => Repository.Name;
    public string Owner => Repository.Owner;
    public string FullName => Repository.FullName;
    public string Description => Repository.Description;
    public string PrimaryLanguage => Repository.PrimaryLanguage;
    public int Stars => Repository.Stars;
    public int Forks => Repository.Forks;
    public string StarsFormatted => Stars.ToString("N0");
    public string ForksFormatted => Forks.ToString("N0");
}
