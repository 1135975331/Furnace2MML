using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Furnace2MML.Etc;
using static Furnace2MML.Etc.PublicValue;
namespace Furnace2MML.Utils;

public static class CmdStreamToMMLUtil
{
    /// <summary>
    ///  Tick 혹은 Fraction 길이를 서로 반대로 바꿔주는 메소드
    /// 해당 메소드에 Tick길이를 넣으면 Fraction길이,
    /// Fraction길이를 넣으면 Tick길이가 반환된다.
    /// </summary>
    /// <param name="value">Tick Length of Fraction Length</param>
    /// <returns>If value is Tick Length, returns Fraction Length <br/>
    /// If value is Fraction Length, returns Tick Length
    /// </returns>
    public static int ConvertBetweenTickAndFraction(int value)
    {
        return value switch {
            1  => 96,
            2  => 48,
            3  => 32,
            4  => 24,
            6  => 16,
            8  => 12,
            12 => 8,
            16 => 6,
            24 => 4,
            32 => 3,
            48 => 2,
            96 => 1,
            _  => -1
        };
    }
    
    public static string ConvertChannel(int chNum)
    {
        return chNum switch {
            // FM 1~6
            0 => "A",
            1 => "B",
            2 => "C",
            3 => "D",
            4 => "E",
            5 => "F",
                
            // SSG 1~3
            6 => "G",
            7 => "H",
            8 => "I",
            
            _ => "?"
        };
    }

    
    public static string GetPitchStr(int pitch)
    {
        return pitch switch {
            0  => "c",
            1  => "c+",
            2  => "d",
            3  => "d+",
            4  => "e",
            5  => "f",
            6  => "f+",
            7  => "g",
            8  => "g+",
            9  => "a",
            10 => "a+",
            11 => "b",
            _  => throw new ArgumentOutOfRangeException($"Invalid pitch value: {pitch}")
        };
    }
    

    public static int GetCmdTickLength(List<FurnaceCommand> cmdList, int curCmdIdx)
    {
        var curCmd         = cmdList[curCmdIdx];
        var allNoteInChLen = cmdList.Count;
        if(curCmdIdx + 1 < allNoteInChLen) {
            var otherNote = cmdList[curCmdIdx + 1];
            return otherNote.Tick - curCmd.Tick;
        }

        var songTotalTick = EndTick;
        return songTotalTick - curCmd.Tick;
    }
    
    public static StringBuilder GetMMLNote(int noteNum, ref int defaultOct, bool updateDefaultOct = true)
    {
        var sb         = new StringBuilder();
        var octave     = noteNum / 12 - 5;  // note on command on Binary Command Stream can have C-(-5) ~ B-9. C(-5) ~ B(-1) should be excluded.
        var pitch      = noteNum % 12;
        var octaveDiff = octave - defaultOct;
	    
        switch(octaveDiff) {
            case > 0: sb.Append(new string('>', octaveDiff)); break;
            case < 0: sb.Append(new string('<', -octaveDiff)); break;
        }
        
        if(updateDefaultOct)
            defaultOct = octave;
	    
        sb.Append(GetPitchStr(pitch));
        return sb;
    }

    /// <summary>
    /// ExactFractionLength: n분음 표기(1, 2, 4, 8, 16, 32, 64,  3, 6, 12, 24, 48, 96)가 가능한가의 여부
    /// </summary>
    /// <param name="tickLength"></param>
    /// <param name="conversionResult"></param>
    /// <returns></returns>
    public static bool GetIsExactFractionLength(int tickLength, out int conversionResult)
    {
        conversionResult = ConvertBetweenTickAndFraction(tickLength);
        return conversionResult != -1;
    }

    public static FurnaceCommand GetFirstNoteOn(List<FurnaceCommand> cmdsInCh)
        => cmdsInCh.FirstOrDefault(cmd => cmd.CmdType == CmdType.NOTE_ON, new FurnaceCommand(-1, byte.MaxValue, byte.MaxValue, "NO_NOTE_ON", -1, -1));


