using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Furnace2MML.Etc;
using Furnace2MML.Utils;

namespace Furnace2MML.Conversion;

public static class ConvertCmdStreamToMML
{
    private static bool _isAmpersandAlreadyAdded = false;

    private static byte _arpValue     = 0; // 0: arpeggio disabled
    private static byte _arpTickSpeed = 1;

    private static byte _vibSpeed       = 0;
    private static byte _vibDepth       = 0;
    private static byte _vibShape       = 0;
    private static byte _vibRange       = 0x08;  // Default Range: 0x10 (16)
    private static byte _vibActualDepth = 0;


    public static void ConvertCmdStream(List<FurnaceCommand> noteCmdCh, int i, ref int prevOctave, StringBuilder curOrderSb)
    {
        var noteCmd = noteCmdCh[i];
        var tickLen = noteCmd.TickLen;
        // var tickLen = CmdStreamToMMLUtil.GetCmdTickLength(noteCmdCh, i);

        switch(noteCmd.CmdType) {
            case CmdType.HINT_ARP_TIME: SetArpSpeed(noteCmd, tickLen, curOrderSb); break;
            case CmdType.HINT_ARPEGGIO: SetArpStatus(noteCmd, tickLen, curOrderSb); break;

            case CmdType.VIB_SHAPE:     ConvertVibShape(noteCmd, tickLen, curOrderSb); break;
            case CmdType.VIB_RANGE:     ConvertVibRange(noteCmd, tickLen, curOrderSb); break;
            case CmdType.VIBRATO:       ConvertVibrato(noteCmdCh, i, tickLen, curOrderSb); break;
            case CmdType.INSTRUMENT:    ConvertInstrument(noteCmd, tickLen, curOrderSb); break;
            case CmdType.PANNING:       ConvertPanning(noteCmd, tickLen, curOrderSb); break;
            case CmdType.HINT_VOLUME:   ConvertVolume(noteCmdCh, i, tickLen, curOrderSb); break;
            case CmdType.NOTE_ON:       ConvertNoteOn(noteCmd, tickLen, ref prevOctave, curOrderSb); break;
            case CmdType.NOTE_OFF:      ConvertNoteOff(tickLen, curOrderSb); break;
            case CmdType.HINT_PORTA:    ConvertPortamento(noteCmdCh, i, ref prevOctave, curOrderSb); break;
            case CmdType.HINT_LEGATO:   ConvertLegato(noteCmdCh, i, tickLen, ref prevOctave, curOrderSb); break;
            // default: ignored
        }

        _isAmpersandAlreadyAdded = false;
        noteCmdCh[i] = new FurnaceCommand(true, noteCmd);
    }

    public static void ResetAllFields()
    {
        _arpValue = 0;
        _arpTickSpeed = 1;

        _vibDepth = 0;
        _vibSpeed = 0;
        _vibRange = 0x08;
    }


    private static void SetArpSpeed(FurnaceCommand  cmd, int tickLen, StringBuilder curOrderSb) { _arpTickSpeed = (byte)cmd.Value1; AppendRestForTheCmd(tickLen, curOrderSb); }
    private static void SetArpStatus(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb) { _arpValue     = (byte)cmd.Value1; AppendRestForTheCmd(tickLen, curOrderSb); }

    /// <summary>
    ///
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="tickLen"></param>
    /// <param name="curOrderSb"></param>
    /// <see href="https://pigu-a.github.io/pmddocs/pmdmml.htm#9-1"/>
    /// <see href="https://pigu-a.github.io/pmddocs/pmdmml.htm#9-2"/>
    private static void ConvertVibShape(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        _vibShape = (byte)cmd.Value1;

        var mwValue = (byte)cmd.Value1 switch {
            0x00 => '0', // sine(default) -> triangle1 (MW0)
            0x01 => '4', // sine(upper)   -> triangle2 (MW4)
            0x02 => '?', // sine(lower)   -> n/a
            0x03 => '0', // triangle      -> triangle1 (MW0)
            0x04 => '1', // ramp up       -> sawtooth  (MW1)
            0x05 => '?', // ramp down     -> n/a
            0x06 => '2', // square        -> square    (MW2)
            0x07 => '3', // random        -> random    (MW3)
            0x08 => '?', // square(up)    -> n/a
            0x09 => '?', // square(down)  -> n/a
            0x0a => '?', // half sine(up) -> n/a
            0x0b => '?', // half sine(up) -> n/a
            _    => '?'
        };

        if(mwValue == '?')
            PrintLog.LogWarn($"Warning: Invalid Vibrato Shape value found.\nExpected Shape value is 00, 01, 03, 04, 06 or 07.\n{MiscellaneousConversionUtil.GetFurnaceCommandPositionInfo(cmd)}\n");

        curOrderSb.Append($"MW{mwValue} ");

        AppendRestForTheCmd(tickLen, curOrderSb);
    }
    private static void ConvertVibRange(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb) { _vibRange = (byte)cmd.Value1; AppendRestForTheCmd(tickLen, curOrderSb); }

