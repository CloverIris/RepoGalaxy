using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Recommendation.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class DiscoverViewModel : ViewModelBase
{
    private readonly DiscoveryStore _store;
    private readonly DiscoverySyncService _sync;
    public ObservableCollection<FeedItem> Items { get; } = new();
    public IReadOnlyList<string> Sources { get; } = ["For you", "Subscriptions", "Trending"];
    [ObservableProperty] private string _selectedSource = "For you";
    [ObservableProperty] private FeedItem? _selectedItem;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "Create a subscription or sync to begin discovering repositories.";

    public DiscoverViewModel(DiscoveryStore store, DiscoverySyncService sync) { _store = store; _sync = sync; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Items.Clear();
            var source = SelectedSource switch { "Subscriptions" => FeedSource.Subscription, "Trending" => FeedSource.Trending, _ => FeedSource.ForYou };
            foreach (var item in await _store.GetFeedAsync(source)) Items.Add(item);
            Status = Items.Count == 0 ? "No items yet. Create a subscription or sync to find repositories." : $"{Items.Count} items";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand] public async Task SyncAsync() { IsLoading = true; try { await _sync.SyncAsync(); await _sync.RefreshForYouAsync(); } finally { await LoadAsync(); } }
    [RelayCommand] private async Task SaveAsync(long id) { var item = Items.FirstOrDefault(x => x.Id == id); if (item != null) { await _store.ToggleSavedAsync(item.RepositoryId); item.Repository.IsBookmarked = !item.Repository.IsBookmarked; } }
    [RelayCommand] private async Task DismissAsync(long id) { await _store.MarkReadAsync(id, true); var item = Items.FirstOrDefault(x => x.Id == id); if (item != null) Items.Remove(item); }
    partial void OnSelectedSourceChanged(string value) => _ = LoadAsync();
}

public sealed partial class SubscriptionsViewModel : ViewModelBase
{
    private readonly DiscoveryStore _store;
    public ObservableCollection<DiscoverySubscription> Items { get; } = new();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _topics = string.Empty;
    [ObservableProperty] private string _languages = string.Empty;
    [ObservableProperty] private string _keywords = string.Empty;
    public SubscriptionsViewModel(DiscoveryStore store) => _store = store;
    public async Task LoadAsync() { Items.Clear(); foreach (var item in await _store.GetSubscriptionsAsync()) Items.Add(item); }
    [RelayCommand] private async Task AddAsync() { if (string.IsNullOrWhiteSpace(Name)) return; await _store.SaveSubscriptionAsync(new DiscoverySubscription { Name = Name, Topics = Split(Topics), Languages = Split(Languages), Keywords = Split(Keywords) }); Name = Topics = Languages = Keywords = string.Empty; await LoadAsync(); }
    [RelayCommand] private async Task DeleteAsync(long id) { await _store.DeleteSubscriptionAsync(id); await LoadAsync(); }
    private static List<string> Split(string input) => input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly DiscoveryStore _store;
    public ObservableCollection<Repository> Items { get; } = new();
    [ObservableProperty] private string _status = string.Empty;
    public LibraryViewModel(DiscoveryStore store) => _store = store;
    public async Task LoadAsync() { Items.Clear(); foreach (var item in await _store.GetSavedRepositoriesAsync()) Items.Add(item); Status = Items.Count == 0 ? "Your library is empty." : $"{Items.Count} saved repositories"; }
    [RelayCommand] private async Task RemoveAsync(long id) { await _store.ToggleSavedAsync(id); await LoadAsync(); }
}

public sealed partial class NotificationsViewModel : ViewModelBase
{
    private readonly DiscoveryStore _store;
    public ObservableCollection<FeedItem> Items { get; } = new();
    public NotificationsViewModel(DiscoveryStore store) => _store = store;
    public async Task LoadAsync() { Items.Clear(); foreach (var item in await _store.GetNotificationsAsync()) Items.Add(item); }
    [RelayCommand] private async Task ReadAsync(long id) { await _store.MarkReadAsync(id); var item = Items.FirstOrDefault(x => x.Id == id); if (item != null) item.IsRead = true; }
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    [ObservableProperty] private string _selectedTheme = "System";
    [ObservableProperty] private int _syncIntervalMinutes = 30;
    [ObservableProperty] private double _notificationThreshold = .75;

    partial void OnSelectedThemeChanged(string value)
    {
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = value switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
