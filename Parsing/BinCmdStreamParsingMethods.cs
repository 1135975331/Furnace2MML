using System;
using Furnace2MML.Etc;
namespace Furnace2MML.Parsing;

public static class BinCmdStreamParsingMethods
{
    public static CmdType GetCmdType(byte curByteVal)
    {
        return curByteVal switch {   // 다른 파일에 유틸리티 함수로 만들 것
            <= 0xB4 => CmdType.NOTE_ON, // 0x00 <= curByteVal <= 0xB4 (byte: 0x00 ~ 0xFF)
            0xB5    => CmdType.NOTE_OFF,
            0xB6    => CmdType.NOTE_OFF_ENV, // Note off env
            0xB7    => CmdType.ENV_RELEASE,  // env release
            0xB8    => CmdType.INSTRUMENT,
            0xBE    => CmdType.PANNING,

            0xC0 => CmdType.PRE_PORTA,
            0xC2 => CmdType.VIBRATO,
            0xC3 => CmdType.VIB_RANGE,
            0xC4 => CmdType.VIB_SHAPE,
            0xC5 => CmdType.PITCH,
            0xC6 => CmdType.HINT_ARPEGGIO,
            0xC7 => CmdType.HINT_VOLUME,
            0xC8 => CmdType.VOL_SLIDE,
            0xC9 => CmdType.HINT_PORTA,
            0xCA => CmdType.HINT_LEGATO,
            // ...
            _ => throw new ArgumentOutOfRangeException($"Unknown CmdType (should be <=0xCA): {curByteVal}")
        };
    }

    public static int GetValueCount(CmdType cmdType)
    {
        return cmdType switch {  // value(Byte)Count  1 Value count == 1 Byte(2 Hexadecimal digits)
            CmdType.NOTE_ON      or 
            CmdType.NOTE_OFF     or
            CmdType.NOTE_OFF_ENV or
            CmdType.PRE_PORTA    or
            CmdType.ENV_RELEASE 
                                => 0,
                    
            CmdType.INSTRUMENT    or  // INSTRUMENT takes 1 value
            CmdType.VIB_RANGE     or
            CmdType.VIB_SHAPE     or
            CmdType.PITCH         or
            CmdType.HINT_VOLUME   or
            CmdType.HINT_LEGATO   or
            CmdType.HINT_ARP_TIME 
                                => 1,
                    
            CmdType.PANNING       or
            CmdType.VIBRATO       or
            CmdType.HINT_ARPEGGIO or
            CmdType.VOL_SLIDE     or
            CmdType.HINT_PORTA    
                                => 2,
                    
            _ => throw new ArgumentOutOfRangeException($"Unknown CmdType (on valueCount): {cmdType}")
        };
    }
    
    public static int GetValue1(byte binVal, CmdType cmdType)
    {
        return cmdType switch {
            CmdType.INSTRUMENT    or 
            CmdType.PANNING       or 
            CmdType.PRE_PORTA     or 
            CmdType.VIBRATO       or 
            CmdType.VIB_RANGE     or 
            CmdType.VIB_SHAPE     or 
            CmdType.PITCH         or 
            CmdType.HINT_ARPEGGIO or 
            CmdType.HINT_VOLUME   or 
            CmdType.VOL_SLIDE     or 
            CmdType.HINT_PORTA    or 
            CmdType.HINT_LEGATO   or 
            CmdType.HINT_ARP_TIME => binVal,
                
            _ => throw new ArgumentOutOfRangeException(nameof(cmdType), cmdType, null)
        };
    }
    
    public static int GetValue2(byte binVal, CmdType cmdType)
    {
        return cmdType switch {
            CmdType.PANNING       or
            CmdType.PRE_PORTA     or
            CmdType.VIBRATO       or
            CmdType.HINT_ARPEGGIO or
            CmdType.VOL_SLIDE     or
            CmdType.HINT_PORTA    or
            CmdType.HINT_ARP_TIME => binVal,

            _ => throw new ArgumentOutOfRangeException(nameof(cmdType), cmdType, null)
        };
    }
}