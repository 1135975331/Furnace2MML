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
    /// ExactFractionLength == n분음 표기(1, 2, 4, 8, 16, 32, 64,  3, 6, 12, 24, 48, 96)가 가능한가의 여부
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

    public static StringBuilder AppendFracLength(this StringBuilder curOrderSb, int tickLen)
    {
        if(tickLen == 0)
            return curOrderSb;
        
        var mmlFracLen = FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        curOrderSb.Append(mmlFracLen);
        return curOrderSb;
    }
    

    public static StringBuilder FormatNoteLength(int tickLenP, int[] validFractionLength, int defaultFractionLength)
    {
        var tickLen      = tickLenP;
        var isLengthLong = tickLen >= 192; // 길이가 길면 점분음표 표기 시 오류가 발생함
		
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

        for(var i = 0; i < fracLenResultList.Count; i++) { // 변환되어 저장된 값에 따라 문자열 만들기
            var fracLength      = fracLenResultList[i];
            var isDefaultLength = fracLength == defaultFractionLength;
			
            var fracLenStr = fracLength.ToString();

            if(i == 0 && isDefaultLength)
                strBuilder.Append('&');
            else if(i != 0 && !isLengthLong && fracLength == fracLenResultList[i - 1] * 2) // 현재 분수표기 길이 == 이전 분수표기 길이 * 2 => 점n분음표로 나타낼 수 있는가의 여부
                strBuilder.Append('.');
            else
                strBuilder.Append('&').Append(fracLenStr);
            //DebuggingAndTestingTextBox.AppendText($" &{d}");
        }

        // strBuilder = ReplaceComplicatedLengthStr(strBuilder);  // 복잡하게 변환된 길이를 단순하게 되도록 치환함

        return strBuilder.Remove(0, 1);
    }

    // private static readonly string[] DefaultFracLenCmdTypes = ["NOTE_ON", "NOTE_OFF", "HINT_LEGATO"];
    private static readonly CmdType[] DefaultFracLenCmdTypes = [CmdType.NOTE_ON, CmdType.NOTE_OFF, CmdType.HINT_LEGATO];
    public static int GetDefaultNoteFracLenForThisOrder(List<FurnaceCommand> cmdList, int curOrder, int curIdx)
    {
        var nextOrderStartTick = GetOrderStartTick(curOrder+1);
        
        var fracLenCounts = new Dictionary<int, int>();  // <FracLen, Count>
        
        var cmdListLen = cmdList.Count;
        for(var i = curIdx; i < cmdListLen; i++) {
            var curCmd = cmdList[i];

            if(curCmd.CmdType.EqualsAny(DefaultFracLenCmdTypes)) {
                var fracLen = ConvertBetweenTickAndFraction(GetCmdTickLength(cmdList, i));
                
                if(!fracLenCounts.TryAdd(fracLen, 1))
                    fracLenCounts[fracLen]++;
            }

            if(curCmd.Tick >= nextOrderStartTick)
                break;
        }
        
        return GetMostFrequentFracLen();
        
        #region Local Functions
        /* -------------------------------------- Local Function -------------------------------------------- */
        int GetMostFrequentFracLen()
        {
            var maxKey = -1;    // MaxFracLenKey
            var maxValue = -1;  // MaxFracLenCountValue

            foreach(var curKey in fracLenCounts.Keys) {
                if(curKey == -1)
                    continue;
                
                var curValue = fracLenCounts[curKey];
                if(curValue > maxValue) {
                    maxValue = curValue;
                    maxKey   = curKey;
                } else if(curValue == maxValue)
                    maxKey = Math.Min(maxKey, curKey);
            }
            
            return maxKey;
        }
        #endregion
    }

    /// <summary>
    /// Search for the first cmd that meets the condition.
    /// </summary>
    /// <param name="cmdList">List of FurnaceCommand</param>
    /// <param name="startIdx">Start index to search</param>
    /// <param name="predicateToSearch"></param>
    /// <param name="foundCmdIdx">index value where the command is found</param>
    /// <param name="predicateToStopSearching">A condition that stops searching for performance.</param>
    /// <param name="direction">Search direction. Only forward(increasing index) or backward(decreasing index) is valid.</param>
    /// <param name="isCmdFound">if such cmd is found, <c>isCmdFound</c> is <c>true</c>. if cmd is not found, or searching is stopped, <c>isCmdFound</c> is <c>false</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">If direction parameter is neither forward nor backward.</exception>
    /// <returns>The first FurnaceCommand that meets the condition. <br/>
    /// If searching was stopped by <c>predicateToStopSearching</c> condition, returns FurnaceCommand with <c>"SEARCH_STOPPED"</c> CmdType. <br/>
    /// If there is no FurnaceCommand that meet the conditions, returns FurnaceCommand with <c>EndTick</c>Tick and <c>"NOTE_OFF"</c> CmdType.
    /// </returns>
    public static FurnaceCommand GetFirstCertainCmd(List<FurnaceCommand> cmdList, int startIdx, Predicate<FurnaceCommand> predicateToSearch, out bool isCmdFound, out int foundCmdIdx, Predicate<FurnaceCommand>? predicateToStopSearching = null, string direction = "forward")
    {
        var cmdWhenStopped = new FurnaceCommand(-1, 0xFF, 0xFF, "SEARCH_STOPPED", -1, -1);
        
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
    
}