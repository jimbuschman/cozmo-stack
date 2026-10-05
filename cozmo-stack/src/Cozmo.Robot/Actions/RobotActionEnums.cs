namespace Cozmo.Robot;

/// <summary>
/// The shipped <c>RobotActionType</c> enum tables (20261004-actionlist-extraction.md, "Enum tables"):
/// <c>EnumToString&lt;RobotActionType&gt;</c> 0x0075A430, table base 0x01032560, index = value + 2, 54 entries, and
/// the forward map <c>RobotActionTypeFromString</c> 0x0075A448, which misses to -2 (COMPOUND).
///
/// This is a shipped table, vendored exactly: the value-to-name pairs are the row's, in index order
/// (-2 .. 51). The inverse returns null out of range; the forward map is the exact inverse and returns the
/// COMPOUND miss fallback for a name it does not hold.
/// </summary>
// fidelity: M7-020
internal static class RobotActionType
{
    /// <summary>Index = value + 2 (0x01032560). -2 COMPOUND .. 51 WAIT_FOR_LAMBDA.</summary>
    private static readonly string[] Names =
    {
        "COMPOUND",                                  // -2
        "UNKNOWN",                                   // -1
        "ALIGN_WITH_OBJECT",                         // 0
        "ASCEND_OR_DESCEND_RAMP",                    // 1
        "CALIBRATE_MOTORS",                          // 2
        "CROSS_BRIDGE",                              // 3
        "DEVICE_AUDIO",                              // 4
        "DISPLAY_FACE_IMAGE",                        // 5
        "DISPLAY_PROCEDURAL_FACE",                   // 6
        "DRIVE_OFF_CHARGER_CONTACTS",                // 7
        "DRIVE_STRAIGHT",                            // 8
        "DRIVE_TO_FLIP_BLOCK_POSE",                  // 9
        "DRIVE_TO_OBJECT",                           // 10
        "DRIVE_PATH",                                // 11
        "DRIVE_TO_POSE",                             // 12
        "DRIVE_TO_PLACE_CARRIED_OBJECT",             // 13
        "FACE_PLANT",                                // 14
        "FLIP_BLOCK",                                // 15
        "HANG",                                      // 16
        "MOUNT_CHARGER",                             // 17
        "MOVE_HEAD_TO_ANGLE",                        // 18
        "MOVE_LIFT_TO_HEIGHT",                       // 19
        "PAN_AND_TILT",                              // 20
        "PICK_AND_PLACE_INCOMPLETE",                 // 21
        "PICKUP_OBJECT_LOW",                         // 22
        "PICKUP_OBJECT_HIGH",                        // 23
        "PLACE_OBJECT_LOW",                          // 24
        "PLACE_OBJECT_HIGH",                         // 25
        "PLAY_ANIMATION",                            // 26
        "PLAY_ANIMATION_DRONE_MODE_CLIFF_EVENT",     // 27
        "PLAY_CUBE_ANIMATION",                       // 28
        "POP_A_WHEELIE",                             // 29
        "READ_TOOL_CODE",                            // 30
        "ROLL_OBJECT_LOW",                           // 31
        "SAY_TEXT",                                  // 32
        "SEARCH_FOR_NEARBY_OBJECT",                  // 33
        "TRACK_OBJECT",                              // 34
        "TRACK_FACE",                                // 35
        "TRACK_GROUND_POINT",                        // 36
        "TRACK_MOTION",                              // 37
        "TRACK_PET_FACE",                            // 38
        "TRAVERSE_OBJECT",                           // 39
        "TURN_IN_PLACE",                             // 40
        "TURN_TOWARDS_FACE",                         // 41
        "TURN_TOWARDS_IMAGE_POINT",                  // 42
        "TURN_TOWARDS_LAST_FACE_POSE",               // 43
        "TURN_TOWARDS_OBJECT",                       // 44
        "TURN_TOWARDS_POSE",                         // 45
        "VISUALLY_VERIFY_OBJECT",                    // 46
        "VISUALLY_VERIFY_FACE",                      // 47
        "VISUALLY_VERIFY_NO_OBJECT_AT_POSE",         // 48
        "WAIT",                                      // 49
        "WAIT_FOR_IMAGES",                           // 50
        "WAIT_FOR_LAMBDA",                           // 51
    };

    /// <summary>0x0075A430 inverse table: the name at index value + 2, or null out of range.</summary>
    public static string? NameOf(int value)
    {
        int index = value + 2;
        return index >= 0 && index < Names.Length ? Names[index] : null;
    }

    /// <summary>0x0075A448 forward map: the value whose name matches, else the COMPOUND miss fallback -2.</summary>
    public static int FromString(string name)
    {
        for (int i = 0; i < Names.Length; i++)
            if (string.Equals(Names[i], name, StringComparison.Ordinal)) return i - 2;
        return -2;      // 0x0075A448 miss fallback: COMPOUND
    }
}

/// <summary>
/// The shipped <c>ActionResultCategory</c> enum tables (20261004-actionlist-extraction.md, "Enum tables"):
/// <c>EnumToString&lt;ActionResultCategory&gt;</c> 0x00757E28, table base 0x01032540, index = value, 5 entries, and
/// the forward map <c>ActionResultCategoryFromString</c> 0x00757E40, which misses to 0 (SUCCESS).
///
/// The inverse returns null out of range; the forward map is the exact inverse and returns the SUCCESS miss
/// fallback for a name it does not hold.
/// </summary>
// fidelity: M7-020
internal static class ActionResultCategory
{
    /// <summary>Index = value (0x01032540). 0 SUCCESS .. 4 RETRY.</summary>
    private static readonly string[] Names =
    {
        "SUCCESS",      // 0
        "RUNNING",      // 1
        "CANCELLED",    // 2
        "ABORT",        // 3
        "RETRY",        // 4
    };

    /// <summary>0x00757E28 inverse table: the name at index value, or null out of range.</summary>
    public static string? NameOf(int value) =>
        value >= 0 && value < Names.Length ? Names[value] : null;

    /// <summary>0x00757E40 forward map: the value whose name matches, else the SUCCESS miss fallback 0.</summary>
    public static int FromString(string name)
    {
        for (int i = 0; i < Names.Length; i++)
            if (string.Equals(Names[i], name, StringComparison.Ordinal)) return i;
        return 0;       // 0x00757E40 miss fallback: SUCCESS
    }
}