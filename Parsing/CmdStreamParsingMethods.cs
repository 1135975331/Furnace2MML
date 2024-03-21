using System;
using System.Collections.Generic;
using Furnace2MML.Etc;
using Furnace2MML.Utils;
using static Furnace2MML.Etc.PublicValue;
using static Furnace2MML.Utils.CmdStreamToMMLUtil;
namespace Furnace2MML.Parsing;

public class CmdStreamParsingMethods
{
       /// <summary>
    /// 마지막 줄이 >> LOOP 0 인 경우, Order의 첫 줄에 노트가 있으면 중복되는 Command Stream이 생김
    /// 이 메소드는 이러한 중복을 제거함
    /// (중복을 제거하면서 curTick값도 재조정함)
    /// </summary>
    /// <param name="curTick"></param>
    /// <param name="lastLine"></param>
    public void RemoveDuplicatedCommandsAtTheEnd(ref int curTick, string lastLine)
    {
        if(lastLine.Equals(">> END"))  //  There's no duplicates if the lastLine is ">> END"
            return;
        
        var noteCmds = NoteCmds;
        var drumCmds = DrumCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdCh    = noteCmds[chNum];
            var noteCmdChLen = noteCmdCh.Count;
            if(noteCmdChLen == 0)
                continue;
            
            RemoveDuplicatedCommand(noteCmdCh);
        }
        
        RemoveDuplicatedCommand(drumCmds);
        
