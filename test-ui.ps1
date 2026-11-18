$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskWork = Join-Path ([IO.Path]::GetTempPath()) ('LightAudioSwitch-ui-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $taskRoot 'build.ps1') -OutputDirectory $taskWork
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 ('/win32manifest:' + (Join-Path $taskRoot 'src\app.manifest')) ('/out:' + (Join-Path $taskWork 'UiVerify.exe')) ('/reference:' + (Join-Path $taskWork 'LightAudioSwitch.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $taskRoot 'tests\UiVerify.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
$taskProcess = Start-Process -FilePath (Join-Path $taskWork 'UiVerify.exe') -ArgumentList ('"' + $taskWork + '"') -WindowStyle Hidden -PassThru -Wait
$taskReport = Join-Path $taskWork 'ui-test-results.txt'
if ($taskProcess.ExitCode -ne 0) { Get-Content -LiteralPath $taskReport; throw 'UI tests failed.' }
Write-Host ('UI checks passed. Report: ' + $taskReport)
