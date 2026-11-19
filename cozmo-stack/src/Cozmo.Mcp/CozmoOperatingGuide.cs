namespace Cozmo.Mcp;

// Host documentation only; embedded so clients read the guide matching the running binary.
internal static class CozmoOperatingGuide
{
    internal const string ResourceUri = "cozmo://guide";
    internal static readonly string Markdown = Read();

    private static string Read()
    {
        using var stream = typeof(CozmoOperatingGuide).Assembly.GetManifestResourceStream("Cozmo.Mcp.CozmoGuide.md")
            ?? throw new InvalidOperationException("Embedded Cozmo operating guide is unavailable.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
