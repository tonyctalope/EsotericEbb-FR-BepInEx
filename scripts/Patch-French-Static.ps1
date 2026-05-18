param(
  [string] $GamePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GamePath)) {
  $LocalPatcher = Join-Path $PSScriptRoot "tools\StaticInkPatcher\StaticInkPatcher.exe"
  if (Test-Path -LiteralPath $LocalPatcher) {
    $GamePath = $PSScriptRoot
  } else {
    $GamePath = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
  }
}

$ResolvedGamePath = Resolve-Path -LiteralPath $GamePath
$Patcher = Join-Path $ResolvedGamePath "tools\StaticInkPatcher\StaticInkPatcher.exe"

if (-not (Test-Path -LiteralPath $Patcher)) {
  throw "Static patcher not found: $Patcher"
}

& $Patcher patch --game-dir $ResolvedGamePath
