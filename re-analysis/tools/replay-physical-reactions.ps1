param([Parameter(Mandatory=$true)][string]$Bundle)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$inputDirectory=(Resolve-Path -LiteralPath $Bundle).Path
if(!(Test-Path -LiteralPath (Join-Path $inputDirectory 'messages.jsonl'))){throw 'Bundle has no messages.jsonl'}
$previous=$env:COZMO_REACTIONS_REPLAY_BUNDLE
try {
    $env:COZMO_REACTIONS_REPLAY_BUNDLE=$inputDirectory
    # In-memory transport only. See the tests/handoff for captured lock context,
    # ideal consumption feedback and actual new-tag start/end acknowledgements.
    & dotnet test (Join-Path $repo 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj') --no-restore --disable-build-servers -m:1 --filter 'FullyQualifiedName~PhysicalReactionRecordedReplayTests' --logger 'console;verbosity=normal'
    $result=$LASTEXITCODE
} finally {$env:COZMO_REACTIONS_REPLAY_BUNDLE=$previous}
exit $result
