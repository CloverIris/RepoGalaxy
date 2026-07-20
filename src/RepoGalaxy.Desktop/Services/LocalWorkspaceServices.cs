using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Desktop.Services;

public sealed class LocalIdeDiscoveryService : ILocalIdeDiscoveryService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<LocalIdeDescriptor>? _cached;

    public async Task<IReadOnlyList<LocalIdeDescriptor>> DiscoverAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cached is not null) return _cached;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cached is not null) return _cached;
            _cached = await Task.Run(() => Discover(cancellationToken), cancellationToken);
            return _cached;
        }
        finally { _gate.Release(); }
    }

    public LocalIdeDescriptor? Recommend(IReadOnlyList<LocalIdeDescriptor> candidates, string language, IReadOnlyList<string> topics)
    {
        if (candidates.Count == 0) return null;
        var text = $"{language} {string.Join(' ', topics)}".ToLowerInvariant();
        int Score(LocalIdeDescriptor ide) => ide.RecommendationRank + (ide.Family switch
        {
            IdeFamily.VisualStudio when text.Contains("c#") || text.Contains(".net") || text.Contains("c++") => 100,
            IdeFamily.Rider when text.Contains("c#") || text.Contains(".net") => 95,
            IdeFamily.CLion when text.Contains("c++") || text.Contains("cmake") => 95,
            IdeFamily.PyCharm when text.Contains("python") => 95,
            IdeFamily.WebStorm when text.Contains("javascript") || text.Contains("typescript") => 92,
            IdeFamily.GoLand when text.Contains("go") => 95,
            IdeFamily.RustRover when text.Contains("rust") => 95,
            IdeFamily.IntelliJIdea when text.Contains("java") || text.Contains("kotlin") => 95,
            IdeFamily.VisualStudioCode => 55,
            _ => 20
        });
        return candidates.OrderByDescending(Score).ThenByDescending(x => VersionKey(x.Version)).First();
    }

    private static IReadOnlyList<LocalIdeDescriptor> Discover(CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, LocalIdeDescriptor>(StringComparer.OrdinalIgnoreCase);
        void Add(LocalIdeDescriptor ide)
        {
            if (!File.Exists(ide.ExecutablePath)) return;
            var key = Path.GetFullPath(ide.ExecutablePath);
            found.TryAdd(key, ide with { ExecutablePath = key });
        }

        DiscoverVisualStudio(Add, cancellationToken);
        DiscoverVsCode(Add);
        DiscoverJetBrains(Add, cancellationToken);
        return found.Values.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(x => VersionKey(x.Version)).ToList();
    }

    private static void DiscoverVisualStudio(Action<LocalIdeDescriptor> add, CancellationToken cancellationToken)
    {
        var installer = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswhere = Path.Combine(installer, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (File.Exists(vswhere))
        {
            try
            {
                var psi = new ProcessStartInfo(vswhere) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (var arg in new[] { "-products", "*", "-format", "json", "-utf8" }) psi.ArgumentList.Add(arg);
                using var process = Process.Start(psi);
                if (process is not null)
                {
                    var json = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);
                    using var document = JsonDocument.Parse(json);
                    foreach (var instance in document.RootElement.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var path = instance.TryGetProperty("installationPath", out var installation) ? installation.GetString() : null;
                        var name = instance.TryGetProperty("displayName", out var display) ? display.GetString() : "Visual Studio";
                        var version = instance.TryGetProperty("installationVersion", out var installed) ? installed.GetString() : string.Empty;
                        if (path is not null) add(new($"vs:{version}", name ?? "Visual Studio", IdeFamily.VisualStudio, version ?? string.Empty, Path.Combine(path, "Common7", "IDE", "devenv.exe"), IdeCapability.OpenSolution | IdeCapability.OpenProject, 70));
                    }
                }
            }
            catch { }
        }
    }

    private static void DiscoverVsCode(Action<LocalIdeDescriptor> add)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe")
        };
        foreach (var path in candidates) add(new("vscode", "Visual Studio Code", IdeFamily.VisualStudioCode, FileVersion(path), path, IdeCapability.OpenFolder, 50));
    }

    private static void DiscoverJetBrains(Action<LocalIdeDescriptor> add, CancellationToken cancellationToken)
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains", "Toolbox", "apps")
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "product-info.json", SearchOption.AllDirectories).Take(80).ToArray(); }
            catch { continue; }
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(file));
                    var product = document.RootElement;
                    var name = product.GetProperty("name").GetString() ?? "JetBrains IDE";
                    var version = product.TryGetProperty("version", out var versionValue) ? versionValue.GetString() ?? string.Empty : string.Empty;
                    var family = Family(name);
                    if (family is null || !product.TryGetProperty("launch", out var launches)) continue;
                    foreach (var launch in launches.EnumerateArray())
                    {
                        if (!launch.TryGetProperty("launcherPath", out var launcher)) continue;
                        var executable = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, launcher.GetString() ?? string.Empty));
                        add(new($"jetbrains:{family}:{version}", name, family.Value, version, executable, IdeCapability.OpenFolder | IdeCapability.OpenProject, 60));
                        break;
                    }
                }
                catch { }
            }
        }
    }

    private static IdeFamily? Family(string name) => name.ToLowerInvariant() switch
    {
        var x when x.Contains("rider") => IdeFamily.Rider,
        var x when x.Contains("clion") => IdeFamily.CLion,
        var x when x.Contains("pycharm") => IdeFamily.PyCharm,
        var x when x.Contains("webstorm") => IdeFamily.WebStorm,
        var x when x.Contains("goland") => IdeFamily.GoLand,
        var x when x.Contains("rustrover") => IdeFamily.RustRover,
        var x when x.Contains("intellij") => IdeFamily.IntelliJIdea,
        _ => null
    };

    private static string FileVersion(string path) { try { return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? string.Empty; } catch { return string.Empty; } }
    private static Version VersionKey(string value) => Version.TryParse(value.Split('-', '+')[0], out var version) ? version : new Version(0, 0);
}

