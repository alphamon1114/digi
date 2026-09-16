param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'digi'
$method = if ($Build) { 'Digi.Prototype.Editor.PrototypeTools.Build' } else { 'Digi.Prototype.Editor.PrototypeTools.Verify' }
$log = Join-Path $repo 'verification.log'
# Close this project's Editor first. Paths are resolved from the checkout, never from a personal machine.
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', $method, '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)). Read verification.log." }
if (!$Build -and !(Select-String -LiteralPath $log -Pattern 'DIGI_VERIFICATION_PASSED' -Quiet)) { throw 'Verification completion marker missing.' }
Write-Output "Unity verification/build completed. Log: verification.log"
