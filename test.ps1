param([switch]$Hardware, [string]$HeadsetName, [string]$SpeakerName)
$ErrorActionPreference = 'Stop'
if ($Hardware -and (-not $HeadsetName -or -not $SpeakerName)) {
    throw 'Hardware tests temporarily switch audio outputs. Specify two distinct connected devices with -HeadsetName and -SpeakerName.'
}
$taskRepoRoot = $PSScriptRoot
$taskWork = Join-Path ([IO.Path]::GetTempPath()) ('LightAudioSwitch-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskWork -Force | Out-Null
& (Join-Path $taskRepoRoot 'build.ps1') -OutputDirectory $taskWork
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $taskWork 'AudioSwitchTestGame.exe')) (Join-Path $taskRepoRoot 'tests\Fixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test fixture compilation failed.' }
& $taskCompiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $taskWork 'Verify.exe')) ('/reference:' + (Join-Path $taskWork 'LightAudioSwitch.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll (Join-Path $taskRepoRoot 'tests\Verify.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$taskReport = Join-Path $taskWork 'test-results.txt'
$taskTestArgs = @($taskWork, $taskReport)
if ($Hardware) {
    Write-Host 'Hardware tests will temporarily switch audio outputs and then restore the original default devices.'
    $taskTestArgs += @($HeadsetName, $SpeakerName)
}
& (Join-Path $taskWork 'Verify.exe') @taskTestArgs
if ($LASTEXITCODE -ne 0) { throw ('Tests failed. Report: ' + $taskReport) }
Write-Host ('Tests passed. Report: ' + $taskReport)
