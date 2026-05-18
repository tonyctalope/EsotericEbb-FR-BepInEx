param(
  [string] $Version = "0.1.0",
  [string] $Configuration = "Release",
  [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $RepoRoot "src\EsotericEbbFrench\EsotericEbbFrench.csproj"
$PatcherProject = Join-Path $RepoRoot "tools\StaticInkPatcher\StaticInkPatcher.csproj"
$PatcherPublish = Join-Path $RepoRoot "tools\StaticInkPatcher\bin\$Configuration\publish\win-x64"
$Dist = Join-Path $RepoRoot "dist"
$StageRoot = Join-Path $Dist "stage"
$PluginStage = Join-Path $StageRoot "BepInEx\plugins\EsotericEbbFrench"
$PatcherStage = Join-Path $StageRoot "tools\StaticInkPatcher"
$TranslationsSource = Join-Path $RepoRoot "assets\translations"
$Dll = Join-Path $RepoRoot "src\EsotericEbbFrench\bin\$Configuration\net6.0\EsotericEbbFrench.dll"
$Zip = Join-Path $Dist "EsotericEbb-FR-BepInEx-$Version.zip"

if (-not $SkipBuild) {
  dotnet build $Project -c $Configuration
}

dotnet publish $PatcherProject -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $PatcherPublish

if (-not (Test-Path $Dll)) {
  throw "DLL not found: $Dll"
}

if (Test-Path $StageRoot) {
  Remove-Item $StageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force $PluginStage | Out-Null
New-Item -ItemType Directory -Force $PatcherStage | Out-Null
Copy-Item $Dll (Join-Path $PluginStage "EsotericEbbFrench.dll") -Force
Copy-Item $TranslationsSource (Join-Path $PluginStage "translations") -Recurse -Force
Copy-Item (Join-Path $PatcherPublish "*") $PatcherStage -Recurse -Force
Copy-Item (Join-Path $RepoRoot "scripts\Patch-French-Static.ps1") (Join-Path $StageRoot "Patch-French-Static.ps1") -Force
Copy-Item (Join-Path $RepoRoot "scripts\Restore-Original-Assets.ps1") (Join-Path $StageRoot "Restore-Original-Assets.ps1") -Force

if (Test-Path $Zip) {
  Remove-Item $Zip -Force
}

Compress-Archive -Path (Join-Path $StageRoot "*") -DestinationPath $Zip -Force
Write-Host "Release package written to $Zip"
