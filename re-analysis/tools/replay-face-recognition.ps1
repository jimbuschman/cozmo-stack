param([Parameter(Mandatory=$true)][string]$Bundle)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$inputDirectory=(Resolve-Path -LiteralPath $Bundle).Path
if(!(Test-Path -LiteralPath (Join-Path $inputDirectory 'messages.jsonl'))){throw 'Bundle has no messages.jsonl'}
$previous=$env:COZMO_FACE_REPLAY_BUNDLE
try {
    $env:COZMO_FACE_REPLAY_BUNDLE=$inputDirectory
    # In-memory robot transport only. Private frames/albums are read from the
    # bundle and never written to the repository or sent to a physical robot.
    & dotnet test (Join-Path $repo 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj') --no-restore --disable-build-servers -m:1 --filter 'FullyQualifiedName~FaceRecognitionRecordedReplayTests' --logger 'console;verbosity=normal'
    $result=$LASTEXITCODE
} finally {
    $env:COZMO_FACE_REPLAY_BUNDLE=$previous
}
exit $result
