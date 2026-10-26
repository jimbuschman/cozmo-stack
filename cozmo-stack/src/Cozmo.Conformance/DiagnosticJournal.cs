namespace Cozmo.Conformance;

/// <summary>Shared multi-producer tool diagnostics; snapshots never enumerate a live list.</summary>
public sealed class DiagnosticJournal<T>
{
    private readonly object _gate = new();
    private readonly List<T> _entries = new();
    private readonly TextWriter _output;

    public DiagnosticJournal(TextWriter? output = null) => _output = output ?? Console.Out;

    public void Write(T entry, string consoleLine)
    {
        lock (_gate)
        {
            _output.WriteLine(consoleLine);
            _entries.Add(entry);
        }
    }

    public T[] Snapshot()
    {
        lock (_gate) return _entries.ToArray();
    }
}
