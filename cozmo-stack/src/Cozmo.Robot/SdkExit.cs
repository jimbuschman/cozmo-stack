namespace Cozmo.Robot;

// fidelity: M1-025
// Manager-checked E1–E14. These are external-interface handoffs, not robot messages.
internal sealed record SdkExitHandoff(string Name, object? Payload = null);

internal sealed class SdkCommunicationConnection
{
    internal byte Enabled;
    internal Action<byte, bool>? Changed;
}

public sealed partial class CozmoEngine
{
    internal Action<bool>? NeedsSetPaused;
    internal Action? MovementExitSdkMode;
    internal Action<SdkExitHandoff>? SdkExitExternalInterface;
    internal Action<string>? SdkTelemetry;
    // The writers in EnterSdkMode and the concrete SDK connection callbacks are not
    // recovered by the checked exit rows. Keep their state and interfaces distinct.
    internal bool SdkExternalMode, SdkEduMode, SdkWasConnected;
    internal bool SdkResetOnDisconnect = true, SdkResetBlockPool;
    internal byte SdkCommunicationEnabled;
    internal readonly SdkCommunicationConnection?[] SdkConnections = new SdkCommunicationConnection?[3];

    /// <summary>ExitSdkMode (game tag 0xF2): UI, MessageHandler, then the per-Robot Movement subscriber.</summary>
    public void ExitSdkMode(bool isExternalSdkMode, bool isEduMode) => Post(() =>
    {
        // 0x00661546: edu gates only NeedsManager's unpause.
        if (!isEduMode)
        {
            if (NeedsSetPaused is { } unpause) unpause(false);
            else Log("MISSING: ExitSdkMode NeedsManager.SetPaused(false) 0x00661552");
        }
        ExitSdkStatus(isExternalSdkMode);
        UpdateSdkCommunication();
        // 0x0069E1DC: this subscriber uses only the external byte.
        if (isExternalSdkMode)
        {
            Rcm.Reason = RobotDisconnectReason.ExitSDKMode;
            Rcm.DisconnectCurrent();
        }
        if (Robot is not null) MovementExitSdkMode?.Invoke();
    });

    private void SdkHandoff(string name, object? payload = null)
    {
        if (SdkExitExternalInterface is { } emit) emit(new(name, payload));
        else Log($"MISSING: SDK external-interface recipient {name}");
    }

    private void SdkEvent(string name)
    {
        if (SdkTelemetry is { } emit) emit(name);
        else Log($"MISSING: SDK telemetry fields for {name}");
    }

    private void ExitSdkStatus(bool external)
    {
        // 0x0065E2B2..E2E2: retain the other mode, including its early return.
        if (external) SdkExternalMode = false;
        else
        {
            SdkEduMode = false;
            if (SdkExternalMode) return;
        }
        if (SdkEduMode) return;
        SdkHandoff("RemoveIdleAnimation", "sdk_mode_obfusc8te");
        if (SdkWasConnected)
        {
            if (SdkExternalMode) SdkEvent("robot.sdk_connection_ended");
            if (SdkResetOnDisconnect) ResetSdkRobot();
            if (SdkResetBlockPool) SdkHandoff("BlockPoolResetMessage", new byte[] { 0, 1 });
            SdkWasConnected = false;
        }
        if (external)
        {
            SdkEvent("robot.sdk_mode_off");
        }
    }

    private void ResetSdkRobot()
    {
        // 0x0065DCFC..DF98: synchronous handoffs in construction order. The nested
        // handlers are PARTIAL and are deliberately not replaced with device resets.
        SdkHandoff("RemoveDisableReactionsLock", "sdk");
        SdkHandoff("ActivateHighLevelActivity", (byte)1);
        if (SdkExternalMode)
        {
            SdkHandoff("EnableCubeSleep", new byte[] { 1, 1 });
            SdkHandoff("EnableLightStates", (false, -1));
        }
        SdkHandoff("SetCameraSettings", (true, (ushort)0, BitConverter.Int32BitsToSingle(0x00000000)));
        SdkHandoff("EnableColorImages", false);
        SdkHandoff("UndefineAllCustomMarkerObjects");
        SdkHandoff("DeleteAllCustomObjects");
        SdkHandoff("StopRobotForSdk");
        SdkHandoff("EnableLiftPower", true);
    }

    private void UpdateSdkCommunication()
    {
        // 0x00663298..C4: +0xE1 is a separate byte; do not derive it from mode flags.
        byte enabled = SdkCommunicationEnabled;
        for (int i = 1; i < 3; i++)
        {
            if (SdkConnections[i] is not { } connection) continue;
            byte old = connection.Enabled;
            connection.Enabled = enabled;
            if (connection.Changed is { } changed) changed(old, enabled != 0);
            else Log("MISSING: SDK connection virtual slot +0x24 0x0065FE24");
        }
    }
}