public sealed class LocalRepositoryResolver : ILocalRepositoryResolver
{
    private readonly RepositoryService _repositories;
    public LocalRepositoryResolver(RepositoryService repositories) => _repositories = repositories;

    public async Task<LocalRepository?> ResolveAsync(long repositoryId, string owner, string name, string cloneUrl, CancellationToken cancellationToken = default)
    {
        var expected = Normalize(cloneUrl, owner, name);
        foreach (var repository in await _repositories.GetLocalRepositoriesAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(repository.LocalPath)) continue;
            var origin = repository.GitHubUrl ?? await ReadOriginAsync(repository.LocalPath, cancellationToken);
            if (Normalize(origin, string.Empty, string.Empty) != expected) continue;
            if (repository.GitHubUrl is null) await _repositories.AddLocalRepositoryAsync(repository.LocalPath, repository.Name, origin);
            return repository;
        }
        return null;
    }

    public async Task<string?> ReadOriginAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repositoryPath, ".git"))) return null;
        var psi = new ProcessStartInfo("git") { WorkingDirectory = repositoryPath, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "remote", "get-url", "origin" }) psi.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch { return null; }
    }

    internal static string Normalize(string? value, string owner, string name)
    {
        var input = string.IsNullOrWhiteSpace(value) ? $"https://github.com/{owner}/{name}" : value.Trim();
        input = Regex.Replace(input, "^git@github\\.com:", "https://github.com/", RegexOptions.IgnoreCase);
        input = input.Replace("ssh://git@github.com/", "https://github.com/", StringComparison.OrdinalIgnoreCase).TrimEnd('/');
        if (input.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) input = input[..^4];
        return input.ToLowerInvariant();
    }
}

public sealed class IdeLauncher : IIdeLauncher
{
    public async Task<(bool Success, string ErrorCode)> OpenAsync(LocalIdeDescriptor ide, string repositoryPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(ide.ExecutablePath) || !Directory.Exists(repositoryPath)) return (false, "ide_or_repository_missing");
        var target = await Task.Run(() => ResolveTarget(ide, repositoryPath), cancellationToken);
        if (target is null) return (false, "visual_studio_workspace_missing");
        try
        {
            var psi = new ProcessStartInfo(ide.ExecutablePath) { UseShellExecute = false, WorkingDirectory = repositoryPath };
            psi.ArgumentList.Add(target);
            Process.Start(psi);
            return (true, string.Empty);
        }
        catch { return (false, "ide_launch_failed"); }
    }

    private static string? ResolveTarget(LocalIdeDescriptor ide, string root)
    {
        if (ide.Family != IdeFamily.VisualStudio) return root;
        foreach (var pattern in new[] { "*.slnx", "*.sln", "*.csproj", "*.vcxproj", "*.fsproj" })
        {
            try { if (Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Take(1).FirstOrDefault() is { } file) return file; }
            catch { }
        }
        return null;
    }
}

