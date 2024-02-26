using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Furnace2MML.Etc;
using Furnace2MML.Utils;
using static Furnace2MML.Etc.InstOperator;
using static Furnace2MML.Etc.PublicValue;
namespace Furnace2MML.Parsing;

public class TxtOutputParsingMethods(StreamReader sr)
{
    public readonly StreamReader CmdStreamReader = sr;
    
    public bool CheckIsSystemValid()
    {
        var system = PublicValue.SongInfo.System;
        // var opnaSystemName = new[] { "Yamaha YM2608 (OPNA)", "NEC PC-98 (with PC-9801-86)" };
        var opnaSystemNames = new[] { "YM2608", "OPNA", "PC-98", "PC-9801-86" };
        return opnaSystemNames.Any(availableSysName => system.Contains(availableSysName));
    }
    
    public SongInformation ParseSongInfoSection(ref int curReadingLineNum)
    {
        ConvertProgress.Progress[(int)ConvertStage.PARSE_TEXT_SONG_INFO] = true;
        
        var songInfo = new SongInformation();
        
        while(!CmdStreamReader.EndOfStream) {
            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;
            
            var splitLine = SplitLine(line);
            var curType   = GetType(splitLine);
            var curValue  = GetValue(splitLine);

            switch(curType) {
                case "name":   songInfo.SongName = curValue; break;
                case "author": songInfo.Author   = curValue; break;
                case "album":  songInfo.Album    = curValue; break;
                case "system": songInfo.System   = curValue; break;
                case "tuning": songInfo.Tuning   = int.Parse(curValue); break;
                
                case "instruments": songInfo.InstrumentCount = byte.Parse(curValue); break;
                case "wavetables":  songInfo.WavetableCount  = byte.Parse(curValue); break;
                case "samples":     songInfo.SampleCount     = byte.Parse(curValue); break;
            }
            
            if(curType.Equals("samples"))
                return songInfo;
        }
        
        throw new InvalidOperationException("Should not be reached.");
    }

    public string ParseSongComment(ref int curReadingLineNum)
    {
        var songCommentFirstEmptyLine = false;
        var sb = new StringBuilder();
        while(!CmdStreamReader.EndOfStream) {
            if(CmdStreamReader.Peek() == '#')
                return sb.ToString();

            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(!songCommentFirstEmptyLine && line.Length == 0) {
                songCommentFirstEmptyLine = true;
                continue;
            }
            
            sb.Append("#Memo\t\t").AppendLine(line);
        }
        
        throw new InvalidOperationException("Should not be reached.");
    }

