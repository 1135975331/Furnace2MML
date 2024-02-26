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
    public static StringBuilder ConvertInstrument(StringBuilder instSb)
    {
        var instDefsLen = InstDefs.Count;
        for(var instNum = 0; instNum < instDefsLen; instNum++) {
            var curInstDef = InstDefs[instNum];

            if(curInstDef.OperatorCount != 4)
                PrintLog.LogWarn($"Operator Count of instrument {instNum:X2} is {curInstDef.OperatorCount}, not 4. Some operators may not be converted.");
            
            instSb.AppendLine($"@{instNum:000} {curInstDef.Alg} {curInstDef.Fb}")
               .AppendLine($"; AR DR SR RR SL  TL KS ML DT AMS\t{curInstDef.InstName}");
            
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
                
                instSb.AppendLine($"  {ar:00} {dr:00} {sr:00} {rr:00} {sl:00} {tl:000} {ks:00} {ml:00} {dt:00} {ams:00}");
            }
        }
        
        instSb.AppendLine().AppendLine();
        
        return instSb;
    }
    
    public static void ConvertNotesToMML(StringBuilder[] orderSb)
    {
        var noteCmdsLen = NoteCmds.Length;
        for(var chNum = 0; chNum < noteCmdsLen; chNum++) {
            var noteCmdCh = NoteCmds[chNum];
            var mmlCh     = CmdStreamToMMLUtil.ConvertChannel(chNum);

            if(noteCmdCh.Count == 0)
                continue;
        
            var firstNoteOnCmd = CmdStreamToMMLUtil.GetFirstNoteOn(noteCmdCh);
            if(firstNoteOnCmd.CmdType.Equals("NO_NOTE_ON")) // if there's no NOTE_ON on the channel
                continue;
            
            var prevOctave = firstNoteOnCmd.Value1 / 12;
            
            for(var orderNum = 0; orderNum <= MaxOrderNum; orderNum++)
                orderSb[orderNum].Append($"{mmlCh}\t");
            if(prevOctave != 0)
                orderSb[0].Append('o').Append(prevOctave).Append(' ');  
            // if(DefaultNoteFractionLength != -1)
            // orderSb[0].Append('l').Append(DefaultNoteFractionLength).Append("   ");

            var prevNoteCmdOrderNum = -1;

            var noteCmdChLen = noteCmdCh.Count;
            for(var i = 0; i < noteCmdChLen; i++) {
                var noteCmd     = noteCmdCh[i];
                var curOrderNum = noteCmd.OrderNum;
                var tickLen     = CmdStreamToMMLUtil.GetCmdTickLength(noteCmdCh, i);
                var cmdType     = noteCmd.CmdType;

                if(curOrderNum != prevNoteCmdOrderNum) {
                    prevNoteCmdOrderNum          = curOrderNum;
                    CurDefaultNoteFractionLength = CmdStreamToMMLUtil.GetDefaultNoteFracLenForThisOrder(noteCmdCh, curOrderNum, i);
                    if(CurDefaultNoteFractionLength != -1)
                        orderSb[curOrderNum].Append('l').Append(CurDefaultNoteFractionLength).Append("   ");
                }

                // - => ignored
                switch(cmdType) {
                    case "HINT_ARP_TIME": ConvertCmdStreamToMML.SetArpSpeed(noteCmd); break;
                    case "HINT_ARPEGGIO": ConvertCmdStreamToMML.SetArpeggioStatus(noteCmd); break;
                    case "INSTRUMENT":    ConvertCmdStreamToMML.ConvertInstrument(noteCmd, tickLen, orderSb[curOrderNum]); break; 
                    case "PANNING":       ConvertCmdStreamToMML.ConvertPanning(noteCmd, tickLen, orderSb[curOrderNum]); break;
                    case "HINT_VOLUME":   ConvertCmdStreamToMML.ConvertVolume(noteCmd, tickLen, orderSb[curOrderNum]); break;
                    case "NOTE_ON":       ConvertCmdStreamToMML.ConvertNoteOn(noteCmd, tickLen, ref prevOctave, orderSb[curOrderNum]); break;
                    case "NOTE_OFF":      ConvertCmdStreamToMML.ConvertNoteOff(tickLen, orderSb[curOrderNum]); break;
                    case "HINT_PORTA":    ConvertCmdStreamToMML.ConvertPortamento(noteCmdCh, i, ref prevOctave, orderSb[curOrderNum]); break;
                    case "HINT_LEGATO":   ConvertCmdStreamToMML.ConvertLegato(noteCmd, tickLen, ref prevOctave, orderSb[curOrderNum]); break;
                }

            }

            for(var i = 0; i <= MaxOrderNum; i++)
                orderSb[i].AppendLine();
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
        
        var curInstNum             = new int[8];        // [channel number, instrument number], instrument number is used to distinguish SSG Drums
        var drumsChAtIdenticalTick = new List<int[]>(); // List of curInstNum
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
                case "HINT_VOLUME":   ConvertCmdStreamToMML.ConvertVolume(drumCmd, tickLen, orderSb[curOrderNum]); break;
                case "INSTRUMENT":
                    curInstNum[drumCmd.Channel-9] = drumCmd.Value1;
                    break;
                case "NOTE_ON":
                    var drum = drumCmd.Channel is >= 9 and <= 14 ? new[] {drumCmd.Channel, curInstNum[drumCmd.Channel-9]} : [16, curInstNum[0]];
                    drumsChAtIdenticalTick.Add(drum);
                    break;
                // case "NOTE_OFF": Convert2MML.ConvertNoteOff(tickLen, orderSb[curOrderNum]); break;
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
     * [Drum]  00 - YM2608 Drum Sound Source (without internal SSG drums)
     *  Kick   01 - @1 Bass Drum
     *  Snare  01 - @2 Snare Drum 1
     *  Snare  02 - @64 Snare Drum 2
     *  Top    01 - @256 Hi-Hat Open
     *  Top    02 - @512 Crash Cymbal
     *  Top    03 - @1024 Ride Cymbal
     *  HiHat  01 - @128 Hi-Hat Close
     *  Tom    01 - @4 Low Tom
     *  Tom    02 - @8 Middle Tom
     *  Tom    03 - @16 High Tom
     *  Rim    01 - @32 Rim Shot
     */
    private static string _prevMMLDrum = "";
    private static int _prevOrderNum = -1;
    private static bool _firstDrumProcessed = false;
    public static void ConvertDrumNoteOn(List<int[]> chNums, int curOrderNum, int tickLen, StringBuilder sb)
    {
        var soundSourceOnly = chNums.Where(elem => elem[1] == 0);
        var withInternalSSG = chNums.Where(elem => elem[1] >= 1);
        
        var soundSourceDrumStr = DrumConversion.ToDrumSoundSource(soundSourceOnly);
        var internalSSGDrumStr    = DrumConversion.ToInternalSSGDrum(withInternalSSG);
        var mmlFracLen = CmdStreamToMMLUtil.FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        // var mmlFracLen = CommandToMMLUtil.ConvertBetweenTickAndFraction(tickLen);

        var isRest = internalSSGDrumStr.Equals("r");
        if(curOrderNum != _prevOrderNum) {
            _prevOrderNum       = curOrderNum;
            _firstDrumProcessed = false;
        }

        if(!isRest && _firstDrumProcessed && internalSSGDrumStr.Equals(_prevMMLDrum))
            internalSSGDrumStr = ""; // In order to reduce file size
        else if(!isRest) {
            _prevMMLDrum        = internalSSGDrumStr;
            _firstDrumProcessed = true;
        }
        
        sb.Append(soundSourceDrumStr).Append(isRest ? $"r" : $"{internalSSGDrumStr}c").Append(mmlFracLen);
    }
}