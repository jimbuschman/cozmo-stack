# Records the test run's memory every 5 s: testhost private MB, all dotnet private MB, and system free MB, so a
# low-memory kill can be traced to the tests that were running (operator, 2026-11-09).
param([string]$Out, [string]$Log)
# It stops when <Out>.stop appears (written by test-full.sh on exit) or after 90 minutes.
$deadline = (Get-Date).AddMinutes(90)
while (-not (Test-Path "$Out.stop") -and (Get-Date) -lt $deadline) {
  $os = Get-CimInstance Win32_OperatingSystem
  $th = (Get-Process testhost -ErrorAction SilentlyContinue | Measure-Object PrivateMemorySize64 -Sum).Sum
  $dn = (Get-Process dotnet,VBCSCompiler,MSBuild -ErrorAction SilentlyContinue | Measure-Object PrivateMemorySize64 -Sum).Sum
  $last = ""
  try {
    $fs = [IO.File]::Open($Log, 'Open', 'Read', 'ReadWrite')
    if ($fs.Length -gt 4096) { [void]$fs.Seek(-4096, 'End') }
    $tail = (New-Object IO.StreamReader($fs)).ReadToEnd(); $fs.Dispose()
    $last = ($tail -split "`n" | Where-Object { $_ -match '^\s+(Passed|Failed|Skipped) ' } | Select-Object -Last 1).Trim()
  } catch {}
  "{0:HH:mm:ss} testhost={1} build+dotnet={2} free={3} last={4}" -f (Get-Date), [int]($th/1MB), [int]($dn/1MB), [int]($os.FreePhysicalMemory/1KB), $last | Add-Content -Path $Out
  Start-Sleep -Seconds 5
}
