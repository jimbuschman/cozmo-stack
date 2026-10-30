using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeBouncerGameTests
{
    [Fact]
    public void InitializationConsumesThreeRobotRngDrawsInOrder()
    {
        var rng = new EngineRandom(1);
        int words = 0;
        rng.WordSourceForTest = () => { words++; return words <= 2 ? 0u : 0x80000000u; };
        var game = new NativeBouncerGame();
        game.Initialize(128, 64, rng);
        Assert.Equal(6, words);
        Assert.Equal(64f, game.PaddleX);
        Assert.Equal(60f, game.PaddleY);
        Assert.Equal(64f, game.BallY);
        Assert.Equal(2f, game.VelocityX);
        Assert.Equal(-2f, game.VelocityY);
    }

    [Fact]
    public void PaddleMovesAtMostTenPerUpdateAndBallReflectsAtCenterWalls()
    {
        var rng = new EngineRandom(1) { WordSourceForTest = () => 0u };
        var game = new NativeBouncerGame();
        game.Initialize(128, 64, rng);
        game.UpdatePaddle(1f);
        Assert.Equal(54f, game.PaddleX);
        for (int i = 0; i < 64; i++) game.UpdateBall();
        Assert.Equal(128f, game.BallX);
        Assert.Equal(0f, game.BallY);
        Assert.Equal(-1f, game.VelocityX);
        Assert.Equal(1f, game.VelocityY);
        Assert.Equal(0u, game.PlayerScore);
        Assert.Equal(0u, game.OpponentScore);
    }
    [Fact]
    public void BottomContactScoresAndMissesCollapseIntoOnePendingSound()
    {
        var rng = new EngineRandom(1) { WordSourceForTest = () => 0u };
        var game = new NativeBouncerGame();
        game.Initialize(128, 64, rng);
        for (int i = 0; i < 128; i++) game.UpdateBall();
        Assert.Equal(1u, game.PlayerScore);
        Assert.False(game.PendingSound);
        for (int i = 0; i < 7; i++) game.UpdatePaddle(1f);
        for (int i = 0; i < 128; i++) game.UpdateBall();
        Assert.Equal(1u, game.OpponentScore);
        Assert.True(game.PendingSound);
        game.ClearPendingSound();
        Assert.False(game.PendingSound);
    }
}
