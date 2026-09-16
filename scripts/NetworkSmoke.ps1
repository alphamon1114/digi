param(
    [string]$Player,
    [int]$Port = 17777
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$Player) { $Player = Join-Path $repo 'digi/Builds/DigiPrototype.exe' }
$Player = (Resolve-Path $Player).Path
$output = Join-Path $repo ('.verification/network/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$hostArgs = @('-batchmode', '-nographics', '-digiHost', '-digiAuto', '-digiPort', $Port, '-digiQuit', '150', '-digiTrace', ('"' + (Join-Path $output 'host.json') + '"'), '-logFile', ('"' + (Join-Path $output 'host.log') + '"'))
$hostProcess = Start-Process -FilePath $Player -ArgumentList $hostArgs -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3
$clients = @()
for ($i = 1; $i -le 3; $i++) {
    $arguments = @('-batchmode', '-nographics', '-digiClient', '127.0.0.1', '-digiAuto', '-digiPort', $Port, '-digiQuit', '140', '-digiTrace', ('"' + (Join-Path $output "client$i.json") + '"'), '-logFile', ('"' + (Join-Path $output "client$i.log") + '"'))
    $clients += Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
foreach ($client in $clients) { $client.WaitForExit(); if ($client.ExitCode -ne 0) { throw 'Network client process failed.' } }
$hostProcess.WaitForExit()
if ($hostProcess.ExitCode -ne 0) { throw 'Network host process failed.' }
$hostState = Get-Content -LiteralPath (Join-Path $output 'host.json') -Raw | ConvertFrom-Json
if ($hostState.outcome -ne 'SURVIVORS WIN - rescue complete') { throw 'Host did not reach rescue victory.' }
if ($hostState.sites.Count -ne 0 -or $hostState.reportSequence -ne 3) { throw 'Host report privacy/sequence failed.' }
for ($i = 1; $i -le 3; $i++) {
    $state = Get-Content -LiteralPath (Join-Path $output "client$i.json") -Raw | ConvertFrom-Json
    if ($state.teamXP -ne 210 -or $state.unlock -ne 2 -or $state.outcome -ne $hostState.outcome) { throw "Client $i state mismatch." }
    if ($state.sites.Count -ne 3 -or @($state.sites | Where-Object { !$_.resolved -or $_.progress -lt 12 }).Count -gt 0 -or $state.rescue -lt 20) { throw "Client $i evacuation/rescue state mismatch." }
    if ($state.detections.Count -ne 0 -or $state.reportSequence -ne 0) { throw "Client $i received villain-only information." }
    if (@($state.actors | Where-Object { $_.kind -eq 0 -and $_.stage -ne 0 }).Count -gt 0) { throw 'Expected devolution before end of smoke match.' }
    if (@($state.actors | Where-Object { $_.kind -le 1 -and $_.bot }).Count -gt 0) { throw 'Expected 4 real network peers, not practice bots.' }
    foreach ($actor in $state.actors) {
        $authority = $hostState.actors | Where-Object { $_.id -eq $actor.id }
        if ($actor.hp -ne $authority.hp -or $actor.stage -ne $authority.stage -or $actor.energy -ne $authority.energy) { throw "Actor $($actor.id) replication mismatch." }
    }
}
if (Get-ChildItem $output -Filter '*.log' | Select-String -Pattern 'Exception|Error|failed' -CaseSensitive) { throw 'Inspect network logs for runtime errors.' }
Write-Output 'DIGI_NETWORK_SMOKE_PASSED: host + 3 clients, XP/unlock/forms, privacy, rescue victory.'
