param([Parameter(Mandatory=$true)][string]$Bundle)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$inputDirectory=(Resolve-Path -LiteralPath $Bundle).Path
if(!(Test-Path -LiteralPath (Join-Path $inputDirectory 'messages.jsonl'))){throw 'Bundle has no messages.jsonl'}
$previous=$env:COZMO_CHARGER_REPLAY_BUNDLE
try {
    $env:COZMO_CHARGER_REPLAY_BUNDLE=$inputDirectory
    # Private recording input only; the tests use an in-memory robot transport.
    # One case keeps the recorded pair's native failure. The other explicitly
    # models a stationary repeated frame and ideal firmware motion/contact replies.
    & dotnet test (Join-Path $repo 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj') --no-restore --disable-build-servers -m:1 --filter 'FullyQualifiedName~ChargerMountRecordedReplayTests' --logger 'console;verbosity=normal'
    $result=$LASTEXITCODE
} finally {
    $env:COZMO_CHARGER_REPLAY_BUNDLE=$previous
}
exit $result
