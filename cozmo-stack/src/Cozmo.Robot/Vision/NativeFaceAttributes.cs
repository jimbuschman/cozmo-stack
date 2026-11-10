namespace Cozmo.Robot.Vision;

internal sealed record NativeFaceAttributes(IReadOnlyList<byte> Scores, FaceAttributePair Smile, FaceAttributePair Gaze, FaceAttributePair Blink)
{
    internal static NativeFaceAttributes Default { get; } = new(Array.AsReadOnly(new byte[5]), default, default, default);
    internal FacialExpression Expression
    {
        get
        {
            int best=0;
            for(int i=1;i<Scores.Count;i++)if(Scores[i]>Scores[best])best=i;
            return (FacialExpression)best;
        }
    }
}
