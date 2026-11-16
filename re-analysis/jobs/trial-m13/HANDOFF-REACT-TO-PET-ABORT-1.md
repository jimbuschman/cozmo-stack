# REACT-TO-PET-ABORT-1

The Freeplay control capture reports the switch from Needs_MildLowPlayRequest to ReactToPet at 85822.80 ms. TurnTowardsImagePoint completed successfully. At 86836 ms the next child chose `anim_petdetection_dog_03_head_angle_40`, then logged `MISSING: M14-006 ordinary authored audio requires the shared device, banks and archive`. The parent completed at 86838.89 ms with `0x03000000` and child results `[0, 0x03000000]`. The failed child was the animation.

PetWorld had a dog-classified target. Replaying the reconstructed frames through the shipped detector and camera worker reproduced a false positive on the person in view. The two phases of the shipped every-other-frame schedule acquired Dog on images 30281 and 30282, with scores 958 and 991 and rectangle (100, 64, 96, 96) over the person's face. Subsequent frames retained that tracked result. The replay used an offline pose history and kept its images local.

- The reaction strategy at `0x00611AE4..0x00611CB6` supplies PetWorld targets. The behavior fetches the chosen pet at `0x00606FD8` and selects the animation trigger at `0x00607042` through `0x00607498`. It builds turn, trigger animation, and tracking children at `0x006070D4..0x00607124`, adds them in that order at `0x00607132..0x00607178` and queues at `0x00607192`.
- The engine's trigger action resolves the clip and calls PlayAnimation Init at `0x0054446E..0x00544494`. Main already loads the shared authored audio device during engine startup, using the bank sequence at `0x00593484..0x00593500` from ENGINE-AUDIO-STARTUP-1. The regression uses only ResourcesPath for audio configuration, takes the configured dog reaction through its authored animation, verifies non-silent robot audio samples, and reaches the tracking child.
- The detector classification follows the shipped detector. The M14-004 reaction remains source-backed. Robot confirmation of the restored audio is pending.

Verification passed 43 focused tests, both local camera replay phases, the 762-record fidelity check and the no-robot Freeplay control dry run.
