<#
.SYNOPSIS
  Decompiles a shipped native library with Ghidra, headless: one pseudo-C file per function plus index.tsv.

.DESCRIPTION
  Step 1 imports and auto-analyses the library into a Ghidra project (skipped when the project already holds
  it). Step 2 exports every function with ExportDecomp.java. A problem in the export therefore never costs the
  analysis again.

  Output goes to re-analysis/decomp/<library>/, which is gitignored: it is derived from the proprietary binary.
  The decompilation is a navigation aid for extractors and verifiers, NOT evidence. Citations point at the
  instructions.

  Needs Ghidra 12.x and a JDK 21 (Ghidra's minimum). Give their folders, or set GHIDRA_INSTALL_DIR and
  JAVA_HOME.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File re-analysis\tools\ghidra\decompile.ps1 -Ghidra C:\tools\ghidra_12.1.4_PUBLIC -Jdk C:\tools\jdk-21.0.12.1+1
#>
param(
    [string]$Library = 'resources\lib\armeabi-v7a\libcozmoEngine.so',
    [string]$Ghidra = $env:GHIDRA_INSTALL_DIR,
    [string]$Jdk = $env:JAVA_HOME,
    [string]$ProjectDir = (Join-Path $env:USERPROFILE 'tools\ghidra-projects'),
    [string]$MaxMem = '10G'
)

# 'Continue', not 'Stop': in Windows PowerShell 5.1 a native command's stderr, redirected, would otherwise abort the run.
$ErrorActionPreference = 'Continue'
$root = (& git rev-parse --show-toplevel).Trim()
$lib = Resolve-Path (Join-Path $root $Library)
$name = [IO.Path]::GetFileNameWithoutExtension($lib)
$out = Join-Path $root "re-analysis\decomp\$name"
$scripts = Join-Path $root 're-analysis\tools\ghidra'
$headless = Join-Path $Ghidra 'support\analyzeHeadless.bat'
if (-not (Test-Path $headless)) { throw "analyzeHeadless.bat not found under $Ghidra" }
if (-not (Test-Path (Join-Path $Jdk 'bin\java.exe'))) { throw "no JDK at $Jdk" }

$env:JAVA_HOME = $Jdk
$env:PATH = "$Jdk\bin;$env:PATH"
$env:GHIDRA_HEADLESS_MAXMEM = $MaxMem
New-Item -ItemType Directory -Force -Path $ProjectDir, $out | Out-Null
$project = "cozmo-$name"
$logDir = Join-Path $ProjectDir 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# Ghidra's launcher is a batch file and breaks on paths with parentheses (such as a checkout under a
# "...(armeabi-v7a)(nodpi)..." folder), so the library, the script and the output are staged under the project
# directory, and the result is moved into the repo at the end.
if ($ProjectDir -match '[()]') { throw "the project directory must not contain parentheses: $ProjectDir" }
$stage = Join-Path $ProjectDir "stage-$name"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$stagedLib = Join-Path $stage ([IO.Path]::GetFileName($lib))
Copy-Item -Force $lib $stagedLib
Copy-Item -Force (Join-Path $scripts 'ExportDecomp.java') $stage
$stagedOut = Join-Path $stage 'out'
if (Test-Path $stagedOut) { Remove-Item -Recurse -Force $stagedOut }

$rep = Join-Path $ProjectDir "$project.rep"
if (-not (Test-Path $rep)) {
    "step 1: import and analyse $lib (this is the long part)"
    & $headless $ProjectDir $project -import $stagedLib -log (Join-Path $logDir "$name-analyze.log") 2>&1 |
        Tee-Object -FilePath (Join-Path $logDir "$name-analyze.out")
} else {
    "step 1: project $project exists; analysis skipped"
}

"step 2: export every function to $out"
& $headless $ProjectDir $project -process ([IO.Path]::GetFileName($lib)) -noanalysis -readOnly `
    -scriptPath $stage -postScript ExportDecomp.java $stagedOut -log (Join-Path $logDir "$name-export.log") 2>&1 |
    Tee-Object -FilePath (Join-Path $logDir "$name-export.out")
if (-not (Test-Path (Join-Path $stagedOut 'index.tsv'))) { throw "the export wrote no index; see $logDir" }
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
Move-Item $stagedOut $out
"done: $out (index: $out\index.tsv)"
