using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// fidelity: M5-020
/// <summary>
/// The shipped app's named expressions, which are Code Lab's animation triggers, not faces.
///
/// The engine has no named-expression table: Cozmo's faces come from animation clips
/// (<c>SetFaceAction::SetFaceAction</c> 0x00563AFD takes an arbitrary <c>ProceduralFace const&amp;</c>), and the
/// only face the engine holds as a value is the resting face it loads from the shipped neutral-face animation,
/// <see cref="Expression.Neutral"/> (<see cref="ProceduralFacePose.ShippedNeutral"/>).
///
/// The named expressions the shipped app does have are Code Lab's: <c>CodeLabGame.GetAnimationTriggerForScratchIndex</c>
/// (unity/scripts/csharp/CodeLab/CodeLabGame.cs:3094) maps each block index to an <c>AnimationTrigger</c>, played
/// through the shipped animation-group path (the M5 group choice). The members below are those trigger names in
/// the switch's index order, with <see cref="Neutral"/> as index 0. The invented poses this type used to carry
/// (Happy, Sad, Angry, Surprised, Sleepy, Blinking, Squinting, LookingX) are removed: they were never Anki's
/// (operator decision 2026-09-29).
/// </summary>
public enum Expression
{
    /// <summary>The shipped neutral face, not a Code Lab trigger.</summary>
    Neutral = 0,

    CodeLabHappy = 1,
    CodeLabVictory = 2,
    CodeLabUnhappy = 3,
    CodeLabSurprise = 4,
    CodeLabDog = 5,
    CodeLabCat = 6,
    CodeLabSneeze = 7,
    CodeLabExcited = 8,
    CodeLabThinking = 9,
    CodeLabBored = 10,
    CodeLabFrustrated = 11,
    CodeLabChatty = 12,
    CodeLabDejected = 13,
    CodeLabSleep = 14,
    CodeLabReactHappy = 15,
    CodeLabCelebrate = 16,
    CodeLabTakaTaka = 17,
    CodeLabAmazed = 18,
    CodeLabCurious = 19,
    CodeLabYes = 20,
    CodeLabNo = 21,
    CodeLabIDK = 22,
    CodeLabConducting = 23,
    CodeLabDancingMambo = 24,
    CodeLabFireTruck = 25,
    CodeLabPartyTime = 26,
    CodeLabDizzy = 27,
    CodeLabDizzyEnd = 28,
    CodeLab123Go = 29,
    CodeLabWin = 30,
    CodeLabLose = 31,
    CodeLabTapCube = 32,
    CodeLabGetInPos = 33,
    CodeLabIdle = 34,
    CodeLabWondering = 35,
    CodeLabWhee1 = 36,
    CodeLabWhee2 = 37,
    CodeLabWhee3 = 38,
    CodeLabWhee4 = 39,
    CodeLabWhoa = 40,
    CodeLabWhew = 41,
    CodeLabStaring = 42,
    CodeLabHiccup = 43,
    CodeLabHelium = 44,
    CodeLabYuck = 45,
    CodeLabEnergyEat = 46,
    CodeLabHeadsUp = 47,
    CodeLabBlink = 48,
    CodeLabSquint1 = 49,
    CodeLabSquint2 = 50,
    CodeLabTwitch = 51,
    CodeLabZombie = 52,
    CodeLabVampire = 53,
    CodeLabGhoul = 54,
    CodeLabScaredCozmo = 55,
    CodeLabScaryCozmo = 56,
    CodeLabCow = 57,
    CodeLabRooster = 58,
    CodeLabFrog = 59,
    CodeLabSheep = 60,
    CodeLabDuck = 61,
    CodeLabTiger = 62,
    CodeLabElephant = 63,
    CodeLabChicken = 64,
    CodeLabRattleSnake = 65,
}

// fidelity: M5-020
/// <summary>
/// The Code Lab expression mapping, transliterated from
/// <c>CodeLabGame.GetAnimationTriggerForScratchIndex</c> (unity/scripts/csharp/CodeLab/CodeLabGame.cs:3094..3244).
/// </summary>
public static class Expressions
{
    /// <summary><c>AnimationTrigger.Count</c> (0x23F): the switch's default sentinel.</summary>
    private const AnimationTrigger Count = (AnimationTrigger)0x23F;

    /// <summary>
    /// The trigger an expression plays. <see cref="Expression.Neutral"/> is the shipped neutral face and has no
    /// trigger, so it returns null. Anything outside the Code Lab set throws rather than falling back to a trigger.
    /// </summary>
    public static AnimationTrigger? TriggerFor(Expression expression)
    {
        if (expression == Expression.Neutral) return null;
        var trigger = FromIndex((int)expression);
        if (trigger == Count)
            throw new ArgumentOutOfRangeException(nameof(expression), expression, "not a Code Lab expression");
        return trigger;
    }