    public List<InstrumentDefinition> ParseInstrumentDefinition(ref int curReadingLineNum)
    {
        ConvertProgress.Progress[(int)ConvertStage.PARSE_TEXT_INST] = true;
        
        var instList = new List<InstrumentDefinition>();
        var instAmt  = PublicValue.SongInfo.InstrumentCount;
        
        var instDef               = new InstrumentDefinition();
        var instNumNameParsed     = false;
        var curOperatorNum        = -1;
        var parsingOperatorsCount = -1;
        
        while(!CmdStreamReader.EndOfStream) {
            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;
            
            if(!instNumNameParsed && line.Count(ch => ch == '#') == 2) {
                var splitStr = line.Split(": ");
                instDef.InstNum   = byte.Parse(splitStr[0][3..], NumberStyles.HexNumber);
                instDef.InstName  = splitStr[1];
                instNumNameParsed = true;
                continue;
            }
            
            var splitLine = SplitLine(line);
            var curType   = GetType(splitLine);
            var curValue  = GetValue(splitLine);
            
            if(parsingOperatorsCount == -1) {
                switch(curType) {
                    case "ALG":  instDef.Alg  = byte.Parse(curValue); break;
                    case "FB":   instDef.Fb   = byte.Parse(curValue); break;
                    case "FMS":  instDef.Fms  = byte.Parse(curValue); break;
                    case "AMS":  instDef.Ams  = byte.Parse(curValue); break;
                    case "FMS2": instDef.Fms2 = byte.Parse(curValue); break;
                    case "AMS2": instDef.Ams2 = byte.Parse(curValue); break;
                    case "operators": 
                        var opCount = GetByteValue(line);
                        instDef.OperatorCount = opCount;
                        instDef.Operators     = new byte[4, (int)TL_VC+1];
                        break;  
                    case "tom/top freq": parsingOperatorsCount++; break;
                }
            } else {
                if(curType.Contains("operator ")) {
                    curOperatorNum = GetOperatorNum(byte.Parse(curType[^1..]));
                    parsingOperatorsCount++;
                    continue;
                }
                
                if(curType.Equals("enabled")) {
                    instDef.Operators[curOperatorNum, (int)EN] = GetValue(line).Equals("yes") ? (byte)1 : (byte)0;
                    continue;
                }
                
                var opIdx = curType switch {
                    "AM"     => (int)AM,
                    "AR"     => (int)AR,
                    "DR"     => (int)DR,
                    "MULT"   => (int)MULT,
                    "RR"     => (int)RR,
                    "SL"     => (int)SL,
                    "TL"     => (int)TL,
                    "DT2"    => (int)DT2,  
                    "RS"     => (int)RS, // RS == KS
                    "DT"     => (int)DT,
                    "D2R"    => (int)D2R, // D2R == SR
                    "SSG-EG" => (int)SSG_EG,
                    "DAM"    => (int)DAM,
                    "DVB"    => (int)DVB,
                    "EGT"    => (int)EGT,
                    "KSL"    => (int)KSL,
                    "SUS"    => (int)SUS,
                    "VIB"    => (int)VIB,
                    "WS"     => (int)WS,
                    "KSR"    => (int)KSR,
                        
                    "TL volume scale" => (int)TL_VC,
                    _                 => throw new ArgumentOutOfRangeException($"Invalid Operator: {curType}")
                };
                
                instDef.Operators[curOperatorNum, opIdx] = opIdx == (int)DT ? GetCorrectDt(byte.Parse(curValue)) : byte.Parse(curValue);
                
                if(parsingOperatorsCount == instDef.OperatorCount && curType.Equals("TL volume scale")) { 
                    instList.Add(instDef);
                    InstantiateNewInstDef();
                }
            }
            
            if(instList.Count == instAmt)
                return instList;
        }
        
        throw new InvalidOperationException("Should not be reached.");

        #region Local Functions
        void InstantiateNewInstDef()
        {
            instDef               = new InstrumentDefinition();
            instNumNameParsed     = false;
            curOperatorNum        = -1;
            parsingOperatorsCount = -1;
        }

        byte GetOperatorNum(byte opNumFromTxtOutput) // Currently(as of Jan 22, 2024), Furnace saves the instrument operators in the order of 0 2 1 3, not 0 1 2 3.
        {
            return opNumFromTxtOutput switch {
                0 => 0,
                2 => 1,
                1 => 2,
                3 => 3,
                _ => throw new ArgumentOutOfRangeException($"Invalid Operator Number: {opNumFromTxtOutput}")
            };
        }

        byte GetCorrectDt(byte dtFromTxtOutput)  // Currently(as of Feb 12, 2024), the value of DT is not outputted correctly.
        {
            return dtFromTxtOutput switch {
                0 => 7,
                1 => 6,
                2 => 5,
                3 => 0,
                4 => 1,
                5 => 2,
                6 => 3,
                7 => 4,
                _ => throw new ArgumentOutOfRangeException($"The value of DT is out of range: {dtFromTxtOutput}")
            };
        } 
        #endregion
    }

    public SubsongData ParseSubsongs(ref int curReadingLineNum)
    {
        ConvertProgress.Progress[(int)ConvertStage.PARSE_TEXT_SUBSONG] = true;    
        
        var subsong = new SubsongData();
        
        while(!CmdStreamReader.EndOfStream) {
            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;
            
            var splitLine = SplitLine(line);
            var curType   = GetType(splitLine);
            var curValue  = GetValue(splitLine);

            switch(curType) {
                case "tick rate": subsong.TickRate = float.Parse(curValue); break; // Backspaces are removed by regex replace
                case "speeds":    subsong.Speed    = byte.Parse(curValue); break;
                case "virtual tempo":  
                    var virTempo = curValue.Split('/');
                    subsong.VirtualTempo[0] = int.Parse(virTempo[0]);
                    subsong.VirtualTempo[1] = int.Parse(virTempo[1]);
                    break;
                case "time base":      subsong.TimeBase   = int.Parse(curValue); break;
                case "pattern length": subsong.PatternLen = int.Parse(curValue); break;
            }
            
            if(curType.Equals("pattern length"))
                return subsong;
        }
        
        throw new InvalidOperationException("Should not be reached.");
    }

    public int ParseMaxOrderNum(ref int curReadingLineNum)
    {
        var isCurrentlyOrderSection = false;
        var backtickDivideLineCount = 0;
        var prevLine                = "";
        
        while(!CmdStreamReader.EndOfStream) {
            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;

            if(!isCurrentlyOrderSection) {
                if(line.Contains("orders"))
                    isCurrentlyOrderSection = true;
                continue;
            }
            
            if(line.Equals("```"))
                backtickDivideLineCount++;
            
            if(backtickDivideLineCount == 2)
                return int.Parse(prevLine.Split('|')[0].Trim());

            prevLine = line;
        }
        
        throw new InvalidOperationException("Should not be reached.");
    }

