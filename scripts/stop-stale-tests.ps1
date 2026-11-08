# Stop a test run of this clone that outlived its script (operator, 2026-11-07).
# When the shell running test-full.sh is killed (for example by a low-memory reaper), `dotnet test` and its MSBuild, vstest and
# testhost processes keep running; the next run then stacks on top of it. This finds testhost/vstest processes whose command
# line names this clone, walks up to the topmost dotnet.exe ancestor (the `dotnet test` itself) and stops that whole tree.
# Only this clone's path is matched, so other clones' runs are left alone.
param([Parameter(Mandatory = $true)][string]$Root)

$all = @(Get-CimInstance Win32_Process)
$byId = @{}
foreach ($p in $all) { $byId[[int]$p.ProcessId] = $p }
$roots = @{}
foreach ($p in $all) {
    $cmd = [string]$p.CommandLine
    if ($p.Name -ne 'testhost.exe' -and -not ($p.Name -eq 'dotnet.exe' -and $cmd -like '*vstest.console.dll*')) { continue }
    if ($cmd.IndexOf($Root, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
    $top = $p
    while ($byId.ContainsKey([int]$top.ParentProcessId) -and $byId[[int]$top.ParentProcessId].Name -in @('dotnet.exe', 'testhost.exe')) {
        $top = $byId[[int]$top.ParentProcessId]
    }
    $roots[[int]$top.ProcessId] = $top
}
foreach ($id in $roots.Keys) {
    Write-Output ("test-full: stopping a leftover test run of this clone (pid {0})" -f $id)
    & taskkill.exe /PID $id /T /F | Out-Null
}
