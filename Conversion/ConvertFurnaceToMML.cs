using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Furnace2MML.Etc;
using Furnace2MML.Utils;
using static Furnace2MML.Etc.InstOperator;
using static Furnace2MML.Etc.PublicValue;

namespace Furnace2MML.Conversion;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class ConvertFurnaceToMML
{
    public static StringBuilder ConvertInstrument(StringBuilder instSb, bool debugOutput = true)
    {
        var instDefsLen = InstDefs.Count;
        for(var instNum = 0; instNum < instDefsLen; instNum++) {
            var curInstDef = InstDefs[instNum];

            if(curInstDef.OperatorCount != 4)
                PrintLog.LogWarn($"Operator Count of the instrument 0x{instNum:X2} is {curInstDef.OperatorCount}, not 4. Some operators will not be converted.");
            
            instSb.AppendLine($"@{instNum:000} {curInstDef.Alg} {curInstDef.Fb}")
               .Append(debugOutput ? $"; AR DR SR RR SL  TL KS ML DT AMS\t{curInstDef.InstName}\n" : "");
            
            var ops     = curInstDef.Operators;
            var ams     = curInstDef.Ams;
            for(var opNum = 0; opNum < 4; opNum++) {
                var ar = ops[opNum, (int)AR];
                var dr = ops[opNum, (int)DR];
                var sr = ops[opNum, (int)D2R]; // SR == D2R
                var rr = ops[opNum, (int)RR];
                var sl = ops[opNum, (int)SL];
                var tl = ops[opNum, (int)TL];
                var ks = ops[opNum, (int)RS]; // KS == RS
                var ml = ops[opNum, (int)MULT];
                var dt = ops[opNum, (int)DT];

                if(debugOutput)
                    instSb.AppendLine($"  {ar:00} {dr:00} {sr:00} {rr:00} {sl:00} {tl:000} {ks:00} {ml:00} {dt:00} {ams:00}");
                else
                    instSb.AppendLine($"  {ar} {dr} {sr} {rr} {sl} {tl} {ks} {ml} {dt} {ams}");
            }
        }
        
        instSb.AppendLine().AppendLine();
        
        return instSb;
    }
    
