namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The names behind the 32-bit ids, from the shipped bank definition text files.
///
/// Every bank ships with a <c>.txt</c> beside it inside <c>AudioAssets.zip</c> (Cozmo.txt, Music.txt,
/// Init.txt, SFX.txt, UI.txt, Dev_Debug.txt): tab-separated tables headed <c>Event</c>, <c>Switch Group</c>,
/// <c>Switch</c>, <c>State Group</c>, <c>State</c>, <c>Game Parameter</c>, <c>Audio Bus</c> and the plug-in
/// lists, each row an id, a name and for switches and states the group they belong to. These are Wwise's
/// own exports and the same ids the engine's <c>Anki::AudioMetaData</c> enums carry, so a
/// <c>"audioSwitchGroup": "Cozmo_Sings_80Bpm"</c> in a behaviour file can be turned into the id the bank
/// uses without hashing (<see cref="WwiseHash"/> gives the same answer, which is the cross-check).
/// </summary>
public sealed class WwiseNames
{
    private readonly Dictionary<uint, string> _switchGroups = new();
    private readonly Dictionary<uint, (string Name, string Group)> _switches = new();
    private readonly Dictionary<uint, string> _stateGroups = new();
    private readonly Dictionary<uint, (string Name, string Group)> _states = new();
    private readonly Dictionary<uint, string> _gameParameters = new();
    private readonly Dictionary<uint, string> _buses = new();
    private readonly Dictionary<uint, string> _events = new();
    private readonly Dictionary<uint, string> _effects = new();
    private readonly Dictionary<uint, string> _modulators = new();

    public IReadOnlyDictionary<uint, string> SwitchGroups => _switchGroups;
    public IReadOnlyDictionary<uint, (string Name, string Group)> Switches => _switches;
    public IReadOnlyDictionary<uint, string> StateGroups => _stateGroups;
    public IReadOnlyDictionary<uint, (string Name, string Group)> States => _states;
    public IReadOnlyDictionary<uint, string> GameParameters => _gameParameters;
    public IReadOnlyDictionary<uint, string> Buses => _buses;
    /// <summary>Event names from the text files; SoundbanksInfo.xml names the same events.</summary>
    public IReadOnlyDictionary<uint, string> Events => _events;
    /// <summary>Effect share sets and custom instances, from the "Effect plug-ins" tables.</summary>
    public IReadOnlyDictionary<uint, string> Effects => _effects;
    /// <summary>LFO and envelope modulators, from the "Modulator LFO" and "Modulator Envelope" tables.</summary>
    public IReadOnlyDictionary<uint, string> Modulators => _modulators;

    /// <summary>How many rows of any kind were read.</summary>
    public int Count => _switchGroups.Count + _switches.Count + _stateGroups.Count + _states.Count + _gameParameters.Count + _buses.Count + _events.Count + _effects.Count + _modulators.Count;

    /// <summary>The id of a switch group or switch by name, case-insensitively, or null.</summary>
    public uint? SwitchGroupId(string name) => Find(_switchGroups, name);
    public uint? SwitchId(string name) => Find(_switches.ToDictionary(kv => kv.Key, kv => kv.Value.Name), name);
    public uint? GameParameterId(string name) => Find(_gameParameters, name);

    /// <summary>A name for any id known here, whatever kind it is, or null.</summary>
    public string? NameOf(uint id)
    {
        if (_switchGroups.TryGetValue(id, out var a)) return a;
        if (_switches.TryGetValue(id, out var b)) return b.Name;
        if (_stateGroups.TryGetValue(id, out var c)) return c;
        if (_states.TryGetValue(id, out var d)) return d.Name;
        if (_gameParameters.TryGetValue(id, out var e)) return e;
        if (_buses.TryGetValue(id, out var f)) return f;
        if (_events.TryGetValue(id, out var g)) return g;
        if (_effects.TryGetValue(id, out var h)) return h;
        if (_modulators.TryGetValue(id, out var i)) return i;
        return null;
    }

    private static uint? Find(IReadOnlyDictionary<uint, string> table, string name)
    {
        foreach (var (id, n) in table)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return id;
        return null;
    }

    /// <summary>Adds the rows of one bank definition file. Unknown sections are ignored.</summary>
    public void AddDefinitionText(string text)
    {
        string? section = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (line[0] != '\t')
            {
                int tab = line.IndexOf('\t');
                section = tab > 0 ? line[..tab] : line;
                continue;
            }
            var cells = line.Split('\t');
            if (cells.Length < 3 || !uint.TryParse(cells[1], out var id)) continue;
            string name = cells[2];
            string group = cells.Length > 3 ? cells[3] : "";
            switch (section)
            {
                case "Event": _events[id] = name; break;
                case "Switch Group": _switchGroups[id] = name; break;
                case "Switch": _switches[id] = (name, group); break;
                case "State Group": _stateGroups[id] = name; break;
                case "State": _states[id] = (name, group); break;
                case "Game Parameter": _gameParameters[id] = name; break;
                case "Audio Bus": _buses[id] = name; break;
                case "Effect plug-ins": _effects[id] = name; break;
                case "Modulator LFO":
                case "Modulator Envelope": _modulators[id] = name; break;
            }
        }
    }
}
