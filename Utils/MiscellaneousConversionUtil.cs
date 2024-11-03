using System;
using System.Text;
using Furnace2MML.Etc;
using static Furnace2MML.Etc.PublicValue;
namespace Furnace2MML.Utils;

public static class MiscellaneousConversionUtil
{
    public static byte GetOrderNum(int tick)
    {
        for(byte orderNum = 0; orderNum <= MaxOrderNum; orderNum++) {
            var curOrderStartTick = OrderStartTimes[orderNum].StartTick;
            var nextOrderStartTick = orderNum+1 <= MaxOrderNum ? OrderStartTimes[orderNum+1].StartTick : int.MaxValue;

            if(curOrderStartTick <= tick && tick < nextOrderStartTick)
                return orderNum;
        }
        
        return byte.MaxValue;
    }

    public static byte[]? GetRowNum(int tick)
    {
        // TickPerUnitChanges.Select(tpuc => tpuc.ChangeTimeTick )

        var tickPerUnitChangesLen = TickPerUnitChanges.Count;
        for(byte i = 0; i < tickPerUnitChangesLen; i++) {
            var curTickPerUnitChangeStruct  = TickPerUnitChanges[i];
            
            var curChangeTick  = curTickPerUnitChangeStruct.ChangeTimeTick;
            var nextChangeTick = i+1 < tickPerUnitChangesLen ? TickPerUnitChanges[i+1].ChangeTimeTick : EndTick;

            if(curChangeTick < tick && tick <= nextChangeTick) {
                var orderNum = GetOrderNum(tick);
                var orderStartTime = OrderStartTimes[orderNum].StartTick;

                var tickInCurOrder = tick - orderStartTime;

                var rowNumInfo = new[] { (byte)(tickInCurOrder / curTickPerUnitChangeStruct.TickPerRow), (byte)(tickInCurOrder % curTickPerUnitChangeStruct.TickPerRow) };
                return rowNumInfo;
            }
        }
        
        return null;
    }

    public static string GetPitchChar(int noteNum, bool isBinCmd = false)
    {
        if(noteNum == 0xB4)
            return "NUL";
        
        var octave = noteNum / 12;
        var pitch  = noteNum % 12;

        if(isBinCmd)
            octave -= 5;

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

    public static string GetFurnaceCommandPositionInfo(FurnaceCommand cmd)
        => $"[Channel: {cmd.Channel}, Pos(Order-Row): {cmd.OrderNum:X2}-{GetRowNum(cmd.Tick):X2}, Tick: {cmd.Tick}]";
}