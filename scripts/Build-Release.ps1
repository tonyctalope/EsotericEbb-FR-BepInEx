param(
  [string] $Version = "0.1.0",
  [string] $Configuration = "Release",
  [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $RepoRoot "src\EsotericEbbFrench\EsotericEbbFrench.csproj"
$Dist = Join-Path $RepoRoot "dist"
$StageRoot = Join-Path $Dist "stage"
$PluginStage = Join-Path $StageRoot "BepInEx\plugins\EsotericEbbFrench"
$TranslationsSource = Join-Path $RepoRoot "assets\translations"
$Dll = Join-Path $RepoRoot "src\EsotericEbbFrench\bin\$Configuration\net6.0\EsotericEbbFrench.dll"
$Zip = Join-Path $Dist "EsotericEbb-FR-BepInEx-$Version.zip"

if (-not $SkipBuild) {
  dotnet build $Project -c $Configuration
}

if (-not (Test-Path $Dll)) {
  throw "DLL not found: $Dll"
}

if (Test-Path $StageRoot) {
  Remove-Item $StageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force $PluginStage | Out-Null
Copy-Item $Dll (Join-Path $PluginStage "EsotericEbbFrench.dll") -Force
Copy-Item $TranslationsSource (Join-Path $PluginStage "translations") -Recurse -Force

if (Test-Path $Zip) {
  Remove-Item $Zip -Force
}

Compress-Archive -Path (Join-Path $StageRoot "*") -DestinationPath $Zip -Force
Write-Host "Release package written to $Zip"
