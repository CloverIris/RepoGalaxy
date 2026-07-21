using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Views;

public partial class StartupWindow : Window
{
    public event EventHandler? RetryRequested;
    public event EventHandler? RestoreRequested;

    public StartupWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ConfigureWindowChrome();
    }

    private void ConfigureWindowChrome()
    {
        if (this.FindControl<Control>("StartupTitleBar") is { } title)
            WindowDecorationProperties.SetElementRole(title, WindowDecorationsElementRole.TitleBar);
        if (this.FindControl<Control>("StartupCloseButton") is { } close)
            WindowDecorationProperties.SetElementRole(close, WindowDecorationsElementRole.CloseButton);
    }

    public void Apply(StartupState state)
    {
        if (this.FindControl<TextBlock>("StartupTitle") is { } title) title.Text = state.Title;
        if (this.FindControl<TextBlock>("StartupMessage") is { } message) message.Text = state.Message;
        if (this.FindControl<ProgressBar>("StartupProgress") is { } progress)
        {
            progress.Value = state.Progress;
            progress.IsIndeterminate = state.Phase is StartupPhase.Database or StartupPhase.Restoring;
        }
        if (this.FindControl<Button>("RestoreButton") is { } restore) restore.IsVisible = state.CanRestore;
        if (this.FindControl<Button>("RetryButton") is { } retry) retry.IsVisible = state.Phase == StartupPhase.Failed;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    private void OnRetryClick(object? sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);
    private void OnRestoreClick(object? sender, RoutedEventArgs e) => RestoreRequested?.Invoke(this, EventArgs.Empty);
}
