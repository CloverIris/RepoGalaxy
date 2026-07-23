[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$version = '1.0.0-preview.1'
$runtime = 'win-x64'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repoRoot 'src\RepoGalaxy.Desktop\RepoGalaxy.Desktop.csproj'
$releaseRoot = Join-Path $repoRoot 'release'
$finalDirectory = Join-Path $releaseRoot $version
$releaseReadme = Join-Path $repoRoot "Docs\Release\README.$version.md"
$stagingRoot = Join-Path $repoRoot ("obj\release-staging\" + [Guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $stagingRoot 'publish'
$packageDirectory = Join-Path $stagingRoot 'package'
$finalExeName = "RepoGalaxy-$version-$runtime.exe"
$stagedExe = Join-Path $packageDirectory $finalExeName
$backupDirectory = $null

function Assert-PathUnderRepository {
    param([Parameter(Mandatory)][string]$Path)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = $repoRoot.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $resolved"
    }
}

Assert-PathUnderRepository $stagingRoot
Assert-PathUnderRepository $finalDirectory

if (-not (Test-Path -LiteralPath $project)) {
    throw "Desktop project was not found: $project"
}
if (-not (Test-Path -LiteralPath $releaseReadme)) {
    throw "Release README source was not found: $releaseReadme"
}

try {
    New-Item -ItemType Directory -Force -Path $publishDirectory, $packageDirectory | Out-Null

    & dotnet publish $project `
        --configuration Release `
        --runtime $runtime `
        --self-contained true `
        --output $publishDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $publishedExe = Join-Path $publishDirectory 'RepoGalaxy.Desktop.exe'
    if (-not (Test-Path -LiteralPath $publishedExe)) {
        throw "The expected single-file executable was not produced."
    }

    Copy-Item -LiteralPath $publishedExe -Destination $stagedExe
    Copy-Item -LiteralPath $releaseReadme -Destination (Join-Path $packageDirectory 'README.md')

    $hash = (Get-FileHash -LiteralPath $stagedExe -Algorithm SHA256).Hash.ToUpperInvariant()
    $checksumLine = "$hash  $finalExeName"
    [System.IO.File]::WriteAllText(
        (Join-Path $packageDirectory 'SHA256SUMS.txt'),
        $checksumLine + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    $expectedFiles = @($finalExeName, 'README.md', 'SHA256SUMS.txt') | Sort-Object
    $actualFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File | Select-Object -ExpandProperty Name | Sort-Object)
    if (($actualFiles.Count -ne $expectedFiles.Count) -or
        (Compare-Object -ReferenceObject $expectedFiles -DifferenceObject $actualFiles)) {
        throw "The staged package does not contain exactly the expected three files."
    }

    New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
    if (Test-Path -LiteralPath $finalDirectory) {
        $backupDirectory = Join-Path $releaseRoot (".$version.previous-" + [Guid]::NewGuid().ToString('N'))
        Assert-PathUnderRepository $backupDirectory
        Move-Item -LiteralPath $finalDirectory -Destination $backupDirectory
    }

    try {
        Move-Item -LiteralPath $packageDirectory -Destination $finalDirectory
    }
    catch {
        if ($backupDirectory -and (Test-Path -LiteralPath $backupDirectory) -and -not (Test-Path -LiteralPath $finalDirectory)) {
            Move-Item -LiteralPath $backupDirectory -Destination $finalDirectory
            $backupDirectory = $null
        }
        throw
    }

    if ($backupDirectory -and (Test-Path -LiteralPath $backupDirectory)) {
        Remove-Item -LiteralPath $backupDirectory -Recurse -Force
        $backupDirectory = $null
    }

    Write-Host "Published RepoGalaxy $version to:"
    Write-Host "  $finalDirectory"
    Write-Host "SHA-256:"
    Write-Host "  $hash"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
