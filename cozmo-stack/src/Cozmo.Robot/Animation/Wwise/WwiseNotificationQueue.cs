// fidelity: M6-025, M6-026
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One item of the PBI-notification queue <c>Q</c> (<c>0x108DE78</c>): the 0x14-byte block <c>{next, pbi, code, r2, r3}</c> (<c>0xA38644..0xA38670</c>).</summary>
public sealed class WwiseNotificationItem
{
    internal WwiseNotificationItem(int id, bool inArray, uint address)
    {
        Id = id;
        InArray = inArray;
        Address = address;
    }

    /// <summary>The item's identity: the index in the preallocated array, or the array capacity plus the allocation sequence number for an item allocated at push time (an observation point).</summary>
    public int Id { get; }

    /// <summary>True for an item inside the preallocated array <c>[Q+0x1C]</c> (<c>0xA38540..0xA385B0</c>): it goes back to the free list; any other item is freed with <c>0xA7A988</c>.</summary>
    internal bool InArray { get; }

    /// <summary>The heap address of an allocated item (<c>0xA7A7F4</c>); 0 for an array item.</summary>
    internal uint Address { get; }

    /// <summary><c>[item+0]</c>: the next item in the queue or in the free list.</summary>
    public WwiseNotificationItem? Next { get; internal set; }

    /// <summary><c>[item+4]</c>: the PBI.</summary>
    public object? Pbi { get; internal set; }

    /// <summary><c>[item+8]</c>: the message code (4 is Term).</summary>
    public int Code { get; internal set; }

    /// <summary><c>[item+0xC]</c>.</summary>
    public int R2 { get; internal set; }

    /// <summary><c>[item+0x10]</c>.</summary>
    public int R3 { get; internal set; }
}

/// <summary>
/// The PBI-notification queue <c>Q</c> at <c>0x108DE78</c> and its push <c>0xA38600(pbi, code, r2, r3)</c> (C34.3 S8): <c>+4</c> head, <c>+8</c> tail, <c>+0xC</c> free-list head, <c>+0x10</c> the preallocated array's item count,
/// <c>+0x14</c> the limit, <c>+0x18</c> the count, <c>+0x1C</c> the array base. The initial contents (the array, the order of its free list, the limit) are written by an init no row reads: the host supplies them. The flush
/// <c>0xA38420</c> is <see cref="WwiseVoiceBusPass.FlushPbiNotifications"/>, which removes items with <see cref="PopHeadA38518"/>. Replaces the old <c>Queue&lt;WwisePbiNotification&gt;</c> the voice pass kept.
/// </summary>
public sealed class WwiseNotificationQueue
{
    private readonly WwiseBankMemory _memory;
    private readonly int _arrayCapacity;
    private int _allocated;