public sealed class RepositoryCloneService : IRepositoryCloneService
{
    private static readonly Regex Percent = new(@"(?<value>\d{1,3})%", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly RepositoryService _repositories;

    public RepositoryCloneService(IDbContextFactory<RepoGalaxyDbContext> factory, RepositoryService repositories) { _factory = factory; _repositories = repositories; }

    public async Task<CloneResult> CloneAsync(CloneRequest request, IProgress<CloneProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!ValidRequest(request, out var cloneUri)) return new(false, string.Empty, "invalid_clone_request", "仓库地址或目标目录无效");
        Directory.CreateDirectory(request.ParentDirectory);
        var destination = Path.Combine(request.ParentDirectory, SafeName(request.Name));
        if (Directory.Exists(destination))
        {
            var origin = await ReadOriginAsync(destination, cancellationToken);
            if (LocalRepositoryResolver.Normalize(origin, string.Empty, string.Empty) == LocalRepositoryResolver.Normalize(cloneUri!.AbsoluteUri, string.Empty, string.Empty))
            {
                await _repositories.AddLocalRepositoryAsync(destination, request.Name, cloneUri.AbsoluteUri);
                return new(true, destination, Message: "已关联现有本地仓库");
            }
            return new(false, destination, "destination_exists", "目标目录已经存在且不是同一仓库，请选择其他位置");
        }
        var operationId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(request.ParentDirectory, $".{SafeName(request.Name)}.repogalaxy-{operationId}.tmp");
        var operation = new CloneOperationEntity
        {
            Id = operationId, RepositoryId = request.RepositoryId, RepositoryFullName = $"{request.Owner}/{request.Name}",
            ParentDirectory = request.ParentDirectory, StagingDirectory = staging, DestinationDirectory = destination,
            Mode = (int)request.Mode, State = (int)CloneOperationState.Preparing, StartedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        await SaveOperationAsync(operation, cancellationToken);
        progress?.Report(new(CloneOperationState.Preparing, null, "正在准备克隆目录"));
        try
        {
            var psi = new ProcessStartInfo("git") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = request.ParentDirectory };
            foreach (var arg in new[] { "clone", "--progress", "--origin", "origin" }) psi.ArgumentList.Add(arg);
            if (request.Mode == CloneMode.Shallow) { psi.ArgumentList.Add("--depth"); psi.ArgumentList.Add("1"); psi.ArgumentList.Add("--single-branch"); }
            psi.ArgumentList.Add(cloneUri!.AbsoluteUri);
            psi.ArgumentList.Add(staging);
            operation.State = (int)CloneOperationState.Cloning; operation.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveOperationAsync(operation, cancellationToken);
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
            var readError = PumpAsync(process.StandardError, line => Report(line, progress), cancellationToken);
            var readOutput = PumpAsync(process.StandardOutput, line => Report(line, progress), cancellationToken);
            try { await process.WaitForExitAsync(cancellationToken); }
            catch (OperationCanceledException) { TryKill(process); throw; }
            await Task.WhenAll(readError, readOutput);
            if (process.ExitCode != 0) throw new CloneFailedException("git_clone_failed");
            operation.State = (int)CloneOperationState.Finalizing; operation.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveOperationAsync(operation, cancellationToken);
            progress?.Report(new(CloneOperationState.Finalizing, .98, "正在完成工作区"));
            Directory.Move(staging, destination);
            await _repositories.AddLocalRepositoryAsync(destination, request.Name, cloneUri.AbsoluteUri);
            operation.State = (int)CloneOperationState.Completed; operation.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveOperationAsync(operation, cancellationToken);
            progress?.Report(new(CloneOperationState.Completed, 1, "克隆完成", LocalPath: destination));
            return new(true, destination, Message: "克隆完成");
        }
        catch (OperationCanceledException)
        {
            await FailAsync(operation, CloneOperationState.Cancelled, "cancelled");
            DeleteOwnedStaging(operation);
            progress?.Report(new(CloneOperationState.Cancelled, null, "已取消克隆", "cancelled"));
            return new(false, string.Empty, "cancelled", "克隆已取消");
        }
        catch (CloneFailedException ex)
        {
            await FailAsync(operation, CloneOperationState.Failed, ex.Code);
            DeleteOwnedStaging(operation);
            progress?.Report(new(CloneOperationState.Failed, null, "Git 克隆失败，请检查网络或凭证管理器", ex.Code));
            return new(false, string.Empty, ex.Code, "Git 克隆失败，请检查网络或凭证管理器");
        }
        catch
        {
            await FailAsync(operation, CloneOperationState.Failed, "clone_io_failed");
            DeleteOwnedStaging(operation);
            return new(false, string.Empty, "clone_io_failed", "无法创建本地工作区");
        }
    }