    /// <summary>
    /// <c>GetAnimationTriggerForScratchIndex(index, isVertical)</c>. Index 0 draws from the global Unity stream:
    /// <c>Random.Range(1, 34)</c> for the vertical grammar, else <c>Random.Range(1, 14)</c> (CodeLabGame.cs:3096..3108).
    /// </summary>
    public static AnimationTrigger TriggerForIndex(int index, bool isVertical) =>
        TriggerForIndex(index, isVertical, UnityRandom.Shared);

    /// <summary>The same with an explicit stream, so a caller (or a test) can supply one.</summary>
    internal static AnimationTrigger TriggerForIndex(int index, bool isVertical, UnityRandom random)
    {
        if (index == 0)
            index = isVertical ? random.Range(1, 34) : random.Range(1, 14);
        return FromIndex(index);
    }

    /// <summary>The switch body (CodeLabGame.cs:3109..3244), index 1..65, default <c>AnimationTrigger.Count</c>.</summary>
    private static AnimationTrigger FromIndex(int index) => index switch
    {
        1 => AnimationTrigger.CodeLabHappy,
        2 => AnimationTrigger.CodeLabVictory,
        3 => AnimationTrigger.CodeLabUnhappy,
        4 => AnimationTrigger.CodeLabSurprise,
        5 => AnimationTrigger.CodeLabDog,
        6 => AnimationTrigger.CodeLabCat,
        7 => AnimationTrigger.CodeLabSneeze,
        8 => AnimationTrigger.CodeLabExcited,
        9 => AnimationTrigger.CodeLabThinking,
        10 => AnimationTrigger.CodeLabBored,
        11 => AnimationTrigger.CodeLabFrustrated,
        12 => AnimationTrigger.CodeLabChatty,
        13 => AnimationTrigger.CodeLabDejected,
        14 => AnimationTrigger.CodeLabSleep,
        15 => AnimationTrigger.CodeLabReactHappy,
        16 => AnimationTrigger.CodeLabCelebrate,
        17 => AnimationTrigger.CodeLabTakaTaka,
        18 => AnimationTrigger.CodeLabAmazed,
        19 => AnimationTrigger.CodeLabCurious,
        20 => AnimationTrigger.CodeLabYes,
        21 => AnimationTrigger.CodeLabNo,
        22 => AnimationTrigger.CodeLabIDK,
        23 => AnimationTrigger.CodeLabConducting,
        24 => AnimationTrigger.CodeLabDancingMambo,
        25 => AnimationTrigger.CodeLabFireTruck,
        26 => AnimationTrigger.CodeLabPartyTime,
        27 => AnimationTrigger.CodeLabDizzy,
        28 => AnimationTrigger.CodeLabDizzyEnd,
        29 => AnimationTrigger.CodeLab123Go,
        30 => AnimationTrigger.CodeLabWin,
        31 => AnimationTrigger.CodeLabLose,
        32 => AnimationTrigger.CodeLabTapCube,
        33 => AnimationTrigger.CodeLabGetInPos,
        34 => AnimationTrigger.CodeLabIdle,
        35 => AnimationTrigger.CodeLabWondering,
        36 => AnimationTrigger.CodeLabWhee1,
        37 => AnimationTrigger.CodeLabWhee2,
        38 => AnimationTrigger.CodeLabWhee3,
        39 => AnimationTrigger.CodeLabWhee4,
        40 => AnimationTrigger.CodeLabWhoa,
        41 => AnimationTrigger.CodeLabWhew,
        42 => AnimationTrigger.CodeLabStaring,
        43 => AnimationTrigger.CodeLabHiccup,
        44 => AnimationTrigger.CodeLabHelium,
        45 => AnimationTrigger.CodeLabYuck,
        46 => AnimationTrigger.CodeLabEnergyEat,
        47 => AnimationTrigger.CodeLabHeadsUp,
        48 => AnimationTrigger.CodeLabBlink,
        49 => AnimationTrigger.CodeLabSquint1,
        50 => AnimationTrigger.CodeLabSquint2,
        51 => AnimationTrigger.CodeLabTwitch,
        52 => AnimationTrigger.CodeLabZombie,
        53 => AnimationTrigger.CodeLabVampire,
        54 => AnimationTrigger.CodeLabGhoul,
        55 => AnimationTrigger.CodeLabScaredCozmo,
        56 => AnimationTrigger.CodeLabScaryCozmo,
        57 => AnimationTrigger.CodeLabCow,
        58 => AnimationTrigger.CodeLabRooster,
        59 => AnimationTrigger.CodeLabFrog,
        60 => AnimationTrigger.CodeLabSheep,
        61 => AnimationTrigger.CodeLabDuck,
        62 => AnimationTrigger.CodeLabTiger,
        63 => AnimationTrigger.CodeLabElephant,
        64 => AnimationTrigger.CodeLabChicken,
        65 => AnimationTrigger.CodeLabRattleSnake,
        _ => Count,
    };
}