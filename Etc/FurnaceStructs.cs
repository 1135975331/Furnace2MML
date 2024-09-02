using System;
using System.ComponentModel;
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

    public float GetVirtTempoInDecimal() => (float)VirtualTempo[0] / VirtualTempo[1];
}


/// <summary>
/// Note On/Off, Portamento, Volume, Panning, etc.
/// </summary>
public struct 
    FurnaceCommand(int tick, byte channel, CmdType cmdType, int value1, int value2) : IComparable<FurnaceCommand>
{
    public readonly  int                  Tick     = tick;
    public readonly  byte                 OrderNum = MiscellaneousConversionUtil.GetOrderNum(tick);  // OrderNum cannot be 0xFF(255)
    public readonly  byte                 Channel  = channel;
    private readonly string               _cmdTypeStr;
    public readonly  CmdType              CmdType = cmdType;
    public           int                  Value1  = value1;
    public readonly  int                  Value2  = value2;


    public FurnaceCommand(int tick, byte orderNum, byte channel, string cmdTypeStr, int value1, int value2) : this(tick, channel, CmdType.INVALID, value1, value2) 
        => CmdType = GetCmdTypeEnum(cmdTypeStr);

    //  Copy otherCmd except Tick
    public FurnaceCommand(int tick, FurnaceCommand otherCmd) : this(otherCmd.Tick, otherCmd.Channel, otherCmd.CmdType, otherCmd.Value1, otherCmd.Value2)
    {
        Tick     = tick;
        OrderNum = MiscellaneousConversionUtil.GetOrderNum(tick);
    }

    //  Copy otherCmd except Value1 and Value2 
    public FurnaceCommand(int value1, int value2, FurnaceCommand otherCmd) : this(otherCmd.Tick, otherCmd.Channel, otherCmd.CmdType, otherCmd.Value1, otherCmd.Value2)
    {
        Value1 = value1;
        Value2 = value2;
    }

    public override string ToString()
    {
        var rowNumInfo = MiscellaneousConversionUtil.GetRowNum(Tick) ?? [0xFF, 0xFF];

        return CmdType switch {
            CmdType.NOTE_ON or CmdType.HINT_LEGATO or CmdType.HINT_PORTA =>
                 $"{Channel:00}({GetChannelName(Channel)}) | {OrderNum:X2}-{rowNumInfo[0]:X2}/{rowNumInfo[1]:X2} {Tick+":",-4} [{CmdType,-10} {Value1.ToString("X2")[^2..]}({MiscellaneousConversionUtil.GetPitchChar(Value1, true)}) {Value2.ToString("X2")[^2..]}({Value2:000})]",
            _ => $"{Channel:00}({GetChannelName(Channel)}) | {OrderNum:X2}-{rowNumInfo[0]:X2}/{rowNumInfo[1]:X2} {Tick+":",-4} [{CmdType,-10} {Value1.ToString("X2")[^2..]}({Value1:000}) {Value2.ToString("X2")[^2..]}({Value2:000})]"
        };
    }
    // => $"[{Tick} {Channel} {CmdType} {Value1} {Value2}]";
    public int CompareTo(FurnaceCommand other)
    {
        var tickComparison = Tick.CompareTo(other.Tick);
        if(tickComparison != 0)
            return tickComparison;

        var cmdTypeComparison = GetCmdTypeOrderPriority(CmdType).CompareTo(GetCmdTypeOrderPriority(other.CmdType));
        return Channel switch {
            >= 9 and <= 14 => cmdTypeComparison != 0 ? cmdTypeComparison : Channel.CompareTo(other.Channel),
            _              => cmdTypeComparison
        };

    }
    
    public override bool Equals(object? obj)
        => obj is FurnaceCommand otherCmd && Equals(otherCmd);
    private bool Equals(FurnaceCommand other)
        => Tick     == other.Tick && 
           Channel  == other.Channel && 
           // _cmdTypeStr.Equals(other._cmdTypeStr) && 
           CmdType == other.CmdType && 
           Value1   == other.Value1 && 
           Value2   == other.Value2;
    
    public override int GetHashCode()
        => HashCode.Combine(Tick, OrderNum, Channel, CmdType, Value1, Value2);

    /// <summary>
    /// A larger value will be ordered at the back of the array
    /// </summary>
    /// <param name="cmdType"></param>
    /// <returns></returns>
    private static int GetCmdTypeOrderPriority(CmdType cmdType)
    {
        return cmdType switch {
            CmdType.NOTE_ON     => 3,
            CmdType.NOTE_OFF    => 3,
            CmdType.HINT_PORTA  => 2,
            CmdType.HINT_LEGATO => 2,
            _                   => 1,
        };
    }
    
    public static string GetChannelName(int chNum)
    {
        return chNum switch {
            0  => "F1",
            1  => "F2",
            2  => "F3",
            3  => "F4",
            4  => "F5",
            5  => "F6",
            6  => "S1",
            7  => "S2",
            8  => "S3",
            9  => "BD",
            10 => "SD",
            11 => "TP",
            12 => "HH",
            13 => "TM",
            14 => "RM",
            15 => "AP",
            _  => throw new ArgumentOutOfRangeException(nameof(chNum), chNum, null)
        };
    }
    
    public static CmdType GetCmdTypeEnum(string cmdTypeStr) => Enum.Parse<CmdType>(cmdTypeStr);

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

public readonly struct TickPerUnitChange(int changeTimeTick, int changeTimeOrderNum, int changeTimeRowNum, int speedValue, int virtTempoNumerator, int virtTempoDenominator)
{
    public readonly int ChangeTimeTick     = changeTimeTick;
    public readonly int ChangeTimeOrderNum = changeTimeOrderNum;
    public readonly int ChangeTimeRowNum   = changeTimeRowNum;
    public readonly int Speed              = speedValue;  // == Speed Value
    public readonly int VirtTempoNumer     = virtTempoNumerator;
    public readonly int VirtTempoDenom     = virtTempoDenominator;
    public readonly int TickPerRow         = (int)(speedValue / ((float)virtTempoNumerator / virtTempoDenominator));  
    public readonly int TickPerOrder       = (int)(speedValue / ((float)virtTempoNumerator / virtTempoDenominator)) * PublicValue.Subsong.PatternLen;

    public override string ToString()
        => $"Time: {ChangeTimeTick}({ChangeTimeOrderNum}:{ChangeTimeRowNum}) | SPD: {Speed}, VT: {VirtTempoNumer}/{VirtTempoDenom} TPR: {TickPerRow}, TPO: {TickPerOrder}";
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

public readonly struct CmdFieldChangeArg
{
    public readonly CmdStructField FieldToChange;
    public readonly int            IntValue;
    public readonly CmdType        CmdTypeValue;
    
    public CmdFieldChangeArg(CmdStructField fieldToChange, int value)
    {
        if(fieldToChange == CmdStructField.CMD_TYPE)
            throw new InvalidEnumArgumentException($"All CmdStructField Enums but CMD_TYPE is expected, but {fieldToChange} came.");
        
        FieldToChange = fieldToChange;
        IntValue      = value;
    }
    
    public CmdFieldChangeArg(CmdType value)
    {
        FieldToChange = CmdStructField.CMD_TYPE;
        CmdTypeValue  = value;
    }
}