    public async Task CleanupAbandonedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        // Calculate the boundary before composing the query. SQLite can compare the
        // converted UTC value with a parameter, but cannot translate DateTimeOffset
        // arithmetic embedded in the expression tree.
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        var abandoned = await db.CloneOperations
            .Where(x => x.State != (int)CloneOperationState.Completed && x.UpdatedAt < cutoff)
            .ToListAsync(cancellationToken);
        foreach (var operation in abandoned) { DeleteOwnedStaging(operation); operation.State = (int)CloneOperationState.Failed; operation.ErrorCode = "abandoned_cleanup"; operation.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesWithRetryAsync(cancellationToken);
    }

    private async Task SaveOperationAsync(CloneOperationEntity operation, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.CloneOperations.FindAsync([operation.Id], cancellationToken);
        if (existing is null) db.CloneOperations.Add(operation);
        else
        {
            existing.State = operation.State; existing.ErrorCode = operation.ErrorCode; existing.UpdatedAt = operation.UpdatedAt;
            existing.StagingDirectory = operation.StagingDirectory; existing.DestinationDirectory = operation.DestinationDirectory;
        }
        await db.SaveChangesWithRetryAsync(cancellationToken);
    }
    private async Task FailAsync(CloneOperationEntity operation, CloneOperationState state, string error)
    {
        operation.State = (int)state; operation.ErrorCode = error; operation.UpdatedAt = DateTimeOffset.UtcNow;
        try { await SaveOperationAsync(operation, CancellationToken.None); } catch { }
    }
    private static async Task PumpAsync(StreamReader reader, Action<string> consume, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line) consume(line);
    }
    private static async Task<string?> ReadOriginAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(repositoryPath, ".git"))) return null;
        var psi = new ProcessStartInfo("git") { WorkingDirectory = repositoryPath, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "remote", "get-url", "origin" }) psi.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;
            var value = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? value.Trim() : null;
        }
        catch { return null; }
    }
    private static void Report(string line, IProgress<CloneProgress>? progress)
    {
        var match = Percent.Match(line);
        double? percentage = match.Success && double.TryParse(match.Groups["value"].Value, out var value) ? value / 100d : null;
        var stage = line.Contains("Receiving", StringComparison.OrdinalIgnoreCase) ? "正在接收对象" : line.Contains("Resolving", StringComparison.OrdinalIgnoreCase) ? "正在解析增量" : line.Contains("Checking", StringComparison.OrdinalIgnoreCase) ? "正在检出文件" : "正在克隆仓库";
        progress?.Report(new(CloneOperationState.Cloning, percentage, stage));
    }
    private static bool ValidRequest(CloneRequest request, out Uri? uri)
    {
        uri = null;
        try
        {
            var parent = Path.GetFullPath(request.ParentDirectory);
            if (!Directory.Exists(parent) && !Directory.Exists(Path.GetDirectoryName(parent))) return false;
        }
        catch { return false; }
        return Uri.TryCreate(request.CloneUrl, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(uri.UserInfo);
    }
    private static string SafeName(string value) => string.Concat(value.Where(x => !Path.GetInvalidFileNameChars().Contains(x))).Trim();
    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { } }
    private static void DeleteOwnedStaging(CloneOperationEntity operation)
    {
        try
        {
            var staging = Path.GetFullPath(operation.StagingDirectory);
            var parent = Path.GetFullPath(operation.ParentDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (staging.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(staging).Contains($".repogalaxy-{operation.Id}.tmp", StringComparison.Ordinal) && Directory.Exists(staging)) Directory.Delete(staging, true);
        }
        catch { }
    }
    private sealed class CloneFailedException(string code) : Exception { public string Code { get; } = code; }
}
