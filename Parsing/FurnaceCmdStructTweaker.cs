using System;
using System.Collections.Generic;
using Furnace2MML.Etc;
using Furnace2MML.Utils;
using static Furnace2MML.Etc.PublicValue;
using static Furnace2MML.Utils.CmdStreamToMMLUtil;
namespace Furnace2MML.Parsing;

public class FurnaceCmdStructTweaker
{
    /// <summary>
    /// 각 Order의 시작부분에 NOTE_OFF 명령을 끼워넣는 메소드
    /// </summary>
    public void InsertNoteOffAtStartOfEachOrder()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdCh    = NoteCmds[chNum];
            var noteCmdChLen = noteCmdCh.Count;
            if(noteCmdChLen == 0)
                continue;
            
            InsertNoteOffToList(noteCmdCh, true);
        }
        
        InsertNoteOffToList(DrumCmds, false);
        
        return;
        
        #region Local Functions
        /* --------------------------------- Local Function ------------------------------------- */
        void InsertNoteOffToList(List<FurnaceCommand> cmdList, bool isNoteCmd)
        {
            var cmdListLen = cmdList.Count;
            for(var i = 0; i < cmdListLen; i++) {
                var curCmd         = cmdList[i];
                var curCmdOrderNum = curCmd.OrderNum;
                var nextOrderTick  = GetOrderStartTick(curCmdOrderNum + 1);
                
                if(isNoteCmd && curCmd.CmdType is CmdType.NOTE_ON or CmdType.HINT_LEGATO)
                    continue;
                if(curCmdOrderNum >= MaxOrderNum)
                    break;
                
                if(i != cmdListLen - 1) { // 마지막 인덱스가 아닌 경우에는 현재 Cmd와 다음 Cmd의 Order가 서로 다를때만 NOTE_OFF삽입
                    var nextCmd         = cmdList[i + 1];
                    var nextCmdOrderNum = nextCmd.OrderNum;

                    if(curCmdOrderNum >= nextCmdOrderNum) // curNoteOrderNum < nextNoteOrderNum 인 경우 아래 코드 실행
                        continue;
                    if(nextCmd.Tick == GetOrderStartTick(curCmdOrderNum+1)) // 다음 Order가 시작하는 틱에 Cmd가 있는 경우
                        continue;
                }

                var noteOffCmd = new FurnaceCommand(nextOrderTick, (byte)(curCmdOrderNum+1), curCmd.Channel, "NOTE_OFF", 0, 0);
                cmdList.Insert(i + 1, noteOffCmd);
                cmdListLen++;
            }
        }
        #endregion
    }

    /// <summary>
    /// Remove all invalid NOTE_ON Commands.
    /// (0xB4(null) || &lt; 0x48(C-1)) are the invalid.
    /// </summary>
    public void RemoveInvalidNoteOnCommands()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList    = NoteCmds[chNum];
            var noteCmdChLen = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            for(var i = 0; i < noteCmdChLen - 1; i++) {
                var curCmd = noteCmdChList[i];

                if(curCmd is { CmdType: CmdType.NOTE_ON, Value1: 0xB4+12 or < 0x48 })  // value1 of NOTE_ON Commands are added by 12(1 octave)
                    noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void RemoveUnnecessaryPortamentoBinaryCommands()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList    = NoteCmds[chNum];
            var noteCmdChLen = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            for(var i = 0; i < noteCmdChLen - 1; i++) {
                var curCmd = noteCmdChList[i];

                if(curCmd.CmdType == CmdType.HINT_PORTA && curCmd is { Value2: 0 } || curCmd.CmdType == CmdType.PRE_PORTA)
                    noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
            }
        
            // value2 == 0x00 인 HINT_PORTA 삭제.
            // PRE_PORTA의 value2는 preset delay를 나타내는 것으로 추청, 어쩌면 preset delay == porta 지속시간
        }
    }

    public void RemoveUnnecessaryLegatoCommands()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = NoteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            var curTick     = -1;
            var zeroHintArpFound = false;
            
            for(var i = 0; i < noteCmdChLen - 1; i++) {
                var curCmd = noteCmdChList[i];
                if(curTick != curCmd.Tick) { // 틱이 바뀌면 초기화
                    curTick         = curCmd.Tick;
                    zeroHintArpFound = false;
                }

                if(!zeroHintArpFound && curCmd.CmdType == CmdType.HINT_ARPEGGIO && curCmd is { Value1: 0, Value2: 0 })  // when curCmd is HINT_ARPEGGIO 0 0
                    zeroHintArpFound = true;

                if(zeroHintArpFound && curCmd.CmdType == CmdType.HINT_LEGATO)
                    noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
            }
        }
    }
    
    
    /// <summary>
    /// 
    /// </summary>
    public void RemoveUnnecessaryArpeggioCommands()
    {
        var noteCmdChLen  = DrumCmds.Count;
        for(var i = 0; i < noteCmdChLen - 1; i++) {
            var curCmd = DrumCmds[i];

            if(curCmd.CmdType == CmdType.HINT_ARPEGGIO)  // if curCmd is HINT_ARPEGGIO 0 0
                DrumCmds.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
        }
    }

    public void ReorderCommands()  
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = NoteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            noteCmdChList.Sort();
        }
        
        DrumCmds.Sort();
    }

    public void CorrectHintPortaPitch()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = NoteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            for(var i = 0; i < noteCmdChLen; i++) {
                var curCmd = noteCmdChList[i];
                if(curCmd.CmdType != CmdType.HINT_PORTA)
                    continue;

                noteCmdChList[i] = new FurnaceCommand(curCmd.Value1+60, curCmd.Value2, curCmd);
            }
        }
    }

    /// <summary>
    /// Value1 and Value2 of the Vibrato Command Stream are reversed.
    /// This method corrects them.
    /// </summary>
    public void CorrectVibratoValueOrder()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = NoteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            for(var i = 0; i < noteCmdChLen; i++) {
                var curCmd = noteCmdChList[i];
                if(curCmd.CmdType != CmdType.VIBRATO)
                    continue;

                noteCmdChList[i] = new FurnaceCommand(curCmd.Value2, curCmd.Value1, curCmd);
            }
        }
    }

    public void InsertCmdForZeroLenCmd()
    {
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = NoteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            InsertCmd(noteCmdChList);
        }
        
        InsertCmd(DrumCmds);
        
        return;
        #region Local Functions
        /* --------------------------------------- Local Functions -------------------------------------------- */
        void InsertCmd(List<FurnaceCommand> cmdList)
        {
            var noteCmdChLen = cmdList.Count;
            for(var i = 1; i < noteCmdChLen; i++) {
                var curCmd = cmdList[i];
                var predicate = new Predicate<FurnaceCommand>(cmd => cmd.CmdType is CmdType.NOTE_ON or CmdType.NOTE_OFF);
                var prevCmd = GetFirstCertainCmd(cmdList, i, predicate, out _, out _, direction: "backward");
                var nextCmd = GetFirstCertainCmd(cmdList, i, predicate, out _, out _, direction: "forward");

                if(curCmd.CmdType is not (CmdType.HINT_ARPEGGIO or CmdType.HINT_ARP_TIME or CmdType.INSTRUMENT))
                    continue;
                if(curCmd.Tick == prevCmd.Tick || curCmd.Tick == nextCmd.Tick)
                    continue;
                
                switch(prevCmd.CmdType) {
                    case CmdType.NOTE_ON:  cmdList.InsertAtIdxLoop(ref i, ref noteCmdChLen, CloneCommandStruct(prevCmd, [new CmdFieldChangeArg(CmdStructField.TICK, curCmd.Tick), new CmdFieldChangeArg(CmdType.HINT_LEGATO)])); break;
                    case CmdType.NOTE_OFF: cmdList.InsertAtIdxLoop(ref i, ref noteCmdChLen, CloneCommandStruct(prevCmd, [new CmdFieldChangeArg(CmdStructField.TICK, curCmd.Tick)])); break;
                }
            }
        }
        #endregion
    }
}