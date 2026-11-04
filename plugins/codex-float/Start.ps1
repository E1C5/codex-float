param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'bin\CodexFloat.exe'
if (-not (Test-Path -LiteralPath $executable)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
if ($Preview) { Start-Process -FilePath $executable -ArgumentList '--demo' }
else { Start-Process -FilePath $executable }
