param(
    [ValidateSet(6, 7)]
    [int] $ASFVersion = 6,
    [string] $ASFSourceDir
)

$ErrorActionPreference = 'Stop'
$repo = Join-Path $PSScriptRoot '../..'
$project = Join-Path $repo 'ASFFreeGames/ASFFreeGames.csproj'
$output = Join-Path $repo "out/publish-v$ASFVersion"
$archive = Join-Path $repo "out/ASFFreeGames-V$ASFVersion.zip"
$sourceArgument = @()
if ($ASFSourceDir) {
    $sourceArgument += "-p:ASFSourceDir=$((Resolve-Path -LiteralPath $ASFSourceDir).Path)"
}

dotnet publish $project -c Release -o $output -p:ASFVersion=$ASFVersion @sourceArgument -p:ContinuousIntegrationBuild=true -p:TargetLatestRuntimePatch=false -p:UseAppHost=false --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Plugin publish failed for ASF $ASFVersion."
}

# ASF loads the plugin alongside its own dependencies and extracts updates next to the installed DLL.
Compress-Archive -LiteralPath (Join-Path $output 'ASFFreeGames.dll') -DestinationPath $archive -Force

# Keep this in sync with GithubPluginUpdater.MinimumArchiveSize.
if ((Get-Item -LiteralPath $archive).Length -le 64KB) {
    throw "ASF $ASFVersion plugin archive is too small for automatic updates."
}