    /// <param name="memory">The default pool (<c>[0x1052418]</c>) the push allocates items from (<c>0xA7A7F4(pool, 0x14)</c>) and the flush frees them to (<c>0xA7A988</c>).</param>
    /// <param name="arrayCapacity"><c>[Q+0x10]</c>: the number of items in the preallocated array.</param>
    /// <param name="limit"><c>[Q+0x14]</c>: the item count at which the push flushes instead of allocating.</param>
    /// <param name="freeList">The array indices on the free list at the start, head first (the init's order is not read).</param>
    public WwiseNotificationQueue(WwiseBankMemory memory, int arrayCapacity, uint limit, IReadOnlyList<int> freeList)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        ArgumentNullException.ThrowIfNull(freeList);
        _arrayCapacity = arrayCapacity;
        Limit = limit;
        var array = new WwiseNotificationItem[arrayCapacity];
        for (int i = 0; i < arrayCapacity; i++) array[i] = new WwiseNotificationItem(i, true, 0);
        WwiseNotificationItem? head = null, tail = null;
        foreach (int index in freeList)
        {
            var item = array[index];
            if (tail is null) head = item; else tail.Next = item;
            tail = item;
        }
        Free = head;
    }

    /// <summary><c>[Q+4]</c>.</summary>
    public WwiseNotificationItem? Head { get; private set; }

    /// <summary><c>[Q+8]</c>.</summary>
    public WwiseNotificationItem? Tail { get; private set; }

    /// <summary><c>[Q+0xC]</c>.</summary>
    public WwiseNotificationItem? Free { get; private set; }

    /// <summary><c>[Q+0x14]</c>.</summary>
    public uint Limit { get; }

    /// <summary><c>[Q+0x18]</c>.</summary>
    public uint Count { get; private set; }

    /// <summary>The flush <c>0xA38420</c> the push runs when it can get no item (<c>0xA38694</c>); required then.</summary>
    public Action? FlushA38420 { get; set; }

    /// <summary>
    /// <c>0xA38600(pbi, code, r2, r3)</c>. An item comes from the free list (<c>[Q+0xC]</c>); when it is empty and the count is below the limit (unsigned) one 0x14-byte block is allocated (<c>0xA386D8</c>, <c>0xA7A7F4</c>); when
    /// the count is not below the limit, or the allocation fails, the flush runs first (<c>0xA38694</c>) and the free list is tried again, then (<c>0xA38710..0xA38750</c>) one allocation if the count is below the limit; a second
    /// failure stores to address 0 and traps (<c>0xA38720..0xA38728</c>: the engine crashes). The item <c>{0, pbi, code, r2, r3}</c> is appended at the tail (<c>[tail] = item</c>, or <c>[Q+4] = item</c> when the queue is empty),
    /// the free list advances, the count grows.
    /// </summary>
    public void Push(object? pbi, int code, int r2, int r3)
    {
        var item = Free;                                                           // 0xA38618 ldr ip,[r5,#0xc]
        if (item is null)                                                          // 0xA3861C cmp ip,#0; beq 0xA3867C
        {
            if (Count < Limit)                                                     // 0xA3867C..0xA38688 cmp r0,r1; blo 0xA386D8
                item = Allocate();                                                 // 0xA386D8..0xA3870C
            if (item is null)                                                      // 0xA386F8 beq 0xA3868C / 0xA38688 not taken
            {
                (FlushA38420 ?? throw new WwiseMissingBehaviourException(
                    "M6-025 S8: 0xA38600 needs the flush 0xA38420 (WwiseNotificationQueue.FlushA38420) when the queue is full"))();   // 0xA38694
                item = Free;                                                       // 0xA386A0 ldr ip,[r5,#0xc]
                if (item is null)                                                  // 0xA386A4 cmp ip,#0; beq 0xA38710
                {
                    if (Count >= Limit)                                            // 0xA38710..0xA3871C cmp r0,r1; blo 0xA38734
                        throw new InvalidOperationException("the notification queue is full after the flush: 0xA38600 stores to address 0 and traps (0xA38720..0xA38728)");
                    item = Allocate() ?? throw new InvalidOperationException(
                        "the notification item allocation failed after the flush: 0xA38600 stores to address 0 and traps (0xA3874C..0xA38728)");   // 0xA38748..0xA38750
                }
            }
        }
        if (Tail is not null) Tail.Next = item; else Head = item;                  // 0xA3862C..0xA38638 strne ip,[r1]; 0xA386D0 str ip,[r0,#4]
        Free = item.Next;                                                          // 0xA38644 ldr r7,[ip]; 0xA38658 str r7,[r0,#0xc]
        Tail = item;                                                               // 0xA38654 str ip,[r0,#8]
        Count++;                                                                   // 0xA3865C, 0xA38664
        item.Next = null;                                                          // 0xA38660 str r5,[r1],#4 (0)
        item.Pbi = pbi;                                                            // 0xA38668 stm r1,{r4,r6}
        item.Code = code;
        item.R2 = r2;                                                              // 0xA3866C
        item.R3 = r3;                                                              // 0xA38670
    }

    private WwiseNotificationItem? Allocate()
    {
        if (_memory.Allocate(0x14) is not { } address) return null;                // 0xA386EC bl 0xA7A7F4(pool, 0x14)
        var item = new WwiseNotificationItem(_arrayCapacity + _allocated++, false, address) { Next = Free };   // 0xA38704 str r1,[r0] ([Q+0xC] is the old free head)
        Free = item;                                                               // 0xA38708 str r0,[r5,#0xc]
        return item;
    }

    /// <summary>
    /// The removal at the end of an item's turn in the flush (<c>0xA38518..0xA3856C</c>): <c>[Q+4] = [head]</c>, an item that was also the tail clears <c>[Q+8]</c>; an item inside the preallocated array goes back to the front of the free
    /// list, any other is freed (<c>0xA7A988</c>); the count drops.
    /// </summary>
    public void PopHeadA38518()
    {
        var item = Head ?? throw new InvalidOperationException("the notification queue is empty: the flush dereferences its head (0xA38518)");
        Head = item.Next;                                                          // 0xA3851C..0xA38524
        if (Tail == item) Tail = null;                                             // 0xA38520 cmp r2,r1; 0xA38528..0xA3852C moveq
        if (item.InArray)                                                          // 0xA38540..0xA385B0
        {
            item.Next = Free;                                                      // 0xA385B4..0xA385B8
            Free = item;                                                           // 0xA385BC
        }
        else
            _memory.Free(item.Address);                                            // 0xA38554 bl 0xA7A988(pool, item)
        Count--;                                                                   // 0xA38560..0xA38568
    }
}
