using System;
using System.Text;
using static Furnace2MML.Etc.PublicValue;
namespace Furnace2MML.Utils;

public static class MiscellaneousConversionUtil
{
    public static byte GetOrderNum(int tick)
    {
        for(byte orderNum = 0; orderNum <= MaxOrderNum; orderNum++) {
            var curOrderStartTick = OrderStartTicks[orderNum].StartTick;
            var nextOrderStartTick = orderNum+1 <= MaxOrderNum ? OrderStartTicks[orderNum+1].StartTick : int.MaxValue;

            if(curOrderStartTick <= tick && tick < nextOrderStartTick)
                return orderNum;
        }
        
        return byte.MaxValue;
    }

    public static string GetPitchChar(int noteNum)
    {
        var octave     = noteNum / 12;
        var pitch      = noteNum % 12;
        
        var pitchChar = pitch switch {
            0  => "C-",
            1  => "C#",
            2  => "D-",
            3  => "D#",
            4  => "E-",
            5  => "F-",
            6  => "F#",
            7  => "G-",
            8  => "G#",
            9  => "A-",
            10 => "A#",
            11 => "B-",
            _  => throw new ArgumentOutOfRangeException($"Invalid pitch value: {pitch}")
        };
        
        return new StringBuilder(pitchChar).Append(octave).ToString();
    }
}