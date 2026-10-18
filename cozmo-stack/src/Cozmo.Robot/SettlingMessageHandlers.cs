using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M2-024 — adopted OV127 check; no reachable Ramp instance, so no on-ramp state transition.
public sealed partial class EngineRobot
{
    private void InitSettlingMessageHandlers()
    {
        AddMessage(RobotMessageId.PrintText,m=>
            Engine.EmitStackLog($"ROBOT-PRINT ({NativeRobotId}): {((PrintText)m).Field1.Split('\0')[0]}"));
        AddMessage(RobotMessageId.RobotPoked,_=>
        {
            Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","Robot.HandleRobotPoked","");
            Engine.BroadcastRobotPoked();
        });
        AddMessage(RobotMessageId.RampTraverseStarted,_=>
        {
            LogTraversal("started","ramp");
            Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.SetOnRamp.NoRampWithID",
                $"Robot {NativeRobotId} is transitioning on/off of a ramp, but Ramp object with ID=-1 not found in the world");
        });
        AddMessage(RobotMessageId.RampTraverseCompleted,_=>LogTraversal("completed","ramp"));
        AddMessage(RobotMessageId.BridgeTraverseStarted,_=>LogTraversal("started","bridge"));
        AddMessage(RobotMessageId.BridgeTraverseCompleted,_=>LogTraversal("completed","bridge"));
        AddMessage(RobotMessageId.MainCycleTimeError,m=>
        {
            var e=(MainCycleTimeError)m;
            if(e.Field0!=0)Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.MainCycleTooLong",
                $" {NativeRobotId} Num errors: {e.Field0}, Avg time: {e.Field3} us");
            if(e.Field2!=0)Engine.EmitSourceLog(EngineLogLevel.Warning,null,"Robot.MainCycleTooLate",
                $"{NativeRobotId} Num errors: {e.Field2}, Avg time: {e.Field1} us");
        });
        AddMessage(RobotMessageId.DataDump,m=>
        {
            var data=((DataDump)m).Field1;
            Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","RobotMessageHandler.ProcessMessage.MessageDataDump",
                $"ID: {NativeRobotId}, size: {data.Length}, data: {Convert.ToHexString(data).ToLowerInvariant()}");
        });
        // B3 GoalPose is omitted: DrawPreDockPose reaches bx lr SendMessage6BE610 / SendVizMessage6C05EC.
    }

    private void LogTraversal(string step,string surface)=>Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed",
        "RobotMessageHandler.ProcessMessage",$"Robot {NativeRobotId} reported it {step} traversing a {surface}.");
}
