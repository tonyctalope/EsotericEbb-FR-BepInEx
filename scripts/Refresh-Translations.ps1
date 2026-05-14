param(
  [Parameter(Mandatory = $true)]
  [string] $SourcePath,

  [string] $GamePath = "",
  [string] $StatusPath = "",
  [string] $PythonPath = "python"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Script = Join-Path $PSScriptRoot "refresh_translations.py"

$Args = @(
  "--repo", $RepoRoot,
  "--source", (Resolve-Path $SourcePath)
)

if ($GamePath -ne "") {
  $Args += @("--game", (Resolve-Path $GamePath))
}

if ($StatusPath -ne "") {
  $Args += @("--status", (Resolve-Path $StatusPath))
}

& $PythonPath $Script @Args