    public static int ConvertTickrateToTempo(double tickrate)
        => (int)(tickrate * 2.5) / 2;

    public static StringBuilder AppendNoteLength(this StringBuilder curOrderSb, int tickLen)
    {
        if(tickLen == 0)
            return curOrderSb;
        
        // var mmlFracLen = FormatNoteFracLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        var mmlFracLen = FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteLength);
        curOrderSb.Append(mmlFracLen);
        return curOrderSb;
    }

    public static string FormatNoteInternalClockLength(int tickLenP, int defaultClockLen)
    {
        return tickLenP == defaultClockLen ? "" : $"%{tickLenP}";
    }

    public static StringBuilder FormatNoteLength(int tickLenP, int[] validFractionLength, string defaultFractionLength)
    {
        var tickLen      = tickLenP;
        var strBuilder = new StringBuilder();

        var fracLenResultList = new List<int>();
        var validFracLenIndex = 0;

        while(tickLen > 0) {                                                                               // tickLength -> FractionLength 변환
            var isTickLengthExact = CmdStreamToMMLUtil.GetIsExactFractionLength(tickLen, out var fracLen); // validFractionLength의 길이와 정확이 일치한가의 여부

            if(isTickLengthExact) {
                fracLenResultList.Add(fracLen);
                tickLen -= tickLen;
            } else {
                var curTick = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(validFractionLength[validFracLenIndex]);

                var isValidFracLen = tickLen / curTick >= 1; //isValidTick, 현재 curTick값에 의한 분수표기가 fracLengthResultList에 들어가는 것이 올바른지 여부
                
                if(isValidFracLen) {
                    fracLenResultList.Add(validFractionLength[validFracLenIndex]);
                    tickLen -= curTick;
                } else
                    validFracLenIndex++;
            }
        }

        var fracLenResultListLen = fracLenResultList.Count;

        if(fracLenResultListLen == 1) {  // Fraction Length
            var fracLength      = fracLenResultList[0];
            var isDefaultLength = fracLength.ToString().Equals(defaultFractionLength);
			
            var fracLenStr = fracLength.ToString();

            if(!isDefaultLength)
                strBuilder.Append(fracLenStr);
        } else {  // Internal Clock Length
            var isFirstIteration = true;
            var curTickLen       = tickLenP;
            
            while(curTickLen > 255) {
                strBuilder.Append(isFirstIteration && $"%{255}".Equals(defaultFractionLength) ? "" : $"%{255}").Append('&');
                curTickLen -= 255;
                isFirstIteration = false;
            }
            strBuilder.Append($"%{curTickLen}".Equals(defaultFractionLength) ? "" : $"%{curTickLen}");
        }

        return strBuilder;
    }

    // private static readonly string[] DefaultFracLenCmdTypes = ["NOTE_ON", "NOTE_OFF", "HINT_LEGATO"];
    private static readonly CmdType[] CmdTypesToGetDefaultLen = [CmdType.NOTE_ON, CmdType.NOTE_OFF, CmdType.HINT_LEGATO];
    public static string GetDefaultNoteLenForThisOrder(List<FurnaceCommand> cmdList, int curOrder, int curIdx)
    {
        var nextOrderStartTick = GetOrderStartTick(curOrder+1);
        
        var noteLenCounts = new Dictionary<string, int>();  // <FracLen, Count>
        
        var cmdListLen = cmdList.Count;
        for(var i = curIdx; i < cmdListLen; i++) {
            var curCmd = cmdList[i];

            if(curCmd.CmdType.EqualsAny(CmdTypesToGetDefaultLen)) {
                var tickLen = GetCmdTickLength(cmdList, i);
                var fracLen = ConvertBetweenTickAndFraction(tickLen);

                var lenStr = fracLen == -1 ? $"%{Math.Min(tickLen, 255)}" : $"{fracLen}";  // fracLen == -1 : n분음표 길이로 나타낼 수 없는 경우  // internal clock($)의 범위는 1~255 
                
                if(noteLenCounts.ContainsKey(lenStr))
                    noteLenCounts[lenStr] += lenStr.Length;
                else
                    noteLenCounts.Add(lenStr, lenStr.Length);

            }

            if(curCmd.Tick >= nextOrderStartTick)
                break;
        }

        noteLenCounts.Remove("%0");
        noteLenCounts.Remove("-1");

        return Util.GetKeyWithLargestValue(noteLenCounts) ?? string.Empty;
    }
    