        #region Local Functions
        /* -------------------------------- Local Functions ----------------------------------- */
        void RemoveDuplicatedCommand(List<FurnaceCommand> cmdList) 
            => cmdList.RemoveAll(cmd => cmd.Tick >= EndTick);
        #endregion
    }
 
    
    /// <summary>
    /// 각 Order의 시작부분에 NOTE_OFF 명령을 끼워넣는 메소드
    /// </summary>
    public void InsertNoteOffAtStartOfEachOrder()
    {
        var noteCmds = NoteCmds;
        var drumCmds = DrumCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdCh    = noteCmds[chNum];
            var noteCmdChLen = noteCmdCh.Count;
            if(noteCmdChLen == 0)
                continue;
            
            InsertNoteOffToList(noteCmdCh, true);
        }
        
        InsertNoteOffToList(drumCmds, false);
        
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
                
                if(isNoteCmd && curCmd.CmdType == CmdType.NOTE_ON)
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
    /// 
    /// </summary>
    public void RemoveUnnecessaryPortamentoCommands() 
    {
        var noteCmds = NoteCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList    = noteCmds[chNum];
            var noteCmdChLen = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            var curTick     = -1;
            var cmdsToRemove = new List<FurnaceCommand>();
            
            var hintPortaFound  = false;
            var prePortaFound   = false;
            var hintLegatoFound = false;
            
            for(var i = 0; i < noteCmdChLen - 1; i++) {
                var curCmd = noteCmdChList[i];
                if(curTick != curCmd.Tick) { // 틱이 바뀌면 초기화
                    hintPortaFound  = false;
                    prePortaFound   = false;
                    hintLegatoFound = false;
                    curTick         = curCmd.Tick;
                    cmdsToRemove.Clear();
                }

                switch(curCmd.CmdType) {
                    case CmdType.HINT_PORTA:  
                        hintPortaFound = true;
                        if(curCmd.Value2 == 0) // If Value2 of the HINT_PORTA is 0, it's useless
                            noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
                        else
                            cmdsToRemove.Add(curCmd);
                        break;
                    case CmdType.PRE_PORTA: 
                        prePortaFound = true;
                        noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
                        break;
                    case CmdType.HINT_LEGATO:
                        hintLegatoFound = true;
                        if(IsUnnecessaryLegatoCmd(noteCmdChList, i))  // If Value2 of the HINT_PORTA is 0, it's unnecessary
                            noteCmdChList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
                        else
                            cmdsToRemove.Add(curCmd);
                        break;
                }
                
                
                if(hintPortaFound && prePortaFound && hintLegatoFound) { // 같은 틱 내에 해당 3개의 명령이 모두 발견된 경우 Portamento 관련 명령 모두 삭제
                    foreach(var cmd in cmdsToRemove)
                        noteCmdChList.RemoveIdxLoop(cmd, ref noteCmdChLen);
                    
                    i = GetNextTickIdx(noteCmdChList, curTick, out _) - 1;
                }
                //  같은 틱 내에 HINT_PORTA, PRE_PORTA, HINT_LEGATO가 모두 발견되는 경우
                //  해당 틱 내의 세 명령어를 모두 삭제함
            }
        }
        return;
        
        #region Local Functions
        /* --------------------------------- Local Functions ------------------------------------- */
        bool IsUnnecessaryLegatoCmd(List<FurnaceCommand> cmdList, int curIdx)
        {
            var noteOnFound = false;
            var hintPortaFound = false;
            for(var i=curIdx-1; i>=0; i--) {
                var cmd = cmdList[i];
                
                switch(cmd.CmdType) {
                    case CmdType.NOTE_ON:
                        noteOnFound = true;
                        break;
                    case CmdType.HINT_PORTA:
                        hintPortaFound = true;
                        break;
                    case CmdType.NOTE_OFF when (!hintPortaFound):
                        return true;
                }

                if(noteOnFound || hintPortaFound)
                    return false;
            }
            
            return false;
        }
        #endregion
    }

    public void RemoveUnnecessaryLegatoCommands()
    {
        var noteCmds = NoteCmds;

        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = noteCmds[chNum];
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
    /// Remove duplicated and fix invalid value of NOTE_ON command generated by retrigger effect
    /// </summary>
    public void FixRetriggerCommands()
    {
        var noteCmds = NoteCmds;

        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = noteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            FixRetriggerCmds(noteCmdChList, false);
        }
        
        FixRetriggerCmds(DrumCmds, true);
        
        return;
        #region Local Functions
        /* --------------------------------------- Local Functions -------------------------------------------- */
        void FixRetriggerCmds(List<FurnaceCommand> cmdList, bool isDrum)
        {
            var noteCmdChLen = cmdList.Count;
            for(var i = 1; i < noteCmdChLen; i++) {
                var curCmd = cmdList[i];
                var predicate = isDrum ? new Predicate<FurnaceCommand>(cmd => cmd.CmdType == CmdType.NOTE_ON && cmd.Channel == curCmd.Channel)
                                       : new Predicate<FurnaceCommand>(cmd => cmd.CmdType == CmdType.NOTE_ON);
                var prevNoteOnCmd = GetFirstCertainCmd(cmdList, i, predicate, direction: "backward", isCmdFound: out _);
                var nextNoteOnCmd = GetFirstCertainCmd(cmdList, i, predicate, direction: "forward", isCmdFound: out _);

                // is invalid note number (Value1 of the NOTE_ON generated by retrigger effect is 2147483647)    0 ~ 119 (C-0 ~ B-9)
                if(curCmd is not { CmdType: CmdType.NOTE_ON, Value1: < 0 or > 119 })
                    continue;
                
                if(curCmd.Tick == prevNoteOnCmd.Tick || curCmd.Tick == nextNoteOnCmd.Tick)  
                    cmdList.RemoveAtIdxLoop(ref i, ref noteCmdChLen);
                else 
                    cmdList[i] = new FurnaceCommand(curCmd.Tick, prevNoteOnCmd); // FurnaceCommand(int tick, FurnaceCommand otherCmd): Copy otherCmd except tick
            }
        }
        #endregion
    }

    public void ReorderCommands()  
    {
        var noteCmds = NoteCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList = noteCmds[chNum];
            var noteCmdChLen  = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            noteCmdChList.Sort();
        }
    }
}