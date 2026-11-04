param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$pluginDirectory = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $pluginDirectory 'docs\screenshots' }
& (Join-Path $pluginDirectory 'Build.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$references = @('System.dll','System.Core.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$references += '/reference:' + (Join-Path $pluginDirectory 'bin\CodexFloat.exe')
$outputFile = Join-Path $pluginDirectory 'bin\RenderScreenshots.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /codepage:65001 /utf8output ('/out:' + $outputFile) @references (Join-Path $PSScriptRoot 'RenderScreenshots.cs')
if ($LASTEXITCODE -ne 0) { throw 'Screenshot renderer build failed.' }
& $outputFile $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Screenshot render failed.' }
