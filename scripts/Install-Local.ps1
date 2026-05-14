param(
  [Parameter(Mandatory = $true)]
  [string] $GamePath,

  [string] $Configuration = "Release",
  [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $RepoRoot "src\EsotericEbbFrench\EsotericEbbFrench.csproj"
$Dll = Join-Path $RepoRoot "src\EsotericEbbFrench\bin\$Configuration\net6.0\EsotericEbbFrench.dll"
$TranslationsSource = Join-Path $RepoRoot "assets\translations"
$PluginTarget = Join-Path $GamePath "BepInEx\plugins\EsotericEbbFrench"

if (-not (Test-Path (Join-Path $GamePath "Esoteric Ebb.exe"))) {
  throw "Game executable not found in $GamePath"
}

if (-not $SkipBuild) {
  dotnet build $Project -c $Configuration
}

if (-not (Test-Path $Dll)) {
  throw "DLL not found: $Dll"
}

New-Item -ItemType Directory -Force $PluginTarget | Out-Null
Copy-Item $Dll (Join-Path $PluginTarget "EsotericEbbFrench.dll") -Force
Copy-Item $TranslationsSource (Join-Path $PluginTarget "translations") -Recurse -Force

Write-Host "Installed EsotericEbbFrench to $PluginTarget"