    public static int GetDefaultNoteClockLenForThisOrder(List<FurnaceCommand> cmdList, int curOrder, int curIdx)
    {
        var nextOrderStartTick = GetOrderStartTick(curOrder+1);
        
        var clockLenCounts = new Dictionary<int, int>();  // <FracLen, Count>
        
        var cmdListLen = cmdList.Count;
        for(var i = curIdx; i < cmdListLen; i++) {
            var curCmd = cmdList[i];

            if(curCmd.CmdType.EqualsAny(CmdTypesToGetDefaultLen)) {
                var clockLen = GetCmdTickLength(cmdList, i);
                
                if(!clockLenCounts.TryAdd(clockLen, 1))
                    clockLenCounts[clockLen]++;
            }

            if(curCmd.Tick >= nextOrderStartTick)
                break;
        }
        
        return Util.GetKeyWithLargestValue(clockLenCounts);
    }

    /// <summary>
    /// Search for the first cmd that meets the condition.
    /// </summary>
    /// <param name="cmdList">List of FurnaceCommand</param>
    /// <param name="startIdx">Start index to search (caution: <c>startIdx</c> value is exclusive)</param>
    /// <param name="predicateToSearch"></param>
    /// <param name="foundCmdIdx">index value where the command is found</param>
    /// <param name="predicateToStopSearching">A condition that stops searching.</param>
    /// <param name="direction">Search direction. Only forward(increasing index) or backward(decreasing index) is valid.</param>
    /// <param name="isCmdFound">if such cmd is found, <c>isCmdFound</c> is <c>true</c>. if cmd is not found, or searching is stopped, <c>isCmdFound</c> is <c>false</c>.</param>
    /// <param name="isStartIdxInclusive">Start cmd searching including <c>startIdx</c>. Default value is <c>false</c></param>
    /// <exception cref="ArgumentOutOfRangeException">If direction parameter is neither <c>forward</c> nor <c>backward</c>.</exception>
    /// <returns>The first FurnaceCommand that meets the condition. <br/>
    /// If searching was stopped by <c>predicateToStopSearching</c> condition, returns FurnaceCommand with <c>"SEARCH_STOPPED"</c> CmdType. <br/>
    /// If there is no FurnaceCommand that meet the conditions, returns FurnaceCommand with <c>EndTick</c>Tick and <c>"NOTE_OFF"</c> CmdType.
    /// </returns>
    public static FurnaceCommand GetFirstCertainCmd(List<FurnaceCommand> cmdList, int startIdx, Predicate<FurnaceCommand> predicateToSearch, out bool isCmdFound, out int foundCmdIdx, Predicate<FurnaceCommand>? predicateToStopSearching = null, string direction = "forward", bool isStartIdxInclusive = false)
    {
        var cmdWhenStopped = new FurnaceCommand(-1, 0xFF, 0xFF, "SEARCH_STOPPED", -1, -1);

        if(isStartIdxInclusive) {
            switch(direction) {
                case "forward":  startIdx--; break;
                case "backward": startIdx++; break;
                default:         throw new ArgumentException($"Invalid direction: {direction}");
            }
        }

        switch(direction) {
            case "forward": {
                var listLen = cmdList.Count;
                for(var i = startIdx + 1; i < listLen; i++) {
                    if(predicateToSearch(cmdList[i])) {
                        isCmdFound  = true;
                        foundCmdIdx = i;
                        return cmdList[i];
                    }
                    if(predicateToStopSearching != null && predicateToStopSearching(cmdList[i])) {
                        isCmdFound  = false;
                        foundCmdIdx = -1;
                        
                        return cmdWhenStopped;
                    }
                }
                break;
            }
            case "backward": {
                for(var i = startIdx - 1; i >= 0; i--) {
                    if(predicateToSearch(cmdList[i])) {
                        isCmdFound = true;
                        foundCmdIdx = i;
                        return cmdList[i];
                    }
                    if(predicateToStopSearching != null && predicateToStopSearching(cmdList[i])) {
                        isCmdFound = false;
                        foundCmdIdx = -1;
                        return cmdWhenStopped;
                    }
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException($"Invalid direction argument: {direction}");
        }

        isCmdFound = false;
        foundCmdIdx = -1;
        return new FurnaceCommand(EndTick, MiscellaneousConversionUtil.GetOrderNum(EndTick), cmdList[0].Channel, "NOTE_OFF", 0, 0);
    }



    /// <summary>
    /// 리스트에서 다음 틱의 인덱스를 반환하는 메소드
    /// out 매개변수로 다음 틱 값도 얻을 수 있음
    /// </summary>
    /// <param name="cmdList"></param>
    /// <param name="curTick"></param>
    /// <param name="nextTick"></param>
    /// <returns></returns>
    public static int GetNextTickIdx(List<FurnaceCommand> cmdList, int curTick, out int nextTick)
    {
        var listLen = cmdList.Count;
        for(var i = 0; i < listLen; i++) {
            if(cmdList[i].Tick <= curTick)
                continue;
            nextTick = cmdList[i].Tick;
            return i;
        }
        
        nextTick = -1;
        return -1;
    }
    
    public static int GetOrderStartTick(int orderNum)
        => orderNum <= MaxOrderNum ? OrderStartTimes[orderNum].StartTick : EndTick;

    public static FurnaceCommand CloneCommandStruct(FurnaceCommand originalCmd, CmdFieldChangeArg[]? fieldsToChange = null)
    {
        var tick    = originalCmd.Tick;
        var channel = originalCmd.Channel;
        var cmdType = originalCmd.CmdType;
        var value1  = originalCmd.Value1;
        var value2  = originalCmd.Value2;

        if(fieldsToChange == null)
            return new FurnaceCommand(tick, channel, cmdType, value1, value2);
        
        
        foreach(var fieldToChange in fieldsToChange) {
            switch(fieldToChange.FieldToChange) {
                case CmdStructField.TICK:     tick    = fieldToChange.IntValue; break;
                case CmdStructField.CHANNEL:  channel = (byte)fieldToChange.IntValue; break;
                case CmdStructField.CMD_TYPE: cmdType = fieldToChange.CmdTypeValue; break;
                case CmdStructField.VALUE1:   value1  = fieldToChange.IntValue; break;
                case CmdStructField.VALUE2:   value2  = fieldToChange.IntValue; break;
                default: throw new ArgumentOutOfRangeException($"Invalid CmdStructField Enum: {fieldToChange.FieldToChange}");
            }
        }

        return new FurnaceCommand(tick, channel, cmdType, value1, value2);
    }

    /// <summary>
    /// Used to convert other command streams that need to be handled during portamento
    /// </summary>
    /// <param name="otherCmdWhilePorta"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetMMLForOtherCmdWhilePortamento(FurnaceCommand otherCmdWhilePorta)
    {
        return otherCmdWhilePorta.CmdType switch {
            CmdType.HINT_VOLUME => $"V{otherCmdWhilePorta.Value1}",
            _                   => throw new ArgumentOutOfRangeException("Invalid CmdType: " + otherCmdWhilePorta.CmdType)
        };
    }
}