    /// <summary>
    /// OtherEffect, OrderStartTick, MaxOrderNum에 대한 파싱 작업
    /// </summary>
    /// <param name="curReadingLineNum"></param>
    public void ParsePatterns(ref int curReadingLineNum)
    {
        var effList                = OtherEffects;
        var patternLen             = PublicValue.Subsong.PatternLen;
        var curTickPerRow          = PublicValue.Subsong.Speed;
        var curOrderNum            = byte.MaxValue;
        var skippedTicks = 0;
        var totalSkippedTicks      = 0;
        var orderStartTickAssigned = false;

        while(!CmdStreamReader.EndOfStream) {
            var line = CmdStreamReader.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;

            if(line.Contains("ORDER")) {
                orderStartTickAssigned = false;
                curOrderNum            = byte.Parse(line[^2..], NumberStyles.HexNumber);
                continue;
            }

            var splitLine        = line.Split('|');
            var curPatternRowNum = int.Parse(splitLine[0].Trim(), NumberStyles.HexNumber);
            var curTick          = (curOrderNum*patternLen + curPatternRowNum) * curTickPerRow - totalSkippedTicks; // curOrderNum * patternLen * 3 + curPatternRowNum * 3 - skippedRows * tickPerRow

            if(!orderStartTickAssigned) {
                OrderStartTicks.Add(new OrderStartTick(curOrderNum, curTick, skippedTicks, totalSkippedTicks));
                skippedTicks = 0;
                orderStartTickAssigned = true;
            }

            for(byte chNum = 0; chNum < 16; chNum++) {
                var chStr      = splitLine[chNum+1];
                var splitChStr = chStr.Split(' '); // [0]: Note, [1]: InstCh, [2]: VolCh, [3~]: Effect

                for(var idx = 3; idx < splitChStr.Length; idx++) {
                    var effTypeStr = splitChStr[idx][..2];
                    var effValStr  = splitChStr[idx][2..];
                    if(effTypeStr.Equals(".."))
                        continue;

                    var effType = byte.Parse(effTypeStr, NumberStyles.HexNumber);
                    var effVal  = effValStr.Equals("..") ? (byte) 0 : byte.Parse(effValStr, NumberStyles.HexNumber);

                    if(!OtherEffectTypes.Contains(effType))
                        continue;

                    var effStruct = new OtherEffect(curTick, chNum, effType, effVal);
                    effList.Add(effStruct);

                    switch(effType) {
                        case 0x0D: // Jump to next pattern
                            skippedTicks = (patternLen-1 - curPatternRowNum) * curTickPerRow;
                            totalSkippedTicks += skippedTicks;
                            break;
                        
                        case 0x0B: // Jump to pattern
                            if(effStruct.Value <= curOrderNum)
                                EndTick = effStruct.Tick + curTickPerRow;
                            break;
                        
                        case 0xFF: // Stop song
                            EndTick = effStruct.Tick + curTickPerRow;
                            break;
                        
                        case 0x0F: // Set speed
                            curTickPerRow = effVal;
                            break;
                    }
                }
            }
        }
        
        MaxOrderNum = curOrderNum;
        SetTickPerUnits();
        if(EndTick == -1)
            EndTick = OrderStartTicks[MaxOrderNum].StartTick + TickPerUnitChanges[^1].TickPerOrder;
            
    }
    
    private static string[] SplitLine(string line) => line.Split(": "); 
    
    private static string GetType(string line) => GetType(SplitLine(line));
    private static string GetType(IReadOnlyList<string> splitStr) => splitStr[0].TrimExceptWords();
    // private static string GetType(IReadOnlyList<string> splitStr) => splitStr[0].LeaveAlphabetOnly();
    
    private static string GetValue(string line) => SplitLine(line)[1];
    private static string GetValue(IReadOnlyList<string> splitStr) => splitStr.Count != 1 ? splitStr[1] : "";
    private static int GetIntValue(string line) => int.Parse(GetValue(line));
    private static byte GetByteValue(string line) => byte.Parse(GetValue(line));
    
    private void SetTickPerUnits()
    {
        var effList    = OtherEffects;
        var initSpeed  = PublicValue.Subsong.Speed;
        var patternLen = PublicValue.Subsong.PatternLen;
        
        TickPerUnitChanges.Add(new TickPerUnitChange(0, initSpeed, initSpeed * patternLen));
        
        var effListLen = effList.Count;
        for(var i = 0; i < effListLen; i++) {
            var eff = effList[i];
            if(eff.EffType != 0x0F) // 0x0F: Set speed
                continue; 
            
            TickPerUnitChanges.Add(new TickPerUnitChange(eff.Tick, eff.Value, patternLen * eff.Value));
        }
    }
    
}