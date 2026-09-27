<#
.SYNOPSIS
  Extracts the upcoming layers ahead of the manager, one read-only extractor run per layer.

.DESCRIPTION
  For each subsystem in -Layers, runs the cozmo-extractor once (`opencode run`). The extractor writes
  re-analysis/research/<date>-<subsystem>-extraction.md. A layer whose report already exists is skipped, so
  the script can be stopped and restarted. The extractor only reads and writes that report, so this runs
  alongside the manager without collisions. The manager checks and approves each report when it reaches that
  layer.

  Create a file named STOP at the repo root to stop after the current layer. Logs go to .scratch/runner/.
  Set a credit limit on your OpenRouter key: that is the spending cap this script can't exceed.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\run-extractions.ps1

.EXAMPLE
  Two windows in parallel, splitting the list:
  powershell -ExecutionPolicy Bypass -File scripts\run-extractions.ps1 -Layers M11-vision,M12-manipulation,M13-navigation,M14-faces
  powershell -ExecutionPolicy Bypass -File scripts\run-extractions.ps1 -Layers M8-framework,M7-behaviour,M9-wwise-music,M15-freeplay
#>
param(
    [string[]]$Layers = @('M11-vision', 'M12-manipulation', 'M13-navigation', 'M14-faces',
                          'M8-framework', 'M7-behaviour', 'M9-wwise-music', 'M15-freeplay')
)

# 'Continue', not 'Stop': in Windows PowerShell 5.1 a native command's stderr, redirected, would otherwise abort the run.
$ErrorActionPreference = 'Continue'
$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root

$logDir = Join-Path $root '.scratch\runner'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$research = Join-Path $root 're-analysis\research'
$stopFile = Join-Path $root 'STOP'
$summary = Join-Path $logDir 'extraction-summary.txt'
$help = (& opencode run --help 2>&1 | Out-String)
$agentFlag = $help -match '--agent'

function Write-Summary([string]$line) {
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$stamp  $line" | Tee-Object -FilePath $summary -Append
}

Write-Summary "extraction runner start: $($Layers -join ', ') (agent flag: $agentFlag)"
foreach ($layer in $Layers) {
    if (Test-Path $stopFile) { Write-Summary 'STOP file found; stopping.'; break }

    $existing = @(Get-ChildItem -Path $research -Filter "*-$layer-extraction.md" -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) { Write-Summary "$layer : report exists ($($existing[0].Name)); skipped"; continue }

    $report = "re-analysis/research/{0:yyyyMMdd}-$layer-extraction.md" -f (Get-Date)
    $task = @"
Extraction for subsystem $layer, ahead of the manager. Scope: the whole production path of this subsystem in resources/lib/armeabi-v7a/libcozmoEngine.so (and the Unity code and shipped assets where the engine uses them), from its entry points to the robot or to its consumers.
Check every existing record of subsystem $layer in re-analysis/fidelity_manifest.json: say for each whether its evidence is confirmed, too weak, or contradicted, with the citation. Look for NEW steps no record covers.
If re-analysis/inventory/$layer.md exists, it is an earlier inventory: check it the same way, and don't copy it.
Leads you may check but never cite as evidence: the archive/m11-research-package git tag (M11 only), and the memory notes and docs in the repo.
Produce the report your instructions describe. Your edit and write tools are disabled, so write the report to $report with a shell command (for example python). Use .scratch/$layer/ for your own scripts. Then return the report in full as your final message.
"@
    if (-not $agentFlag) { $task = 'Read .opencode/agent/cozmo-extractor.md and act only as that extractor. ' + $task }

    $log = Join-Path $logDir ('{0:yyyyMMdd-HHmmss}-extract-{1}.log' -f (Get-Date), $layer)
    Write-Summary "$layer : start, log $log"
    if ($agentFlag) { & opencode run --agent cozmo-extractor $task *>&1 | Tee-Object -FilePath $log }
    else { & opencode run $task *>&1 | Tee-Object -FilePath $log }

    if (Test-Path (Join-Path $root $report)) { Write-Summary "$layer : report written ($report)" }
    else { Write-Summary "$layer : NO report file; the full output is in $log" }
}
Write-Summary 'extraction runner end'
