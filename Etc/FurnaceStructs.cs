using System;
using Furnace2MML.Utils;

namespace Furnace2MML.Etc;

public struct SongInformation
{
    public string SongName;
    public string Author;
    public string Album;
    public string System;
    public int Tuning;  // Will not be used
    
    public byte InstrumentCount;
    public byte WavetableCount;  // Will not be used
    public byte SampleCount;  // Will not be used
}

public struct InstrumentDefinition()
{
    public byte InstNum;
    public string InstName = "";
    
    public byte Alg;  // Algorithm
    public byte Fb;   // Feedback
    public byte Fms;
    public byte Ams;
    public byte Fms2;
    public byte Ams2;
    
    public byte OperatorCount;
    public byte[,] Operators = null!;  // A

}

public struct SubsongData()
{
    public float TickRate;
    public byte Speed;
    public readonly int[] VirtualTempo = new int[2];  // 0: Numerator / 1: Denominator
    public int TimeBase;
    public int PatternLen;
}


/// <summary>
/// Note On/Off, Portamento, Volume, Panning, etc.
/// </summary>
public struct FurnaceCommand(int tick, byte orderNum, byte channel, string cmdType, int value1, int value2) : IComparable<FurnaceCommand>
{
    public readonly int Tick = tick;
    public readonly byte OrderNum = orderNum;  // OrderNum cannot be 0xFF(255)
    public readonly byte Channel = channel;
    public readonly string CmdType = cmdType;
    
    public int Value1 = value1;
    public readonly int Value2 = value2;

    //  Copy otherCmd except tick
    public FurnaceCommand(int tick, FurnaceCommand otherCmd) : this(otherCmd.Tick, otherCmd.OrderNum, otherCmd.Channel, otherCmd.CmdType, otherCmd.Value1, otherCmd.Value2)
        => Tick = tick;

    public override string ToString()
    {
        switch(CmdType) {
            case "NOTE_ON": case "HINT_LEGATO": case "HINT_PORTA":
                return $"{Channel:00} | {OrderNum:X2} {Tick}: [{Value1:X2}({MiscellaneousConversionUtil.GetPitchChar(Value1)}) {Value2:X2}({Value2:000}) {CmdType}]";
            default:
                return $"{Channel:00} | {OrderNum:X2} {Tick}: [{Value1:X2}({Value1:000}) {Value2:X2}({Value2:000}) {CmdType}]";
        }
    }
    // => $"[{Tick} {Channel} {CmdType} {Value1} {Value2}]";
    public int CompareTo(FurnaceCommand other)
    {
        var tickComparison = Tick.CompareTo(other.Tick);
        return tickComparison != 0 ? tickComparison : GetCmdTypeOrderPriority(CmdType).CompareTo(GetCmdTypeOrderPriority(other.CmdType));
    }
    
    public override bool Equals(object? obj)
        => obj is FurnaceCommand otherCmd && Equals(otherCmd);
    private bool Equals(FurnaceCommand other)
        => Tick     == other.Tick && 
           Channel  == other.Channel && 
           CmdType.Equals(other.CmdType) && 
           Value1   == other.Value1 && 
           Value2   == other.Value2;
    public override int GetHashCode()
        => HashCode.Combine(Tick, OrderNum, Channel, CmdType, Value1, Value2);

    /// <summary>
    /// A larger value will be ordered at the back of the array
    /// </summary>
    /// <param name="cmdType"></param>
    /// <returns></returns>
    private static int GetCmdTypeOrderPriority(string cmdType)
    {
        return cmdType switch {
            "NOTE_ON"    => 3,
            "NOTE_OFF"   => 3,
            "HINT_PORTA" => 2,
            _            => 1,
        };
    }

    public static bool operator ==(FurnaceCommand left, FurnaceCommand right) => left.Equals(right);
    public static bool operator !=(FurnaceCommand left, FurnaceCommand right) => !(left == right);
}


/// <summary>
///  Song Effect, Speed Effect
/// </summary>
public struct OtherEffect(int tick, byte channel, byte effType, byte value)
{
    public readonly int Tick = tick;
    public readonly byte Channel = channel;
    
    public readonly byte EffType = effType;
    public readonly byte Value = value;
    
    public readonly string Category = effType switch {
        // Speed Effects
        0x09 => "Speed",
        0x0F => "Speed",
        0xF0 => "Speed",
        // Song Effects
        0x0B => "Song",
        0x0D => "Song",
        0xFF => "Song",
            
        _    => throw new ArgumentOutOfRangeException($"Invalid effect type value: {effType:X2}")
    };
    public readonly string EffTypeStr = effType switch {
        // Speed Effects
        0x09 => "Set groove pattern",
        0x0F => "Set speed",
        0xF0 => "Set tick rate (bpm)",
        // Song Effects
        0x0B => "Jump to pattern",
        0x0D => "Jump to next pattern",
        0xFF => "Stop song",
            
        _    => throw new ArgumentOutOfRangeException($"Invalid effect type value: {effType:X2}")
    };
    public EffectValueType ValueType = effType switch {
        // Speed Effects
        0x09 => EffectValueType.XX,
        0x0F => EffectValueType.XX,
        0xF0 => EffectValueType.XX,
        // Song Effects
        0x0B => EffectValueType.XX,
        0x0D => EffectValueType.UNUSED,
        0xFF => EffectValueType.UNUSED,
            
        _    => throw new ArgumentOutOfRangeException($"Invalid effect type value: {effType:X2}")
    };
    
    public override string ToString()
        => $"{Channel:00} | {Tick}: [({Category}: {EffTypeStr}) {EffType:X2}{Value:X2}]";
}

public readonly struct TickPerUnitChange(int timeTick, int timeOrderNum, int timeRowNum, int tickPerRow, int tickPerOrder)
{
    public readonly int TimeTick = timeTick;
    public readonly int TimeOrderNum = timeOrderNum;
    public readonly int TimeRowNum = timeRowNum;
    public readonly int TickPerRow = tickPerRow;  // == Speed Value
    public readonly int TickPerOrder = tickPerOrder;

    public override string ToString()
        => $"Time: {TimeTick}({TimeOrderNum}:{TimeRowNum}) | TPR: {TickPerRow}, TPO: {TickPerOrder}";
}

public readonly struct OrderStartTime(byte orderNum, int orderStartTick, int skippedTick, int totalSkippedTick)
{
    public readonly byte OrderNum = orderNum;
    public readonly int StartTick = orderStartTick;
    public readonly int SkippedTick = skippedTick;  // Skipped ticks by jump to pattern effects from previous order
    public readonly int TotalSkippedTick = totalSkippedTick;  
    
    public override string ToString()
        => $"OrderNum: {OrderNum:X2} | Tick: {StartTick} | Skipped Tick: {SkippedTick} (Total: {TotalSkippedTick})";
}