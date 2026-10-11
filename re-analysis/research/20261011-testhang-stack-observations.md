# Test-host baseline stack observations (2026-10-11)

No post-pass hang reproduced. These snapshots were taken while tests were still running, before the passing result and process exit. They do not establish the cause of the operator's earlier non-exit.

Baseline: 4245/4245 passed and exited with blame-hang-timeout 15m / dump-type full; 256.507329 seconds wall time including build and host startup. The collector produced no hang dump.

## .scratch/b4-live-stacks.txt

Full snapshot retained in the local ignored scratch directory. Relevant excerpts:

```text
  System.Private.CoreLib.il!System.Threading.Tasks.Task.InternalWaitCore(int32,value class System.Threading.CancellationToken)
  System.Private.CoreLib.il!System.Threading.Tasks.Task.Wait(int32,value class System.Threading.CancellationToken)
  System.Private.CoreLib.il!System.Threading.Tasks.Task.Wait()
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.DefaultEngineInvoker.Invoke(class System.Collections.Generic.IDictionary`2<class System.String,class System.String>)
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.Program.Run(class System.String[],class Microsoft.VisualStudio.TestPlatform.Execution.UiLanguageOverride)
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.Program.Run(class System.String[])
  Cozmo.Robot!Cozmo.Robot.Vision.MarkerDetector.Detect(class Cozmo.Robot.Vision.GrayImage,unsigned int32)
  Cozmo.Robot!Cozmo.Robot.Vision.VisionSystem.ProcessImage(class Cozmo.Robot.Vision.GrayImage,unsigned int32,unsigned int32,value class Cozmo.Robot.Vision.VisionPoseData,class Cozmo.Robot.Vision.CameraCalibration,int32,value class System.Nullable`1<unsigned int32>,bool)
  Cozmo.Robot!Cozmo.Robot.Vision.VisionSystem.ProcessImage(class Cozmo.Robot.Vision.GrayImage,unsigned int32,unsigned int32,value class Cozmo.Robot.Vision.VisionPoseData)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Frame(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Observe(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests.M11_044_AStationaryRobotLocalizesToAUsableCubeOnceAndThenRefreshesItEveryFrame()
  Cozmo.Robot!Cozmo.Robot.Vision.VisionSystem.ProcessImage(class Cozmo.Robot.Vision.GrayImage,unsigned int32,unsigned int32,value class Cozmo.Robot.Vision.VisionPoseData,class Cozmo.Robot.Vision.CameraCalibration,int32,value class System.Nullable`1<unsigned int32>,bool)
  Cozmo.Robot!Cozmo.Robot.Vision.VisionSystem.ProcessImage(class Cozmo.Robot.Vision.GrayImage,unsigned int32,unsigned int32,value class Cozmo.Robot.Vision.VisionPoseData)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Frame(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Observe(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests.M11_044_AStationaryRobotLocalizesToAUsableCubeOnceAndThenRefreshesItEveryFrame()
  ?!?
  Cozmo.Robot!Cozmo.Robot.Vision.VisionSystem.ProcessImage(class Cozmo.Robot.Vision.GrayImage,unsigned int32,unsigned int32,value class Cozmo.Robot.Vision.VisionPoseData)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Frame(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests+ImageRig.Observe(value class System.ValueTuple`2<value class Cozmo.Protocol.ObjectType,value class Cozmo.Robot.Vision.Pose3d>[])
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.M11RVisBuildTests.M11_044_AStationaryRobotLocalizesToAUsableCubeOnceAndThenRefreshesItEveryFrame()
  ?!?
  System.Private.CoreLib.il!System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(class System.Object,value class System.Reflection.BindingFlags)
```

## .scratch/testhang-baseline-stacks.txt

Full snapshot retained in the local ignored scratch directory. Relevant excerpts:

```text
  System.Private.CoreLib.il!System.Threading.Tasks.Task.InternalWaitCore(int32,value class System.Threading.CancellationToken)
  System.Private.CoreLib.il!System.Threading.Tasks.Task.Wait(int32,value class System.Threading.CancellationToken)
  System.Private.CoreLib.il!System.Threading.Tasks.Task.Wait()
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.DefaultEngineInvoker.Invoke(class System.Collections.Generic.IDictionary`2<class System.String,class System.String>)
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.Program.Run(class System.String[],class Microsoft.VisualStudio.TestPlatform.Execution.UiLanguageOverride)
  testhost!Microsoft.VisualStudio.TestPlatform.TestHost.Program.Run(class System.String[])
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketInverse(class DecoderState,class Cozmo.Robot.Animation.Wwise.WwiseVorbisMappingSetup,class Cozmo.Robot.Animation.Wwise.BitReader)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketEntry(class DecoderState,value class System.ReadOnlyMemory`1<unsigned int8>,bool)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisFraming.FrameLoopAB7E40(class Cozmo.Robot.Animation.Wwise.WwiseVorbisFrameBlock,unsigned int16,value class Cozmo.Robot.Animation.Wwise.WwiseBytePtr,class System.Func`1<bool>)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests.DrivePerPacket(class Cozmo.Protocol.Tests.EngineMedia,class Cozmo.Robot.Animation.Wwise.WwiseCodebookLibrary,bool)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests+<>c__DisplayClass27_0.<V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine>b__0(value class System.ValueTuple`2<int32,unsigned int8[]>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass32_0`2[System.ValueTuple`2[System.Int32,System.__Canon],System.__Canon].<ForEachWorker>b__0(int32)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketEntry(class DecoderState,value class System.ReadOnlyMemory`1<unsigned int8>,bool)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisFraming.FrameLoopAB7E40(class Cozmo.Robot.Animation.Wwise.WwiseVorbisFrameBlock,unsigned int16,value class Cozmo.Robot.Animation.Wwise.WwiseBytePtr,class System.Func`1<bool>)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests.DrivePerPacket(class Cozmo.Protocol.Tests.EngineMedia,class Cozmo.Robot.Animation.Wwise.WwiseCodebookLibrary,bool)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests+<>c__DisplayClass27_0.<V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine>b__0(value class System.ValueTuple`2<int32,unsigned int8[]>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass32_0`2[System.ValueTuple`2[System.Int32,System.__Canon],System.__Canon].<ForEachWorker>b__0(int32)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass19_0`2[System.__Canon,System.Int32].<ForWorker>b__1(value class System.Threading.Tasks.RangeWorker&,int64,bool&)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel.ForEachWorker(class System.Collections.Generic.IList`1<!!0>,class System.Threading.Tasks.ParallelOptions,class System.Action`1<!!0>,class System.Action`2<!!0,class System.Threading.Tasks.ParallelLoopState>,class System.Action`3<!!0,class System.Threading.Tasks.ParallelLoopState,int64>,class System.Func`4<!!0,class System.Threading.Tasks.ParallelLoopState,!!1,!!1>,class System.Func`5<!!0,class System.Threading.Tasks.ParallelLoopState,int64,!!1,!!1>,class System.Func`1<!!1>,class System.Action`1<!!1>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel.ForEachWorker(class System.Collections.Generic.IEnumerable`1<!!0>,class System.Threading.Tasks.ParallelOptions,class System.Action`1<!!0>,class System.Action`2<!!0,class System.Threading.Tasks.ParallelLoopState>,class System.Action`3<!!0,class System.Threading.Tasks.ParallelLoopState,int64>,class System.Func`4<!!0,class System.Threading.Tasks.ParallelLoopState,!!1,!!1>,class System.Func`5<!!0,class System.Threading.Tasks.ParallelLoopState,int64,!!1,!!1>,class System.Func`1<!!1>,class System.Action`1<!!1>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel.ForEach(class System.Collections.Generic.IEnumerable`1<!!0>,class System.Threading.Tasks.ParallelOptions,class System.Action`1<!!0>)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests.V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine()
  ?!?
  System.Private.CoreLib.il!System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(class System.Object,value class System.Reflection.BindingFlags)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketInverse(class DecoderState,class Cozmo.Robot.Animation.Wwise.WwiseVorbisMappingSetup,class Cozmo.Robot.Animation.Wwise.BitReader)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketEntry(class DecoderState,value class System.ReadOnlyMemory`1<unsigned int8>,bool)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisFraming.FrameLoopAB7E40(class Cozmo.Robot.Animation.Wwise.WwiseVorbisFrameBlock,unsigned int16,value class Cozmo.Robot.Animation.Wwise.WwiseBytePtr,class System.Func`1<bool>)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests.DrivePerPacket(class Cozmo.Protocol.Tests.EngineMedia,class Cozmo.Robot.Animation.Wwise.WwiseCodebookLibrary,bool)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests+<>c__DisplayClass27_0.<V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine>b__0(value class System.ValueTuple`2<int32,unsigned int8[]>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass32_0`2[System.ValueTuple`2[System.Int32,System.__Canon],System.__Canon].<ForEachWorker>b__0(int32)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisNative.PacketEntry(class DecoderState,value class System.ReadOnlyMemory`1<unsigned int8>,bool)
  Cozmo.Robot!Cozmo.Robot.Animation.Wwise.WwiseVorbisFraming.FrameLoopAB7E40(class Cozmo.Robot.Animation.Wwise.WwiseVorbisFrameBlock,unsigned int16,value class Cozmo.Robot.Animation.Wwise.WwiseBytePtr,class System.Func`1<bool>)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests.DrivePerPacket(class Cozmo.Protocol.Tests.EngineMedia,class Cozmo.Robot.Animation.Wwise.WwiseCodebookLibrary,bool)
  Cozmo.Protocol.Tests!Cozmo.Protocol.Tests.WwiseVorbisEngineTests+<>c__DisplayClass27_0.<V02_TheCensusOfEveryShippedVorbisMediumMatchesTheEngine>b__0(value class System.ValueTuple`2<int32,unsigned int8[]>)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass32_0`2[System.ValueTuple`2[System.Int32,System.__Canon],System.__Canon].<ForEachWorker>b__0(int32)
  System.Threading.Tasks.Parallel.il!System.Threading.Tasks.Parallel+<>c__DisplayClass19_0`2[System.__Canon,System.Int32].<ForWorker>b__1(value class System.Threading.Tasks.RangeWorker&,int64,bool&)
```

Other waiting workers include ReliableTransport.ReceiveLoop, SerialExecutor.Run and TransportScheduler.Loop. An active vision test and later the emulator-driven Vorbis census were visible. No stuck foreground application thread after a passing suite was captured.

Changes for the lifetime candidates and the final after-run timing are in jobs/status/B-TESTHANG.md. No manifest or engine-rendering behavior change is claimed.