    public static void ConvertNotesToMML(StringBuilder[] orderSbArr)
    {
        var noteCmdsLen = NoteCmds.Length;
        for(var chNum = 0; chNum < noteCmdsLen; chNum++) {
            var noteCmdCh = NoteCmds[chNum];
            var mmlCh     = CmdStreamToMMLUtil.ConvertChannel(chNum);

            if(noteCmdCh.Count == 0)
                continue;
        
            var firstNoteOnCmd = CmdStreamToMMLUtil.GetFirstNoteOn(noteCmdCh);
            if(firstNoteOnCmd.CmdType == CmdType.NO_NOTE_ON) // if there's no NOTE_ON on the channel
                continue;
            
            var prevOctave = firstNoteOnCmd.Value1 / 12 - 5;  // note on command on Binary Command Stream can have C-(-5) ~ B-9. C(-5) ~ B(-1) should be excluded.
            
            for(var orderNum = 0; orderNum <= MaxOrderNum; orderNum++)
                orderSbArr[orderNum].Append($"{mmlCh}\t");
            if(prevOctave != 0)
                orderSbArr[0].Append('o').Append(prevOctave).Append(' ');  
            // if(DefaultNoteFractionLength != -1)
            // orderSb[0].Append('l').Append(DefaultNoteFractionLength).Append("   ");

            var prevNoteCmdOrderNum = -1;

            var noteCmdChLen = noteCmdCh.Count;
            for(var i = 0; i < noteCmdChLen; i++) {
                var noteCmd     = noteCmdCh[i];
                var curOrderNum = noteCmd.OrderNum;
                var tickLen     = CmdStreamToMMLUtil.GetCmdTickLength(noteCmdCh, i);
                var cmdType     = noteCmd.CmdType;

                var curOrderSb = orderSbArr[curOrderNum];

                if(curOrderNum != prevNoteCmdOrderNum) {
                    prevNoteCmdOrderNum          = curOrderNum;
                    CurDefaultNoteFractionLength = CmdStreamToMMLUtil.GetDefaultNoteFracLenForThisOrder(noteCmdCh, curOrderNum, i);
                    if(CurDefaultNoteFractionLength != -1)
                        curOrderSb.Append('l').Append(CurDefaultNoteFractionLength).Append("   ");
                }

                // - => ignored
                switch(cmdType) {
                    case CmdType.HINT_ARP_TIME: ConvertCmdStreamToMML.SetArpSpeed(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.HINT_ARPEGGIO: ConvertCmdStreamToMML.SetArpStatus(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.VIB_SHAPE:     ConvertCmdStreamToMML.ConvertVibShape(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.VIB_RANGE:     ConvertCmdStreamToMML.ConvertVibRange(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.VIBRATO:       ConvertCmdStreamToMML.ConvertVibrato(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.INSTRUMENT:    ConvertCmdStreamToMML.ConvertInstrument(noteCmd, tickLen, curOrderSb); break; 
                    case CmdType.PANNING:       ConvertCmdStreamToMML.ConvertPanning(noteCmd, tickLen, curOrderSb); break;
                    case CmdType.HINT_VOLUME:   ConvertCmdStreamToMML.ConvertVolume(noteCmdCh, i, tickLen, curOrderSb); break;
                    case CmdType.NOTE_ON:       ConvertCmdStreamToMML.ConvertNoteOn(noteCmd, tickLen, ref prevOctave, curOrderSb); break;
                    case CmdType.NOTE_OFF:      ConvertCmdStreamToMML.ConvertNoteOff(tickLen, curOrderSb); break;
                    case CmdType.HINT_PORTA:    ConvertCmdStreamToMML.ConvertPortamento(noteCmdCh, i, ref prevOctave, curOrderSb); break;
                    case CmdType.HINT_LEGATO:   ConvertCmdStreamToMML.ConvertLegato(noteCmdCh, i, tickLen, ref prevOctave, curOrderSb); break;
                }
            }
            
            /* Set Channel Instrument at the time of the loop to end of the MML*/
            if(LoopStartOrder != 1) {
                var lastOrderSb = orderSbArr[MaxOrderNum];
                var chCmdList = NoteCmds[chNum];
                
                CmdStreamToMMLUtil.GetFirstCertainCmd(chCmdList, 0, cmd => cmd.Tick >= LoopStartTick, out _, out var foundCmdIdx);
                var instCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(chCmdList, foundCmdIdx, cmd => cmd.CmdType == CmdType.INSTRUMENT,  out _, out _, direction: "backward");
                lastOrderSb.Append($"@{instCmd.Value1}");
            }
            
            for(var i = 0; i <= MaxOrderNum; i++)
                orderSbArr[i].AppendLine();
        }
    }

    public static void ConvertDrumsToMML(StringBuilder[] orderSb)
    {
        if(DrumCmds.Count == 0)
            return;

        // orderSb[0].Append($"R{0} r32\n");
        for(var i=0; i <= MaxOrderNum; i++)
            orderSb[i].Append($"R{i}\t");

        var prevNoteCmdOrderNum = -1;
        
        var curInstNum             = new int[8];                                                     // [channel number, instrument number], instrument number is used to distinguish SSG Drums
        var drumsChAtIdenticalTick = new SortedSet<int[]>(GetDrumsChannelAtIdenticalTickComparer()); // Set of curInstNum
        var drumCmdsLen            = DrumCmds.Count;
        for(var i = 0; i < drumCmdsLen; i++) {
            var drumCmd     = DrumCmds[i];
            var curOrderNum = drumCmd.OrderNum;
            var tickLen     = CmdStreamToMMLUtil.GetCmdTickLength(DrumCmds, i);
            var cmdType     = drumCmd.CmdType;
            
            if(curOrderNum != prevNoteCmdOrderNum) {
                prevNoteCmdOrderNum          = curOrderNum;
                CurDefaultNoteFractionLength = CmdStreamToMMLUtil.GetDefaultNoteFracLenForThisOrder(DrumCmds, curOrderNum, i);
                if(CurDefaultNoteFractionLength != -1)
                    orderSb[curOrderNum].Append('l').Append(CurDefaultNoteFractionLength).Append("   ");
            }

            switch(cmdType) {
                // case CmdType.HINT_VOLUME: ConvertCmdStreamToMML.ConvertVolume(drumCmd, tickLen, orderSb[curOrderNum]); break;
                case CmdType.INSTRUMENT:  curInstNum[drumCmd.Channel-9] = drumCmd.Value1; break;
                case CmdType.NOTE_ON:
                    var drum = drumCmd.Channel is >= 9 and <= 14 ? new[] {drumCmd.Channel, curInstNum[drumCmd.Channel-9]} : [16, curInstNum[0]];
                    drumsChAtIdenticalTick.Add(drum);
                    break;
            }

            if(tickLen == 0)
                continue;
            
            ConvertDrumNoteOn(drumsChAtIdenticalTick, curOrderNum, tickLen, orderSb[curOrderNum]);
            drumsChAtIdenticalTick.Clear();
        }

        for(var i = 0; i <= MaxOrderNum; i++)
            orderSb[i].AppendLine().AppendLine($"K\tR{i}").AppendLine();
    }
    
    /*
     * [Drum][Ch] 00 - YM2608 Drum Sound Source (without internal SSG drums)
     *  Kick  09  01 - @1    Bass Drum
     *  Snare 10  01 - @2    Snare Drum 1
     *  Snare 10  02 - @64   Snare Drum 2
     *  Top   11  01 - @256  Hi-Hat Open
     *  Top   11  02 - @512  Crash Cymbal
     *  Top   11  03 - @1024 Ride Cymbal
     *  HiHat 12  01 - @128  Hi-Hat Close
     *  Tom   13  01 - @4    Low Tom
     *  Tom   13  02 - @8    Middle Tom
     *  Tom   13  03 - @16   High Tom
     *  Rim   14  01 - @32   Rim Shot
     */
    private static string _prevMMLDrum = "";
    private static int _prevOrderNum = -1;
    private static bool _firstDrumProcessed = false;
    public static void ConvertDrumNoteOn(SortedSet<int[]> chNums, int curOrderNum, int tickLen, StringBuilder sb)
    {
        var soundSourceOnly = chNums.Where(elem => elem[1] == 0);
        var withInternalSSG = chNums.Where(elem => elem[1] >= 1);
        
        var soundSourceDrumStr = DrumConversion.ToDrumSoundSource(soundSourceOnly);
        var internalSSGDrumStr = DrumConversion.ToInternalSSGDrum(withInternalSSG);
        var mmlFracLen = CmdStreamToMMLUtil.FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        // var mmlFracLen = CommandToMMLUtil.ConvertBetweenTickAndFraction(tickLen);

        var isRest = internalSSGDrumStr.Equals("r");
        if(curOrderNum != _prevOrderNum) {
            _prevOrderNum       = curOrderNum;
            _firstDrumProcessed = false;
        }

        if(!isRest) {
            if(_firstDrumProcessed && internalSSGDrumStr.Equals(_prevMMLDrum))
                internalSSGDrumStr = ""; // In order to reduce file size
            else {
                _prevMMLDrum        = internalSSGDrumStr;
                _firstDrumProcessed = true;
            }
        }
        
        sb.Append(soundSourceDrumStr).Append(isRest ? "r" : $"{internalSSGDrumStr}c").Append(mmlFracLen);
    }
    
    public static void AppendLoop(StringBuilder ordSb)
    {
        var noteCmdListLen = NoteCmds.Length;
        for(var chNum = 0; chNum < noteCmdListLen; chNum++) {
            var noteCmdCh = NoteCmds[chNum];
            if(noteCmdCh.Count == 0)
                continue;

            var firstNoteOnCmd = CmdStreamToMMLUtil.GetFirstNoteOn(noteCmdCh);
            if(firstNoteOnCmd.CmdType == CmdType.NO_NOTE_ON) // if there's no NOTE_ON on the channel
                continue;

            ordSb.Append(CmdStreamToMMLUtil.ConvertChannel(chNum));
        }

        if(DrumCmds.Count != 0)
            ordSb.Append('K');

        ordSb.Append(" L").AppendLine();
    }
    

    public static void ResetAllFields()
    {
        _prevMMLDrum = "";
        _prevOrderNum = -1;
        _firstDrumProcessed = false;
    }

    private static Comparer<int[]> GetDrumsChannelAtIdenticalTickComparer() 
        => Comparer<int[]>.Create((intArr1, intArr2) => {
            var channelComparison = intArr1[0].CompareTo(intArr2[0]);
            return channelComparison != 0 ? channelComparison : intArr1[1].CompareTo(intArr2[1]);
        });
}