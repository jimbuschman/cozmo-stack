using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

// BOUNCER-1 checked own arithmetic only. This unit is not a configured runner:
// live face +0xDC, OpenCV raster and SetFaceAction remain shared MISSING paths.
internal sealed class NativeBouncerGame
{
    internal float Width { get; private set; }
    internal float Height { get; private set; }
    internal float PaddleX { get; private set; }
    internal float PaddleY { get; private set; }
    internal float BallX { get; private set; }
    internal float BallY { get; private set; }
    internal float VelocityX { get; private set; }
    internal float VelocityY { get; private set; }
    internal uint PlayerScore { get; private set; }
    internal uint OpponentScore { get; private set; }
    internal bool PendingSound { get; private set; }

    // 005F14CE..005F15AE; caller passes the actual robot RNG.
    internal void Initialize(uint width, uint height, EngineRandom random)
    {
        Width = width; Height = height;
        PlayerScore = OpponentScore = 0;
        PaddleX = Width / 2f; PaddleY = Height - 4f;
        BallX = Width / 2f; BallY = Height;
        double sign = random.RandDbl(1d);
        float horizontal = (float)random.RandDblInRange(1d, 3d);
        VelocityX = horizontal * (sign < 0.5d ? 1f : -1f);
        VelocityY = -(float)random.RandDblInRange(1d, 3d);
        PendingSound = false;
    }

    // 005F15C4..005F167C: radians supplied by the real tracked face, not a guessed angle.
    internal void UpdatePaddle(float radians)
    {
        float degrees = radians * BitConverter.Int32BitsToSingle(0x42652EE1);
        float factor = degrees <= -20f ? 1f : 1f + (MathF.Min(degrees, 20f) + 20f) / -40f;
        float desired = Width * factor;
        if (MathF.Abs(desired - PaddleX) < 10f) PaddleX = desired;
        else PaddleX += desired >= PaddleX ? 10f : -10f;
        PaddleX = Math.Clamp(PaddleX, 0f, Width);
    }

    // 005F1690..005F1790: center reflection and strict 16-pixel score threshold.
    internal void UpdateBall()
    {
        float x = BallX + VelocityX, y = BallY + VelocityY;
        if (x <= 0f) { VelocityX = -VelocityX; x = -x; }
        else if (x >= Width) { VelocityX = -VelocityX; x = 2f * Width - x; }
        if (y <= 0f) { VelocityY = -VelocityY; y = -y; }
        else if (y >= Height)
        {
            if (MathF.Abs(x - PaddleX) < 16f) PlayerScore++;
            else { OpponentScore++; PendingSound = true; }
            VelocityY = -VelocityY; y = 2f * Height - y;
        }
        BallX = Math.Clamp(x, 0f, Width); BallY = Math.Clamp(y, 0f, Height);
    }

    // 005F1B30..005F1B48: only the action owner may consume this flag before dispatch.
    internal void ClearPendingSound() => PendingSound = false;
}
