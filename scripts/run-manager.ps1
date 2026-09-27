<#
.SYNOPSIS
  Runs the cozmo-manager unattended: starts it, and each time it ends its reply, starts it again.

.DESCRIPTION
  Each round is one `opencode run` of the manager. The manager keeps its state in files (PROJECT_STATE.md,
  the inventories, the manifest, git), so a new round picks up where the last one stopped. Before it ends a
  reply, the manager writes .scratch/manager-status.txt: CONTINUE, NEED_OPERATOR or DONE, plus a reason.

  The runner stops when:
    - the manager says NEED_OPERATOR or DONE;
    - -MaxRounds rounds have run;
    - -MaxIdleRounds rounds in a row made no commit and wrote no status;
    - a file named STOP exists at the repo root (create it to stop after the current round).

  Every round's output is logged to .scratch/runner/. Set a credit limit on your OpenRouter key: that is the
  spending cap this script can't exceed.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\run-manager.ps1 -MaxRounds 2
  (a short first test)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\run-manager.ps1
#>
param(
    [int]$MaxRounds = 30,
    [int]$MaxIdleRounds = 3
)

# 'Continue', not 'Stop': in Windows PowerShell 5.1 a native command's stderr, redirected, would otherwise abort the run.
$ErrorActionPreference = 'Continue'
$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root

$scratch = Join-Path $root '.scratch'
$logDir = Join-Path $scratch 'runner'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$statusFile = Join-Path $scratch 'manager-status.txt'
$stopFile = Join-Path $root 'STOP'
$summary = Join-Path $logDir 'manager-summary.txt'

$task = 'Start the session per your instructions and work through the Next list. Before you end your reply, write .scratch/manager-status.txt as your instructions describe.'
$help = (& opencode run --help 2>&1 | Out-String)
$agentFlag = $help -match '--agent'
if (-not $agentFlag) {
    $task = 'Read .opencode/agent/cozmo-manager.md and act as that manager for this session. ' + $task
}

function Write-Summary([string]$line) {
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$stamp  $line" | Tee-Object -FilePath $summary -Append
}

Write-Summary "runner start (max $MaxRounds rounds, agent flag: $agentFlag)"
$idle = 0
for ($round = 1; $round -le $MaxRounds; $round++) {
    if (Test-Path $stopFile) { Write-Summary 'STOP file found; stopping.'; break }

    $before = (& git rev-parse HEAD).Trim()
    Remove-Item -Force -ErrorAction SilentlyContinue $statusFile
    $log = Join-Path $logDir ('{0:yyyyMMdd-HHmmss}-manager-round{1}.log' -f (Get-Date), $round)
    Write-Summary "round $round start (HEAD $($before.Substring(0,7))), log $log"

    if ($agentFlag) { & opencode run --agent cozmo-manager $task *>&1 | Tee-Object -FilePath $log }
    else { & opencode run $task *>&1 | Tee-Object -FilePath $log }

    $after = (& git rev-parse HEAD).Trim()
    $state = 'NONE'; $reason = 'no status file written'
    if (Test-Path $statusFile) {
        $lines = @(Get-Content $statusFile -TotalCount 2)
        if ($lines.Count -ge 1) { $state = $lines[0].Trim().ToUpperInvariant() }
        if ($lines.Count -ge 2) { $reason = $lines[1].Trim() }
    }
    Write-Summary "round $round end: $state - $reason (HEAD $($before.Substring(0,7)) -> $($after.Substring(0,7)))"

    if ($state -eq 'DONE') { Write-Summary 'manager reports DONE; stopping.'; break }
    if ($state -eq 'NEED_OPERATOR') { Write-Summary "manager needs the operator: $reason"; break }

    if ($after -eq $before -and $state -ne 'CONTINUE') { $idle++ } else { $idle = 0 }
    if ($idle -ge $MaxIdleRounds) { Write-Summary "$idle rounds in a row with no commit and no CONTINUE; stopping to avoid spending on no progress."; break }
}
Write-Summary 'runner end'
