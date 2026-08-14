param(
    [string]$GameDir = $env:STS2_GAME_DIR,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    throw "Pass -GameDir or set STS2_GAME_DIR to the Slay the Spire 2 installation root."
}

$gameDll = Join-Path $GameDir "data_sts2_windows_x86_64\sts2.dll"
if (-not (Test-Path -LiteralPath $gameDll)) {
    throw "Could not find the managed STS2 assembly under the supplied game directory."
}

$project = Join-Path $PSScriptRoot "STS2Connector.Host.csproj"
$output = Join-Path $PSScriptRoot "out\STS2_MCP"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}
dotnet build $project -c $Configuration -o $output --no-incremental `
    -p:STS2GameDir="$GameDir" `
    -p:UseSharedCompilation=false `
    -p:Deterministic=true `
    -p:ContinuousIntegrationBuild=true `
    -p:PathMap="$repositoryRoot=/_/sts2-connector"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Built $output\STS2_MCP.dll"
Write-Host "Use 'npm run deploy' at the repository root for verified backup and installation."