    private static readonly CmdType[] VibratoPlayingCmdTypeToFind = [CmdType.NOTE_ON, CmdType.HINT_LEGATO];  // Command type to find array for vibrato command
    private static readonly CmdType[] VibratoNextCmdTypeToFind = [CmdType.NOTE_ON, CmdType.HINT_LEGATO, CmdType.VIBRATO];  // Command type to find array for vibrato command
    private static void ConvertVibrato(List<FurnaceCommand> cmdList, int curIdx, int tickLen, StringBuilder curOrderSb)
    {
        var cmd = cmdList[curIdx];
        _vibSpeed = (byte)cmd.Value1;
        _vibDepth = (byte)cmd.Value2;

        _vibActualDepth = (byte)(_vibRange * (float)_vibDepth / 0xF);

        var mmlVibSpdValue = 0x08 - _vibSpeed / 2;

        if(mmlVibSpdValue < 0)
            throw new Exception("MML LFO Speed Value is out of the range.");

        var playingNoteCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmdIter => VibratoPlayingCmdTypeToFind.Contains(cmdIter.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "backward", isCmdFound: out var isFoundInSmallerIdx, foundCmdIdx: out _);
        var nextNoteCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmdIter => VibratoNextCmdTypeToFind.Contains(cmdIter.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "forward", isCmdFound: out _, foundCmdIdx: out _);

        if(!_isAmpersandAlreadyAdded && isFoundInSmallerIdx && (tickLen > 0 || (tickLen == 0 && nextNoteCmd.CmdType == CmdType.HINT_LEGATO))) { // Vibrato while note is playing
            curOrderSb.Append('&');
            _isAmpersandAlreadyAdded = true;
        }

        curOrderSb.Append(_vibSpeed == 0 || _vibDepth == 0 ? "*0" : $"M0,{mmlVibSpdValue},{_vibActualDepth},1 *1");

        var pitchStrOrRest = isFoundInSmallerIdx ? CmdStreamToMMLUtil.GetPitchStr(playingNoteCmd.Value1 % 12) : "r";
        if(tickLen > 0)
            curOrderSb.Append(pitchStrOrRest).AppendNoteLength(tickLen);
    }


