param([Parameter(Mandatory=$true)][string]$Bundle)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$inputDirectory=(Resolve-Path -LiteralPath $Bundle).Path
foreach($name in @('events.jsonl','messages.jsonl')) {
    if(!(Test-Path -LiteralPath (Join-Path $inputDirectory $name))){throw "Bundle has no $name"}
}
$previous=$env:COZMO_NARRATION_REPLAY_BUNDLE
try {
    $env:COZMO_NARRATION_REPLAY_BUNDLE=$inputDirectory
    # Display replay of the recorded pickup window; no robot connection.
    & dotnet test (Join-Path $repo 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj') --no-restore --disable-build-servers -m:1 --filter 'FullyQualifiedName~LiveNarrationRecordedReplayTests' --logger 'console;verbosity=normal'
    $result=$LASTEXITCODE
} finally {$env:COZMO_NARRATION_REPLAY_BUNDLE=$previous}
exit $result
