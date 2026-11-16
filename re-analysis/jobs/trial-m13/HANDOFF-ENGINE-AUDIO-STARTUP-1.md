# ENGINE-AUDIO-STARTUP-1

CozmoContext constructs the audio controller and wraps it in the multiplexer at `0x004EA84C–0x004EA87C`. The resource path is `sound/` at `0x00592C3A–0x00592C52`. The controller initializes Wwise at `0x005933CC`, sets up the plugins at `0x00593478` and builds the InitScene list in this order.

| Bank | Constructor address |
| --- | --- |
| Init.bnk | `0x00593484` |
| Music.bnk | `0x0059349C` |
| UI.bnk | `0x005934B6` |
| SFX.bnk | `0x005934CE` |
| Cozmo.bnk | `0x005934E8` |
| Dev_Debug.bnk | `0x00593500` |

`0x005935E2` registers InitScene and `0x005935EA` loads it. `0x008D1E3E` attaches the archives. The loop at `0x008D2F08–0x008D2F18` loads each bank in order and exits on the first failure. The scene load precedes CLAD object registration at `0x005935F0` and the separate connection handler at `0x004ED020–0x004ED07C`. The stack uses the existing archive reader and native Wwise bank loader with the shipped English bank under `English(US)`. Startup joins the context worker before accepting a connection. An absent or unusable scene reports MISSING M6-024 and leaves the engine running.

PlayOnRobot selects object 7 at `0x0059A09E–0x0059A0B6`. The existing authored keyframe selection follows `0x0059687E–0x00596914`. The robot route uses the Hijack bus and zero dry output from `0x0059962A–0x0059966A`. The streamer uses its native audio state machine and existing 744-byte frame path. The shared controller tail runs music, render and callback delivery in that order at `0x008D2928–0x008D2942`. Freeplay posts on object 1 through `0x0058FBB4`, and canned and direct speech use the same context worker and its float-input generations. A replacement releases the incumbent animation generation before acquiring speech, with the existing dispatch retirement fence. The context retires the worker after the robot at shutdown. Fidelity statuses are unchanged.

Behavior, Reactions and the M15, M5 and M3/M4 hardware checks use the startup banks. Enrollment and InteractWithFaces find the shared audio device from the resource path under M14-006. The public English speech action still uses the configured locale for text synthesis. Engine-owned Freeplay uses the context audio by default. The standalone legacy FreeplayTool still overrides its renderer for SingingBehavior, which depends on its switch controls and song prewarm. The explicit anim and singing renderers remain available. The Windows PCM device is still required by the existing Wwise platform adapter. For the robot check, play anim_pause_idle_01 with the anim command and its --name option, using the default engine audio, then try speech or Freeplay. The commands script's CozmoSaysSpeakLoop round is a silent face animation. Robot verification and the Claude binary spot-check are pending.

The full suite passed all 8,502 tests on the startup implementation. After integrating the latest hardware options and resource-only device admission, all 45 focused tests passed on the final diff. The offline test records non-silent 744-byte robot audio from anim_pause_idle_01 and then direct speech through the same startup worker. The Freeplay activity test verifies native Wwise calls in client order on this worker. Fidelity validation passed all 762 records. Commands, skills and personality validated 17, 19 and 13 records respectively without opening a network connection.
