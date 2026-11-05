param(
    [switch]$Offline,
    [switch]$SkipTests,
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'
$versionArguments = @()
if ($Version -ne '') {
    $csorbitNumber = '(0|[1-9][0-9]*)'
    $csorbitIdentifier = '(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
    $csorbitVersionPattern = "^$csorbitNumber\.$csorbitNumber\.$csorbitNumber(?:-$csorbitIdentifier(?:\.$csorbitIdentifier)*)?$"
    if ($Version -notmatch $csorbitVersionPattern) {
        throw 'Version must be MAJOR.MINOR.PATCH, optionally followed by a prerelease suffix such as -rc.1.'
    }
    $versionArguments = @("-p:Version=$Version")
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $projectRoot '.cache'
$artifactsRoot = Join-Path $projectRoot 'artifacts'

# Keep generated tooling state inside this checkout.
$env:DOTNET_CLI_HOME = Join-Path $cacheRoot 'dotnet'
$env:NUGET_PACKAGES = Join-Path $cacheRoot 'nuget'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $artifactsRoot -Force | Out-Null

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($Arguments -join ' ')" }
}

$restoreArguments = @('--ignore-failed-sources', '-p:NuGetAudit=false') + $versionArguments
if ($Offline) {
    # Copy only the two cached packages required by netstandard2.0.
    $existingCache = Join-Path $env:USERPROFILE '.nuget/packages'
    foreach ($package in @('netstandard.library', 'microsoft.netcore.platforms')) {
        $cachedPackage = Join-Path $existingCache $package
        $destination = Join-Path $env:NUGET_PACKAGES $package
        if (!(Test-Path -LiteralPath $destination) -and (Test-Path -LiteralPath $cachedPackage)) {
            Copy-Item -LiteralPath $cachedPackage -Destination $env:NUGET_PACKAGES -Recurse
        }
    }
    $offlineConfig = Join-Path $cacheRoot 'NuGet.Offline.Config'
    '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear /></packageSources></configuration>' |
        Set-Content -LiteralPath $offlineConfig -Encoding UTF8
    $restoreArguments += @('--configfile', $offlineConfig)
}

$libraryProject = Join-Path $projectRoot 'src/CSorbit/CSorbit.csproj'
$testProject = Join-Path $projectRoot 'tests/CSorbit.Tests/CSorbit.Tests.csproj'
$consoleProject = Join-Path $projectRoot 'examples/Console/CSorbit.Console.csproj'
Invoke-Dotnet -Arguments (@('restore', $libraryProject) + $restoreArguments)
Invoke-Dotnet -Arguments (@('build', $libraryProject, '-c', 'Release', '--no-restore') + $versionArguments)
if (!$SkipTests) {
    Invoke-Dotnet -Arguments (@('restore', $testProject) + $restoreArguments)
    Invoke-Dotnet -Arguments (@('build', $testProject, '-c', 'Release', '--no-restore') + $versionArguments)
    Invoke-Dotnet -Arguments @('run', '--project', $testProject, '-c', 'Release', '--no-build', '--no-restore')
}
Invoke-Dotnet -Arguments (@('restore', $consoleProject) + $restoreArguments)
Invoke-Dotnet -Arguments (@('build', $consoleProject, '-c', 'Release', '--no-restore') + $versionArguments)

$targets = @{
    godot3 = 'netstandard2.0'
    godot4 = 'net8.0'
    unity = 'netstandard2.0'
}
foreach ($platform in ($targets.Keys | Sort-Object)) {
    $destination = Join-Path $artifactsRoot $platform
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($extension in @('dll', 'pdb', 'xml')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "src/CSorbit/bin/Release/$($targets[$platform])/CSorbit.$extension") -Destination $destination -Force
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'README.ru.md') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $destination -Recurse -Force
    Compress-Archive -Path (Join-Path $destination '*') -DestinationPath (Join-Path $artifactsRoot "CSorbit-$platform.zip") -Force
}
foreach ($framework in @('netstandard2.0', 'netstandard2.1', 'net8.0')) {
    $destination = Join-Path $artifactsRoot "dotnet/$framework"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($extension in @('dll', 'pdb', 'xml')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "src/CSorbit/bin/Release/$framework/CSorbit.$extension") -Destination $destination -Force
    }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $artifactsRoot 'dotnet') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'README.ru.md') -Destination (Join-Path $artifactsRoot 'dotnet') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination (Join-Path $artifactsRoot 'dotnet') -Recurse -Force
Compress-Archive -Path (Join-Path $artifactsRoot 'dotnet/*') -DestinationPath (Join-Path $artifactsRoot 'CSorbit-dotnet.zip') -Force
Invoke-Dotnet -Arguments (@('pack', $libraryProject, '-c', 'Release', '--no-build', '--no-restore', '-o', (Join-Path $artifactsRoot 'nuget')) + $versionArguments)
Write-Output "Build complete: $artifactsRoot"
