<#
.SYNOPSIS
    Publishes SShot.App as a self-contained, single-file portable executable.

.DESCRIPTION
    Produces a single SShot.App.exe under build/publish/ that runs with no .NET runtime
    installed and no installation step. SatelliteResourceLanguages (en;ja, set in
    SShot.App.csproj) must be honored correctly even inside the single-file bundle - this is
    an easy-to-miss trap with PublishSingleFile, so verify Japanese strings actually render
    after publishing (see CLAUDE.md).
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\SShot.App\SShot.App.csproj"
$outputDir = Join-Path $repoRoot "build\publish"

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}

# MSBuild's up-to-date check is timestamp-based, so it happily reuses assemblies compiled without
# DebugType=none - and a stale dll drags its debug directory (with the absolute pdb path) back
# into the bundle. Force a full recompile. obj\ itself is kept so the restore assets survive;
# only the per-configuration subdirectories go.
foreach ($projectDir in @((Join-Path $repoRoot "src\SShot.App"), (Join-Path $repoRoot "src\SShot.Core"))) {
    foreach ($subPath in @("bin\$Configuration", "obj\$Configuration")) {
        $stalePath = Join-Path $projectDir $subPath
        if (Test-Path $stalePath) {
            Remove-Item -Recurse -Force $stalePath
        }
    }
}

Write-Host "Publishing SShot.App ($Configuration, $Runtime) to $outputDir ..." -ForegroundColor Cyan

# DebugType=none: without it the PE debug directory embeds the absolute path of the intermediate
# output (...\obj\Release\...), exposing the build environment to everyone who downloads the exe.
# Symbols are only dropped for the distributed build - ordinary dotnet build/run keeps full
# debugging.
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $outputDir "SShot.App.exe"
if (-not (Test-Path $exePath)) {
    throw "Expected output not found: $exePath"
}

# SkiaSharp's native package ships an ~85 MB libSkiaSharp.pdb that publish copies into the output
# even though DebugType=none suppresses our own symbols. It has no place in a distribution.
Get-ChildItem -Path $outputDir -Filter *.pdb -Recurse | Remove-Item -Force

# The exe must not carry any path from the build environment. Verified rather than trusted,
# because an embedded path is invisible in normal use.
$exeText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($exePath))
if ($exeText.Contains($env:USERPROFILE)) {
    throw "Published exe still embeds a build environment path. DebugType=none did not take effect."
}
$exeText = $null

# The exe bundles the .NET runtime, SkiaSharp and the rest of the dependency graph, so the
# distribution has to carry their licenses next to it - they cannot live inside the single file.
# THIRD-PARTY-NOTICES.md enumerates what is bundled; licenses/ holds the verbatim upstream texts.
$noticeFiles = @("LICENSE", "THIRD-PARTY-NOTICES.md")
foreach ($name in $noticeFiles) {
    $source = Join-Path $repoRoot $name
    if (-not (Test-Path $source)) {
        throw "Required notice file not found: $source"
    }
    Copy-Item $source -Destination $outputDir
}

$licensesSource = Join-Path $repoRoot "licenses"
if (-not (Test-Path $licensesSource)) {
    throw "Required notice directory not found: $licensesSource"
}
Copy-Item $licensesSource -Destination $outputDir -Recurse

$sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Host "Published: $exePath ($sizeMb MB)" -ForegroundColor Green
Write-Host "Verify manually on a machine without the .NET runtime installed, and confirm Japanese UI text renders correctly (SatelliteResourceLanguages + single-file gotcha)." -ForegroundColor Yellow
