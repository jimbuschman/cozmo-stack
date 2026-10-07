### Reopened reaction-lock table census

Native companion `20261007-q17-lock-tables-native.txt`. Each row reads all42
bytes as21(enum-byte,bool-byte) pairs; every enum ordinal is checked0..20.
Mask columns list stored boolean bytes, not an inferred trigger set.
IActivity sites are M15 owners; their table values are supplied interface data.
Use the caller's own name/scope and install point, not a blanket arbiter mask.

| Caller | Install call | Static table | Bool bytes in trigger ordinal order |
|---|---|---|---|
| IBehavior::Init | 0x005BCD44 | 0x00C65F90 | 000000001000000000000 |
| IBehavior::Resume | 0x005BCFDE | 0x00C65F90 | 000000001000000000000 |
| BehaviorDriveOffCharger::InitInternal | 0x005C0B2A | 0x00C672F0 | 111001001100000000001 |
| BehaviorKnockOverCubes::InitializeMemberVars | 0x005C31FA | 0x00C67CB2 | 010001001000000000000 |
| BehaviorKnockOverCubes::PrepareForKnockOverAttempt | 0x005C37F0 | 0x00C67CDC | 000000000000000000000 |
| BehaviorPopAWheelie::SetupRetryAction | 0x005C7BE8 | 0x00C6883D | 110100000001101100001 |
| BehaviorPutDownBlock::InitInternal | 0x005C7FE2 | 0x00C68B20 | 000000000000000000000 |
| BehaviorFeedingEat::TransitionToEating | 0x005D6144 | 0x00C6AD18 | 100000000001111000000 |
| BehaviorCubeLiftWorkout::InitInternal | 0x005D7EC8 | 0x00C6B7E0 | 011000001010000000001 |
| BehaviorBuildPyramid::TransitionToPlacingTopBlock | 0x005DC0D6 | 0x00C6C691 | 000000001000000000000 |
| BehaviorRequestGameSimple::RequestGame_InitInternal | 0x005EA6E2 | 0x00C6E820 | 011100001010000000000 |
| BehaviorRequestGameSimple::TransitionToPlayingRequstAnim | 0x005EBF26 | 0x00C6E820 | 011100001010000000000 |
| BehaviorDance::InitInternal | 0x005ED47C | 0x00C6F1C8 | 011100001010000000001 |
| BehaviorSinging::InitInternal | 0x005EEB5E | 0x00C6F590 | 011000001010000000001 |
| BehaviorBouncer::InitInternal | 0x005F14B8 | 0x00C6FB90 | 011111001010000000001 |
| BehaviorFistBump::InitInternal | 0x005F1EE6 | 0x00C6FDC8 | 011000001011101000001 |
| BehaviorGuardDog::InitInternal | 0x005F2D0E | 0x00C6FF06 | 011111001010000000001 |
| BehaviorPeekABoo::InitInternal | 0x005F67A8 | 0x00C70960 | 011100001010000000000 |
| BehaviorEnrollFace::InitInternal | 0x005FD10A | 0x00C71B6C | 111111001001101111001 |
| BehaviorOnboardingShowCube::InitInternal | 0x00600CA8 | 0x00C72454 | 011011001010000000000 |
| BehaviorAcknowledgeCubeMoved::InitInternal | 0x00602242 | 0x00C72BB2 | 000000001000000000000 |
| BehaviorRamIntoBlock::TransitionToRammingIntoBlock | 0x00604A44 | 0x00C73520 | 101000001000000000001 |
| BehaviorReactToCliff::InitInternal | 0x00604DA0 | 0x00C73746 | 011000001000000000001 |
| BehaviorReactToImpact::InitInternal | 0x00606216 | 0x00C73D36 | 111111111011111111011 |
| BehaviorReactToMotorCalibration::InitInternal | 0x00606642 | 0x00C74032 | 100001000000101111000 |
| BehaviorReactToOnCharger::InitInternal | 0x00606CB0 | 0x00C74182 | 011111011010000000011 |
| BehaviorReactToPlacedOnSlope::InitInternal | 0x00607FFE | 0x00C745E0 | 100000000000101000000 |
| BehaviorReactToRobotShaken::InitInternal | 0x00609144 | 0x00C750E0 | 111111001011111111001 |
| ActivityFeeding::OnSelectedInternal | 0x005AAE1E | 0x00C61DA0 | 011111001010000000010 |
| ActivityFeeding::SetupSevereAnims | 0x005AB482 | 0x00C61DD4 | 011111011011101111010 |
| ActivitySparked::OnSelectedInternal | 0x005B16A8 | 0x00C624BC | 011111000010000000000 |
| ActivitySparked::CheckIfSparkShouldEnd | 0x005B199C | 0x00C624E6 | 011010001000000000000 |
