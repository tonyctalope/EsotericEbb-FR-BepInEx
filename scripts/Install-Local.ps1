param(
  [Parameter(Mandatory = $true)]
  [string] $GamePath,

  [string] $Configuration = "Release",
  [switch] $SkipBuild,
  [switch] $NoPatch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$PatcherProject = Join-Path $RepoRoot "tools\StaticInkPatcher\StaticInkPatcher.csproj"
$PatcherPublish = Join-Path $RepoRoot "tools\StaticInkPatcher\bin\$Configuration\publish\win-x64"
$TranslationsSource = Join-Path $RepoRoot "assets\translations"
$PatcherTarget = Join-Path $GamePath "tools\StaticInkPatcher"
$TranslationsTarget = Join-Path $GamePath "translations"

if (-not (Test-Path (Join-Path $GamePath "Esoteric Ebb.exe"))) {
  throw "Game executable not found in $GamePath"
}

if (-not $SkipBuild) {
  dotnet publish $PatcherProject -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $PatcherPublish
}

New-Item -ItemType Directory -Force $PatcherTarget | Out-Null
Copy-Item (Join-Path $PatcherPublish "*") $PatcherTarget -Recurse -Force
Copy-Item $TranslationsSource $TranslationsTarget -Recurse -Force
Copy-Item (Join-Path $RepoRoot "scripts\Patch-French-Static.ps1") (Join-Path $GamePath "Patch-French-Static.ps1") -Force
Copy-Item (Join-Path $RepoRoot "scripts\Restore-Original-Assets.ps1") (Join-Path $GamePath "Restore-Original-Assets.ps1") -Force

if (-not $NoPatch) {
  & (Join-Path $GamePath "Patch-French-Static.ps1") -GamePath $GamePath
}

Write-Host "Installed static French patcher to $GamePath"
