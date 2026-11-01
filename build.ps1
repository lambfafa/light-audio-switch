param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskSource = Join-Path $taskRoot 'src'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'dist' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'Windows x64 and .NET Framework 4.x are required.' }
$taskArguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/debug-',
    ('/out:' + (Join-Path $taskOutput 'LightAudioSwitch.exe')),
    ('/win32manifest:' + (Join-Path $taskSource 'app.manifest')),
    ('/win32icon:' + (Join-Path $taskSource 'app.ico')),
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll')
$taskFiles = Get-ChildItem -LiteralPath $taskSource -Filter '*.cs' | ForEach-Object { $_.FullName }
& $taskCompiler @taskArguments @taskFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $taskSource 'LightAudioSwitch.exe.config') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs\使用说明.txt') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination $taskOutput -Force
Get-Item -LiteralPath (Join-Path $taskOutput 'LightAudioSwitch.exe') | Select-Object FullName,Length