    private static void ConvertNoteOn(FurnaceCommand cmd, int tickLen, ref int defaultOct, StringBuilder curOrderSb)
    {
        var noteNum = cmd.Value1;
        var mmlNote = CmdStreamToMMLUtil.GetMMLNote(noteNum, ref defaultOct);

        if(_arpValue == 0) {
            curOrderSb.Append(mmlNote).AppendNoteLength(tickLen);
        } else { // if arpeggio is enabled
            var arpNote1 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue / 16), ref defaultOct);
            var arpNote2 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue % 16), ref defaultOct);
            var fracLenSpeed = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(_arpTickSpeed); curOrderSb.Append($"{{{{{mmlNote}{arpNote1}{arpNote2}}}}}").AppendNoteLength(tickLen).Append($",{fracLenSpeed}"); }
    }

    private static void ConvertNoteOff(int tickLen, StringBuilder curOrderSb)
    {
        AppendRestForTheCmd(tickLen, curOrderSb);
    }

    private static void ConvertPanning(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        var leftPan  = cmd.Value1;
        var rightPan = -cmd.Value2;
	    
        // curOrderSb.Append(" px").Append((leftPan + rightPan) / 2).Append(' ');
        curOrderSb.Append("px").Append((leftPan + rightPan) / 2);
        AppendRestForTheCmd(tickLen, curOrderSb);
    }
    
    private static void ConvertInstrument(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        // Conversion Warning: Valid Instrument Type of SSG Channel is @0 ~ @9.
        if(cmd.Channel is >= 6 and <= 8 && cmd.Value1 is not (>= 0 and <= 9)) {
            PrintLog.LogWarn($"Warning: Invalid instrument type for SSG Channel found.\nExpected instrument type of SSG Channel is @0 ~ @9.\n{MiscellaneousConversionUtil.GetFurnaceCommandPositionInfo(cmd)}\n");
            cmd.Value1 =  0;
        }

        curOrderSb.Append($"@{cmd.Value1}");
        AppendRestForTheCmd(tickLen, curOrderSb);
    }

    
    private static readonly CmdType[] VolumeChangeCmdTypeToFind = [CmdType.NOTE_ON, CmdType.HINT_LEGATO];  // Command type to find array for volume change command
    private static void ConvertVolume(List<FurnaceCommand> cmdList, int curIdx, int tickLen, StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curIdx];
        var volumeValue = curCmd.Value1;

        var playingNoteCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmdIter => VolumeChangeCmdTypeToFind.Contains(cmdIter.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "backward", isCmdFound: out var isFoundBackward, foundCmdIdx: out _);
        var nextNoteCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmdIter => VolumeChangeCmdTypeToFind.Contains(cmdIter.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "forward", isCmdFound: out _, foundCmdIdx: out _);

        if(!_isAmpersandAlreadyAdded && isFoundBackward && (tickLen > 0 || (tickLen == 0 && nextNoteCmd.CmdType == CmdType.HINT_LEGATO))) { // Volume change while note is playing
            curOrderSb.Append('&');
            _isAmpersandAlreadyAdded = true;
        }

        curOrderSb.Append('V').Append(volumeValue);

        var pitchStrOrRest = isFoundBackward ? CmdStreamToMMLUtil.GetPitchStr(playingNoteCmd.Value1 % 12) : "r";
        if(tickLen > 0) 
            curOrderSb.Append(pitchStrOrRest).AppendNoteLength(tickLen);
    }

    private const int TICK_OF_FRAC1 = 96;
    private static readonly CmdType[] CmdTypeToFindPrevCmd = [CmdType.HINT_LEGATO, CmdType.NOTE_ON, CmdType.HINT_PORTA];
    private static readonly CmdType[] CmdTypeToFindOtherCmd = [CmdType.HINT_VOLUME];
    private static readonly CmdType[] CmdTypeToFindPortaStopCmd = [CmdType.NOTE_ON, CmdType.NOTE_OFF, CmdType.HINT_PORTA, CmdType.HINT_LEGATO];
    private static void ConvertPortamento(List<FurnaceCommand> cmdList, int curCmdIdx, ref int defaultOct,  StringBuilder curOrderSb)
    {
        // HINT_PORTA에서 포르타멘토 시작, HINT_LEGATO 시점에서 음 상승을 종료하고 도착음 유지.
        // HINT_LEGATO(도착음 도달시점) 앞에 NOTE_OFF가 있는 경우는 도착음에 도달하기 이전에 NOTE_OFF가 찍혀있는 경우임
        //   => HINT_PORTA ~ HINT_LEGATO 사이의 시간, 시작음과 도착음을 알고 있으므로 올라가는 중에 NOTE_OFF시 음이 어디까지 올라가다가 끊어지는지 알 수 있음
        
        var curCmd = cmdList[curCmdIdx];

        // MML Grammar: a&&{b e}4
        if(curCmd.Value1 == -1)
            return;

        var prevNoteCmd       = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => CmdTypeToFindPrevCmd.Contains(cmd.CmdType), direction: "backward", isCmdFound: out _, foundCmdIdx: out _);
        var portaStopCmd      = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => CmdTypeToFindPortaStopCmd.Contains(cmd.CmdType), direction: "forward", isCmdFound: out _, foundCmdIdx: out var portaStopCmdIdx);
        var legatoForThePorta = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => cmd.CmdType == CmdType.HINT_LEGATO, direction: "forward", isCmdFound: out _, foundCmdIdx: out var legatoCmdIdx, predicateToStopSearching: cmd => cmd.CmdType == CmdType.HINT_PORTA);
        /* bool isThereOtherCmdWhilePorta */ CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => CmdTypeToFindOtherCmd.Contains(cmd.CmdType), direction: "forward", isCmdFound: out var isThereOtherCmdWhilePorta, foundCmdIdx: out _, predicateToStopSearching: cmd => CmdTypeToFindPortaStopCmd.Contains(cmd.CmdType));

        // CmdStreamToMMLUtil.GetFirstCertainCmd method returns FurnaceCommand struct with CmdType.SEARCH_STOPPED if searching is stopped by condition of predicateToStopSearching parameter.
        // GetFirstCertainCmd method stop searching when the condition is met, in order to prevent HINT_LEGATO cmd struct for another portamento cmd struct being assigned to legatoForThePorta variable
        if(legatoForThePorta.CmdType == CmdType.SEARCH_STOPPED)
            legatoForThePorta = portaStopCmd;

        var portaLength       = legatoCmdIdx == 1 ? portaStopCmd.Tick - curCmd.Tick : legatoForThePorta.Tick - curCmd.Tick;
        var portaActualLength = portaStopCmd.Tick - curCmd.Tick;

        var isPortaEndBeforeLegato = legatoCmdIdx != -1 && portaActualLength < portaLength;  // NOTE_OFF while portamento
        var isPortaLenLong         = portaActualLength > TICK_OF_FRAC1;

        var startPitch = prevNoteCmd.Value1;
        var endPitch   = curCmd.Value1;

        if(portaLength == 0)
            return;

        // Length of Portamento cannot be longer than a whole note + a quarter note(fracLen: 1 + 4), so it should be split into parts.
        if(isThereOtherCmdWhilePorta || isPortaLenLong) {
            FormatSplitPortamento(GetActualEndPitch(), ref defaultOct);
        } else {
            var startMMLNote   = CmdStreamToMMLUtil.GetMMLNote(startPitch, ref defaultOct, false);
            var targetMMLNote  = CmdStreamToMMLUtil.GetMMLNote(GetActualEndPitch(), ref defaultOct, true);
            curOrderSb.Append($"&{{{startMMLNote} {targetMMLNote}}}").AppendNoteLength(portaActualLength);
        }

        if(isPortaEndBeforeLegato)
            cmdList[legatoCmdIdx] = new FurnaceCommand(legatoForThePorta.Value1, -2, legatoForThePorta);  // Mark the HINT_LEGATO Command no longer necessary

        #region Local Functions
        /* -------------------------------------- Local Functions -------------------------------------------- */
        int GetActualEndPitch()
        {
            if(!isPortaEndBeforeLegato)
                return endPitch;

            var progressPercent = (float)portaActualLength / portaLength;
            var deltaPitch      = Math.Abs(endPitch - startPitch);
            return (int)(startPitch < endPitch ? startPitch + deltaPitch * progressPercent : startPitch - deltaPitch * progressPercent);
        }

        void FormatSplitPortamento(int actualEndPitch, ref int defaultOct)
        {
            var otherCmdList = new List<FurnaceCommand>();
            for(var i=curCmdIdx+1;; i++) {
                var cmd = cmdList[i];
                if(CmdTypeToFindPortaStopCmd.Contains(cmd.CmdType))
                    break;

                if(cmd.TickLen == 0)
                    continue;

                otherCmdList.Add(cmd);
                cmdList[i] = new FurnaceCommand(true, cmd);
            }

            var otherCmdListLen      = otherCmdList.Count;
            var totalDeltaNoteNum   = actualEndPitch - startPitch;
            var tickLenPerEach      = new List<int[]>();  // tickLenPerEach[i][1] == -1: first portamento tickLen, tickLenPerEach[i][1] >= 0: index value of otherCmdList
            for(var i = 0; i < otherCmdListLen+1; i++) {
                var tickLen = i == 0 ? curCmd.TickLen : otherCmdList[i - 1].TickLen;

                switch(tickLen) {
                    case > TICK_OF_FRAC1: {
                        var portaLenInFrac1         = (double)portaActualLength / TICK_OF_FRAC1;
                        var longPortaFrac1PartCount = (int)Math.Floor(portaLenInFrac1);

                        for(var b = 0; b < longPortaFrac1PartCount; b++)
                            tickLenPerEach.Add([TICK_OF_FRAC1, i-1]);

                        tickLenPerEach.Add([portaActualLength % TICK_OF_FRAC1, i-1]);
                        break;
                    }
                    case < TICK_OF_FRAC1:
                        tickLenPerEach.Add([tickLen, i-1]);
                        break;
                }

                // if(tickLen == 0), do nothing
            }

            var portaPartCount = tickLenPerEach.Count;
            var targetNoteNumPerEach = new int[portaPartCount];

            var accumDeltaNoteDecimalPart = 0.0f;  // accumulatedDeltaNoteDecimalPart
            for(var i = 0; i < portaPartCount; i++) {
                var deltaNoteNumFloat = (float)tickLenPerEach[i][0] / portaActualLength * totalDeltaNoteNum;
                accumDeltaNoteDecimalPart += deltaNoteNumFloat % 1;

                var deltaNoteNum = (int)deltaNoteNumFloat;
                if(Math.Abs(accumDeltaNoteDecimalPart) >= 1) {
                    switch(accumDeltaNoteDecimalPart) {  // if decimal part is larger than or equal to 1, add 1 to deltaNoteNum, and vice versa.
                        case > 0:
                            deltaNoteNum              += 1;
                            accumDeltaNoteDecimalPart -= 1;
                            break;
                        case < 0:
                            deltaNoteNum              -= 1;
                            accumDeltaNoteDecimalPart += 1;
                            break;
                    }
                }

                if(i == 0)
                    targetNoteNumPerEach[i] = startPitch + deltaNoteNum;
                else if(i == portaPartCount-1)
                    targetNoteNumPerEach[i] = actualEndPitch;
                else
                    targetNoteNumPerEach[i] = targetNoteNumPerEach[i - 1] + deltaNoteNum;
            }


            var startNoteNum  = startPitch;
            for(var i = 0; i < portaPartCount; i++) {
                var targetNoteNum = targetNoteNumPerEach[i];

                var startMMLNote    = CmdStreamToMMLUtil.GetMMLNote(startNoteNum, ref defaultOct, false);
                var targetMMLNote   = CmdStreamToMMLUtil.GetMMLNote(targetNoteNum, ref defaultOct, true);
                var length          = tickLenPerEach[i][0];
                var otherCmdListIdx = tickLenPerEach[i][1];

                if(otherCmdListIdx != -1) {  // tickLenPerEach[i][1] == -1: first portamento tickLen, tickLenPerEach[i][1] >= 0: index value of otherCmdList
                    var otherCmdMML = CmdStreamToMMLUtil.GetMMLForOtherCmdWhilePortamento(otherCmdList[otherCmdListIdx]);
                    curOrderSb.Append('&').Append(otherCmdMML);
                }
                curOrderSb.Append($"&{{{startMMLNote} {targetMMLNote}}}").AppendNoteLength(length);

                startNoteNum = targetNoteNum;
            }
        }
        #endregion
    }
    

    private static void ConvertLegato(List<FurnaceCommand> cmdList, int curIdx, int tickLen, ref int defaultOct, StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curIdx];
        
        if(curCmd.Value2 == -2) {  // if the command has been marked no longer necessary by ConvertPortamento method
            ConvertNoteOff(tickLen, curOrderSb);  // then treat the command as NOTE_OFF 
            return;
        }
        
        var noteNum = curCmd.Value1;
        var mmlNote = CmdStreamToMMLUtil.GetMMLNote(noteNum, ref defaultOct).ToString();
        
        // var prevNotePlayCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "backward", isCmdFound: out var isFound);
        // var prevVolumeChangeCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => cmd.CmdType == CmdType.HINT_LEGATO, predicateToStopSearching: cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), direction: "backward", isCmdFound: out _);
        
        // if(isFound )  // if NOTE_ON or HINT_LEGATO is found, '&' is appended at ConvertVolume method
        curOrderSb.Append('&').Append(mmlNote).AppendNoteLength(tickLen);
    }

    private static void AppendRestForTheCmd(int tickLen, StringBuilder curOrderSb)
    {
        if(tickLen > 0)
            curOrderSb.Append('r').AppendNoteLength(tickLen);
    }
}