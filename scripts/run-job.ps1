<#
.SYNOPSIS
  Runs one job from re-analysis/jobs/ with opencode, unattended, until the job's status says it is finished.

.DESCRIPTION
  Each round is one `opencode run` with the job's agent (cozmo-extractor for X jobs, cozmo-manager for B jobs).
  The job keeps its progress in its reports and its status file, re-analysis/jobs/status/<job>.md, so a new
  round carries on where the last stopped.

  The runner stops when:
    - the status starts with DONE, BLOCKED or WAITING;
    - -MaxRounds rounds have run;
    - -MaxIdleRounds rounds in a row changed nothing (no new commit, the same status);
    - a file named STOP exists at the repo root (create it to stop after the current round).

  Logs go to .scratch/runner/. The OpenRouter (or other provider) credit limit is the spending cap.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\run-job.ps1 -Job X3
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\run-job.ps1 -Job X3 -Then X4
#>
param(
    [Parameter(Mandatory = $true)][string]$Job,
    [string[]]$Then = @(),
    [int]$MaxRounds = 25,
    [int]$MaxIdleRounds = 3
)

# 'Continue', not 'Stop': in Windows PowerShell 5.1 a native command's stderr, redirected, would otherwise abort the run.
$ErrorActionPreference = 'Continue'
$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root
$logDir = Join-Path $root '.scratch\runner'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stopFile = Join-Path $root 'STOP'
$summary = Join-Path $logDir 'jobs-summary.txt'
$help = (& opencode run --help 2>&1 | Out-String)
$agentFlag = $help -match '--agent'

function Write-Summary([string]$line) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $line" | Tee-Object -FilePath $summary -Append
}
function Get-Status([string]$id) {
    $f = Join-Path $root "re-analysis\jobs\status\$id.md"
    if (Test-Path $f) { return ((Get-Content $f -TotalCount 1) -join '').Trim() } else { return '' }
}

foreach ($id in @($Job) + $Then) {
    $jobFile = "re-analysis/jobs/$id.md"
    if (-not (Test-Path (Join-Path $root $jobFile))) { Write-Summary "$id : no job file $jobFile; skipped"; continue }
    $agent = if ($id -like 'B*') { 'cozmo-manager' } else { 'cozmo-extractor' }
    $task = "Do job $id. Follow $jobFile exactly, including claiming it, committing and pushing only your own files, and setting its status file when you finish or are blocked. If the job is already claimed by you, carry on from its reports and status."
    if (-not $agentFlag) { $task = "Read .opencode/agent/$agent.md and act as that agent. " + $task }

    $idle = 0
    for ($round = 1; $round -le $MaxRounds; $round++) {
        if (Test-Path $stopFile) { Write-Summary 'STOP file found; stopping.'; return }
        & git pull --rebase --quiet 2>&1 | Out-Null
        $status = Get-Status $id
        if ($status -match '^(DONE|BLOCKED|WAITING)') { Write-Summary "$id : $status"; break }

        $before = (& git rev-parse HEAD).Trim()
        $log = Join-Path $logDir ('{0:yyyyMMdd-HHmmss}-{1}-round{2}.log' -f (Get-Date), $id, $round)
        Write-Summary "$id : round $round start ($agent), log $log"
        if ($agentFlag) { & opencode run --agent $agent $task *>&1 | Tee-Object -FilePath $log }
        else { & opencode run $task *>&1 | Tee-Object -FilePath $log }

        $after = (& git rev-parse HEAD).Trim()
        $newStatus = Get-Status $id
        Write-Summary "$id : round $round end: '$newStatus' (HEAD $($before.Substring(0,7)) -> $($after.Substring(0,7)))"
        if ($newStatus -match '^(DONE|BLOCKED|WAITING)') { break }
        if ($after -eq $before -and $newStatus -eq $status) { $idle++ } else { $idle = 0 }
        if ($idle -ge $MaxIdleRounds) { Write-Summary "$id : $idle rounds with no progress; stopping this job."; break }
    }
}
Write-Summary 'run-job end'
