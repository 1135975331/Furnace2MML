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
    private static byte _arpValue = 0; // 0: arpeggio disabled
    private static byte _arpTickSpeed = 1;
    public static void SetArpSpeed(FurnaceCommand cmd)
        => _arpTickSpeed = (byte)cmd.Value1;
    public static void SetArpeggioStatus(FurnaceCommand cmd)
        => _arpValue = (byte)cmd.Value1;

    public static void ConvertNoteOn(FurnaceCommand cmd, int tickLen, ref int defaultOct, StringBuilder curOrderSb)
    {
        var noteNum = cmd.Value1;
        var mmlNote = CmdStreamToMMLUtil.GetMMLNote(noteNum, ref defaultOct);

        if(_arpValue == 0) {
            curOrderSb.Append(mmlNote).AppendFracLength(tickLen);
        } else { // if arpeggio is enabled
            var arpNote1 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue / 16), ref defaultOct);
            var arpNote2 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue % 16), ref defaultOct);
            var fracLenSpeed = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(_arpTickSpeed);
            curOrderSb.Append($"{{{{{mmlNote}{arpNote1}{arpNote2}}}}}").AppendFracLength(tickLen).Append($",{fracLenSpeed}");
        }
    }

    public static void ConvertNoteOff(int tickLen, StringBuilder curOrderSb)
    {
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }
    
    public static void ConvertPanning(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        var leftPan  = cmd.Value1;
        var rightPan = -cmd.Value2;
	    
        // curOrderSb.Append(" px").Append((leftPan + rightPan) / 2).Append(' ');
        curOrderSb.Append("px").Append((leftPan + rightPan) / 2);
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }
    
    public static void ConvertInstrument(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        MainWindow? mainWindow;
        if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) 
            mainWindow = (MainWindow)desktop.MainWindow!;
        else 
            return;
        
        // Conversion Warning: Valid Instrument Type of SSG Channel is @0 ~ @9.
        if(cmd.Channel is >= 6 and <= 8 && cmd.Value1 is not (>= 0 and <= 9)) {
            mainWindow.LogTextBox.Text += $"Warning: Invalid instrument type for SSG Channel found.\nValid instrument type of SSG Channel is @0 ~ @9.\n[Channel: {cmd.Channel}, Order: {cmd.OrderNum:X2}, Tick: {cmd.Tick}]\n";
            cmd.Value1 =  0;
        }

        curOrderSb.Append($"@{cmd.Value1}");
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }

    
    private static readonly CmdType[] CmdTypeToFindVolumeChange = [CmdType.NOTE_ON, CmdType.HINT_LEGATO];
    public static void ConvertVolume(List<FurnaceCommand> cmdList, int curIdx, int tickLen, StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curIdx];
        var volumeValue = curCmd.Value1;
        
        var playingNoteCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "backward", isCmdFound: out var isFoundBackward);
        var nextNoteCmd    = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "forward", isCmdFound: out _);
        
        if(isFoundBackward && (tickLen > 0 || (tickLen == 0 && nextNoteCmd.CmdType == CmdType.HINT_LEGATO)))  // Volume change while note is playing
            curOrderSb.Append('&');
        
        curOrderSb.Append('V').Append(volumeValue);

        var pitchStrOrRest = isFoundBackward ? CmdStreamToMMLUtil.GetPitchStr(playingNoteCmd.Value1 % 12) : "r";
        if(tickLen > 0) 
            curOrderSb.Append(pitchStrOrRest).AppendFracLength(tickLen);
    }

    private const int TICK_OF_FRAC1 = 96;
    private static readonly CmdType[] CmdTypeToFindPrevCmd = [CmdType.HINT_PORTA, CmdType.NOTE_ON];
    private static readonly CmdType[] CmdTypeToFindNextCmd = [CmdType.NOTE_ON, CmdType.NOTE_OFF, CmdType.HINT_PORTA, CmdType.HINT_LEGATO];
    public static void ConvertPortamento(List<FurnaceCommand> cmdList, int curCmdIdx, ref int defaultOct,  StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curCmdIdx];
        
        // MML 문법: a&&{b e}4
        if(curCmd.Value1 == -1)
            return;

        var prevCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => CmdTypeToFindPrevCmd.Contains(cmd.CmdType), direction: "backward", isCmdFound: out _);  
        
        var nextCmdForLength = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, cmd => CmdTypeToFindNextCmd.Contains(cmd.CmdType), direction: "forward", isCmdFound: out _);
        
        var portaLength    = nextCmdForLength.Tick - curCmd.Tick;
        var isPortaLenLong = portaLength > TICK_OF_FRAC1;

        // Length of Portamento cannot be longer than a whole note + a quarter note(fracLen: 1 + 4), so it should be split into parts.
        if(isPortaLenLong) {
            FormatSplitLongPortamento(ref defaultOct);
        } else {
            var prevMMLNote = CmdStreamToMMLUtil.GetMMLNote(prevCmd.Value1, ref defaultOct, false); 
            var curMMLNote  = CmdStreamToMMLUtil.GetMMLNote(curCmd.Value1, ref defaultOct, true);
            curOrderSb.Append($"&{{{prevMMLNote} {curMMLNote}}}").AppendFracLength(portaLength);
        }
        
        #region Local Functions
        /* -------------------------------------- Local Functions -------------------------------------------- */
        void FormatSplitLongPortamento(ref int defaultOct)
        {
            var portaLenInFrac1 = (double)portaLength / TICK_OF_FRAC1;
            var splitCount = (int)Math.Ceiling(portaLenInFrac1);
            var deltaNoteNum = curCmd.Value1 - prevCmd.Value1;
            var deltaNoteNumPerFrac1 = (1 / portaLenInFrac1) * deltaNoteNum;

            var prevNoteNum = prevCmd.Value1;
            var curNoteNum  = prevCmd.Value1;
            
            for(var i = 1; i <= splitCount; i++) {
                if(i != splitCount)
                    curNoteNum += (int)Math.Round(deltaNoteNumPerFrac1);
                else
                    curNoteNum = curCmd.Value1;

                var prevMMLNote = CmdStreamToMMLUtil.GetMMLNote(prevNoteNum, ref defaultOct, false); 
                var curMMLNote  = CmdStreamToMMLUtil.GetMMLNote(curNoteNum, ref defaultOct, true);

                var length = i == splitCount ? portaLength % TICK_OF_FRAC1 : TICK_OF_FRAC1;
                curOrderSb.Append($"&{{{prevMMLNote} {curMMLNote}}}").AppendFracLength(length);

                prevNoteNum = curNoteNum;
            }
        }
        #endregion
    }

    public static void ConvertLegato(List<FurnaceCommand> cmdList, int curIdx, int tickLen, ref int defaultOct, StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curIdx];
        
        var noteNum = curCmd.Value1;
        var mmlNote = CmdStreamToMMLUtil.GetMMLNote(noteNum, ref defaultOct).ToString();
        
        // var prevNotePlayCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), predicateToStopSearching: cmd => cmd.CmdType == CmdType.NOTE_OFF, direction: "backward", isCmdFound: out var isFound);
        // var prevVolumeChangeCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curIdx, cmd => cmd.CmdType == CmdType.HINT_LEGATO, predicateToStopSearching: cmd => CmdTypeToFindVolumeChange.Contains(cmd.CmdType), direction: "backward", isCmdFound: out _);
        
        // if(isFound )  // if NOTE_ON or HINT_LEGATO is found, '&' is appended at ConvertVolume method
        curOrderSb.Append('&');
        curOrderSb.Append(mmlNote).AppendFracLength(tickLen);
    }


    public static void ResetAllFields()
    {
        _arpValue = 0;
        _arpTickSpeed = 1;
    }

}