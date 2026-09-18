# M4 — physical control layer

Status: **COMPLETE and FROZEN** (2026-09-18). Sensors, lights, head and lift motion and wheel drive all
passed on hardware. Cubes are code-complete and offline-tested with hardware acceptance pending, only
because no cube was available. See `ACCEPTANCE.md`.

Everything here sits above the frozen M1 transport and M2 protocol and the M3 device layer. Nothing in
those was reopened, and no generated code was hand-edited.

```
CozmoRobot
  .WaitUntilReadyAsync()   telemetry + animation controller + motor calibration, or why not
  .EmergencyStop()         unconditional, never gated on anything

  .Motion    DriveWheelsAsync  StopWheelsAsync  SetHeadAngleAsync  SetLiftHeightAsync
             MoveHead  MoveLift  StopAllAsync
  .Lights    SetBackpack  BlinkBackpack  BackpackOff  SetHeadlight
  .Sensors   BatteryVolts  OnCharger  Charging  PickedUp  Falling  CliffDetectedNow
             CliffSensorsRaw  Accelerometer  Gyroscope  HeadAngleRad  LiftPositionRaw
             WheelSpeedsMmps  WheelsMoving  SetStopOnCliff  RequestImuBurst  WaitForCliffAsync
  .Cubes     SetDiscovery  DiscoverAsync  DiscoveredCubes  ConnectedCubes
             ByFactoryId  ByObjectId  WaitForConnectionAsync
```

## Success is what the robot said, not what we sent

This was the main design constraint, and it shapes every method.

| Capability | How success is established |
| --- | --- |
| Head and lift positioning | The command carries an action id and the robot answers `MotorActionAck` with the same id. A different id does not satisfy the wait. |
| Wheels | No acknowledgement exists, so it is confirmed against the wheel speeds and the `ARE_WHEELS_MOVING` flag in the state stream. |
| Stop all | Confirmed against the robot reporting the wheels stopped, not against the send. |
| Cliff | The robot's own `CliffEvent`, including which sensors tripped and whether it stopped itself. |
| Battery, charger, IMU, pose | Read from `RobotState`, which arrives about thirty times a second. |
| Cube discovery and connection | `ObjectAvailable` and `ObjectConnectionState` from the robot. |
| Backpack LEDs and headlight | **Nothing.** The robot reports neither, so the API records what it was told to do and the acceptance command says the verdict is a person's. |

A call that is not confirmed returns `TimedOut` rather than success, and says the command was sent but the
outcome is unknown. A call made before the robot is ready returns `Refused` and **sends nothing**.

## Calibration gate

The robot recalibrates head and lift on every connect and fights motion commands while that runs. Every
motion method refuses until `MotorCalibration` has been seen and has finished, unless explicitly overridden.
`StopAllAsync` and `EmergencyStop` are the exceptions: stopping must work whatever state the robot is in.
`Dispose` stops the motors before dropping the link, so leaving a `using` block never leaves the robot driving.

## Uncertainties preserved, not guessed

These are surfaced with their ambiguity intact rather than converted into something that reads cleanly:

* **`liftAngle`.** The engine's field is called an angle; PyCozmo treats it as a height in millimetres.
  Which it is has not been established, so `Sensors.LiftPositionRaw` passes it through unconverted and is
  deliberately not named after either unit. The setter takes millimetres, which is what the engine's own
  `SetLiftHeight.height_mm` field says.
* **Accelerometer and gyroscope units.** Not established. At rest the accelerometer magnitude is about 9800,
  which suggests millimetres per second squared, but nothing confirms it. Reported as raw floats.
* **Cliff sensor identity.** The four raw readings arrive in a fixed order but which physical corner each
  one is has not been established, so `CliffSensors` names them `Sensor0` to `Sensor3` rather than guessing
  at front-left and so on. The threshold the firmware uses is also unknown.
* **Cube battery level.** A single byte on an unknown scale, kept as `BatteryLevelRaw`.
* **`IS_CHARGER_OOS`.** Surfaced as `ChargerOutOfSpec` with no interpretation.
* **Head and lift limits.** Taken from PyCozmo, not confirmed against the engine. Values out of range are
  clamped rather than sent, so a bad limit cannot become a bad command.
* **Wheel speed ceiling.** 200 mm/s is documented by PyCozmo. The robot clamps rather than faulting, so
  this is advisory.

## Tests

177 pass, of which 27 are new. They run against an offline robot fed synthetic or captured traffic, so none
of them need hardware.

* **Motion** — refused before any state and while calibrating; nothing is sent in either case. An action
  waits for its own acknowledgement and ignores another action's. An unacknowledged action times out rather
  than reporting success. Action ids are distinct and never zero. Angles and heights are clamped. Driving is
  confirmed from reported wheel speeds. Stop waits for the wheels to be reported stopped. Stop is not gated
  on calibration.
* **Lights** — colour packing, the message actually emitted, blink timing carried by the robot rather than a
  loop, hex parsing and rejection.
* **Sensors** — every field read through from a reported state, cliff events decoded to which sensors
  tripped, picked-up and charger events raised only on an actual change.
* **Cubes** — advertisement then connection tracked as one cube, telemetry landing on the right cube,
  telemetry for an unknown cube ignored rather than inventing one.
* **Readiness** — reports exactly what it is still waiting for.
* **Replay** — the whole control layer driven by the 20 s firmware-2457 capture, asserting only what the
  robot actually reported.

The test rig ticks the offline connection and acknowledges what it sends, because the engine spaces packets
2 ms apart and batches rather than sending immediately. A queued message does not reach the wire until the
connection is ticked, which a live transport does on its own thread.

## Hardware acceptance

One command per capability group. All but `cubes` have passed on hardware; see `ACCEPTANCE.md`.

```
dotnet run --project src/Cozmo.Conformance -- drive   172.31.1.1 --acceptance
dotnet run --project src/Cozmo.Conformance -- drive   172.31.1.1 --allow-drive --acceptance
dotnet run --project src/Cozmo.Conformance -- lights  172.31.1.1 --acceptance
dotnet run --project src/Cozmo.Conformance -- sensors 172.31.1.1 --acceptance
dotnet run --project src/Cozmo.Conformance -- cubes   172.31.1.1 --acceptance
```

`drive` moves only the head and lift unless `--allow-drive` is given, and enables the robot's stop-on-cliff
reflex before anything moves. Every command writes an acceptance record separating what was measured from
what a person still has to watch.

## Out of scope, deliberately

Animations and animation groups, navigation, mapping, docking, vision, behaviours, speech and cube lights.
Cube support here is discovery, connection state and basic telemetry only.
