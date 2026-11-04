param([switch]$RunChecks)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler is required.' }
$binDirectory = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $binDirectory -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$compileArguments = @('/nologo','/target:winexe','/optimize+','/codepage:65001','/utf8output',('/out:' + (Join-Path $binDirectory 'CodexFloat.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'ui\app.manifest'))) + $references + @((Join-Path $PSScriptRoot 'ui\Program.cs'))
& $compiler @compileArguments
if ($LASTEXITCODE -ne 0) { throw 'Codex Float build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\App.config') -Destination (Join-Path $binDirectory 'CodexFloat.exe.config') -Force
Write-Output (Join-Path $binDirectory 'CodexFloat.exe')
if ($RunChecks) {
    $testOutput = Join-Path $binDirectory 'LayoutTests.exe'
    & $compiler /nologo /target:exe /codepage:65001 /utf8output (('/out:') + $testOutput) @references (('/reference:') + (Join-Path $binDirectory 'CodexFloat.exe')) (Join-Path $PSScriptRoot 'tests\LayoutTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Layout check build failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Layout geometry checks failed.' }
}
