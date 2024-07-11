#define DEBUG
// #undef DEBUG
// #define RELEASE
#undef RELEASE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Furnace2MML.Conversion;
using Furnace2MML.Etc;
using Furnace2MML.Parsing;
using Furnace2MML.Utils;
using static Furnace2MML.Etc.ErrorWhileConversion;
using static Furnace2MML.Etc.ErrorWhileConversionMethods;
using static Furnace2MML.Etc.PrintLog;
using static Furnace2MML.Etc.PublicValue;
using Util = Furnace2MML.Utils.Util;


// todo Progressbar
// todo Document

namespace Furnace2MML;

public partial class MainWindow : Window
{
    public string CmdFilePath;
    public string TxtOutFilePath;
    public StreamReader Sr;
    public BinaryReader Br;
    
    private static readonly FilePickerFileType TextFileTypeFilter = new("Text Files") { Patterns = ["*.txt"] };

    public MainWindow()
    {
        InitializeComponent();
        this.Loaded  += OnWindowLoad;
        // this.Closing += OnClosing;

        PrintLog.LogTextBox = LogTextBox;
        
		#if DEBUG
        var testFilesDirPath = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName + @"/FurnaceForTest";
        LogDebug($"TestFilesDir: {testFilesDirPath}");
        GetFilePaths($@"{testFilesDirPath}/etc/binaryStreamAnalysis3.bin", OutputFileType.CMD_STREAM);
        GetFilePaths($@"{testFilesDirPath}/etc/binaryStreamAnalysis3.txt", OutputFileType.TXT_OUTPUT);
        StartConvert();
		#endif
    }
    
    private void OnWindowLoad(object sender, RoutedEventArgs e)
    {
        Application.Current.Name = "Furnace2MML Converter";
    }

    private void CmdFileSelectButton_Click(object sender, RoutedEventArgs e)
        => FileSelect(OutputFileType.CMD_STREAM);

    private void TxtOutFileSelectButton_Click(object sender, RoutedEventArgs e)
        => FileSelect(OutputFileType.TXT_OUTPUT);

    private async void FileSelect(OutputFileType outputFileType)
    {
        var topLevel = GetTopLevel(this);
        var file     = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Select a File",
            AllowMultiple = false,
            FileTypeFilter = [TextFileTypeFilter],
        });

        if(file.Count == 0)
            return;
        
        var filePath = file[0].TryGetLocalPath();
        if(filePath != null)
            GetFilePaths(filePath, outputFileType);  //Get the path of specified file
    }

    public void GetFilePaths(string filePath, OutputFileType outputFileType)
    {
        switch(outputFileType) {
            case OutputFileType.CMD_STREAM:
                CmdFilePath             = filePath;
                CmdFilePathTextBox.Text = filePath;
                
                if(!Util.GetFileExtensionFromPath(filePath).Equals("bin"))
                    ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_VALID_FCS);
                break;
            case OutputFileType.TXT_OUTPUT:
                TxtOutFilePath             = filePath;
                TxtOutFilePathTextBox.Text = filePath;
                
                if(!Util.GetFileExtensionFromPath(filePath).Equals("txt"))
                    ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_VALID_TXT);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid output file type: {outputFileType}");
        }

    }

    private void StartConvert()
    {
        ClearPreviousData();
        
        var isTxtOutputParseSuccessful = ParseTextOutput(TxtOutFilePath); Sr.Close();
        if(!isTxtOutputParseSuccessful)  // return if the parse method returns false(unsuccessful).
            return;
        
        var isCmdStreamParseSuccessful = ParseBinCommandStream(CmdFilePath); Br.Close();
        if(!isCmdStreamParseSuccessful)
            return;
        /*
        var isCmdStreamParseSuccessful = ParseTextCommandStream(CmdFilePath); Sr.Close();
        if(!isCmdStreamParseSuccessful)
            return;
            */

        Convert();
        CountCharSize();
    }

    private static void ClearPreviousData()
    {
        InstDefs.Clear();
        foreach(var noteCmdList in NoteCmds)
            noteCmdList.Clear();
        DrumCmds.Clear();
        OtherEffects.Clear();
        TickPerUnitChanges.Clear();
        OrderStartTimes.Clear();
        EndTick = -1;
        MaxOrderNum = -1;

        MetadataOutput = null!;
        InstDefOutput = null!;
        NoteChannelsOutput = null!;
        
        ConvertFurnaceToMML.ResetAllFields();
        ConvertCmdStreamToMML.ResetAllFields();
    }

    private bool ParseTextOutput(string textOutputFilePath)
    {
        ConvertProgress.Progress[(int) ConvertStage.PARSE_TEXT_INIT] = true;
        try { Sr = new StreamReader(textOutputFilePath); } catch(ArgumentException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(PathTooLongException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_PATH_TOO_LONG, e);
            return false;
        } catch(DirectoryNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(FileNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(Exception e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(UNKNOWN_ERROR, e);
            return false;
        }

        var curReadingLineNum = 0;

        var firstLine = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
        if(firstLine == null) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!firstLine.Equals("# Furnace Text Export")) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_TEXT_EXPORT);
            return false;
        }

#if RELEASE
        try {
#endif
            // # Song Information
            var txtOutputParser = new TxtOutputParsingMethods(Sr);

            while(!Sr.EndOfStream) {
                var line = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
                if(string.IsNullOrEmpty(line))
                    continue;

                // Parse Section (# Song Information, # Sound Chips, # Instruments, etc.)
                var curSection = line.Count(ch => ch == '#') is 1 or 2 ? line.Trim('#').Trim() : "";

                switch(curSection) {
                    case "Song Information":
                        SongInfo = txtOutputParser.ParseSongInfoSection(ref curReadingLineNum);
                        break;
                    case "Song Comments":
                        Memo = txtOutputParser.ParseSongComment(ref curReadingLineNum);
                        break;
                    case "Instruments":
                        InstDefs = txtOutputParser.ParseInstrumentDefinition(ref curReadingLineNum);
                        break;
                    case "Wavetables": break;
                    case "Samples":    break;
                    case "Subsongs":
                        Subsong = txtOutputParser.ParseSubsongs(ref curReadingLineNum);
                        break;
                    case "Patterns":
                        txtOutputParser.ParsePatterns(ref curReadingLineNum);
                        break;
                }
            }

            if(!txtOutputParser.CheckIsSystemValid()) {
                ResultOutputTextBox.Text = GetErrorMessage(SYSTEM_NOT_OPNA);
                return false;
            }

            switch(Subsong.VirtualTempo[0] / (float)Subsong.VirtualTempo[1]) {
                case 1: case 0.5f:  
                    break; 
                default:
                    ResultOutputTextBox.Text = GetErrorMessage(INVALID_VIRT_TEMPO);
                    return false;
            }
#if RELEASE
        } catch(Exception e) {
            var errMsg = $"An error has occured while parsing the command stream at line {curReadingLineNum}.\n\nStackTrace: {e.StackTrace}\n\nMsg: {e.Message}\n\n\n\n";
            LogTextBox.Text = errMsg;
        }
#endif
        return true;
    }

    private bool ParseBinCommandStream(string binCmdFilePath)
    {
        if(!File.Exists(binCmdFilePath)) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_FOUND);
            return false;
        }

        using var binCmdFileStream = File.Open(binCmdFilePath, FileMode.Open);
        
        try {
            Br = new BinaryReader(binCmdFileStream); 
        } catch(ArgumentException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(PathTooLongException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_PATH_TOO_LONG, e);
            return false;
        } catch(DirectoryNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(FileNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(Exception e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(UNKNOWN_ERROR, e);
            return false;
        }
        
        NoteCmds = new List<FurnaceCommand>[9];
        for(var chNum = 0; chNum < NoteCmds.Length; chNum++)
            NoteCmds[chNum] = [];
        DrumCmds = [];

        var brBaseStream  = Br.BaseStream;

        // Parsing First 4 bytes: "FCS\0"
        var first4Byte = Br.ReadBytes(4); 
        if(first4Byte.Length != 4) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!(first4Byte[0] == 'F' && first4Byte[1] == 'C' && first4Byte[2] == 'S' && first4Byte[3] == '\0')) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_CMD_STREAM);
            return false;
        }

        // Parsing channel count
        var chCountBin = Br.ReadBytes(4);
        var chCount    = Util.GetIntFrom4Bytes(chCountBin);
        
       // Initialize Data Arrays, they are in the PublicValues.cs
        ChDataStartAddr = new int[chCount];
        PresetDelays    = new byte[chCount];
        SpeedDialCmads  = new byte[chCount];
        ChData          = new List<byte>[chCount];

        // Parsing pointers to channel data
        for(var ch=0; ch<chCount; ch++) {
            var chStartAddrBin = Br.ReadBytes(4);
            ChDataStartAddr[ch] = Util.GetIntFrom4Bytes(chStartAddrBin);
        }
        
        // Parsing delay presets
        for(var ch=0; ch<chCount; ch++) {
            var binByte = Br.ReadByte();
            PresetDelays[ch] = binByte;
        }
        
        // Parsing speed dial commands
        for(var ch=0; ch<chCount; ch++) {
            var binByte = Br.ReadByte();
            SpeedDialCmads[ch] = binByte;
        }
        
        for(var ch=0; ch<chCount; ch++) {
            ChData[ch] = [];
            var curChDataEndAddr = ch + 1 < chCount ? ChDataStartAddr[ch + 1] - 1 : brBaseStream.Length-1;
            var curReadingAddr   = brBaseStream.Position;;
            
            while(curReadingAddr < curChDataEndAddr) {
                curReadingAddr   = brBaseStream.Position;
                var binByte      = Br.ReadByte();
                ChData[ch].Add(binByte);
            }
        }

        
        // Convert Binary Data to FurnaceCommand Struct
        for(byte ch=0; ch<chCount; ch++) {
            var curChData    = ChData[ch];

            var curTick = 0;
            var curVol  = 0xFF;
            // var curDelay = -1;

            for(var i=0; i<curChData.Count;) {
                var curByteVal = curChData[i];
                var valueCount = 0;
                
                if(curByteVal is >= 0xE0 and <= 0xEF) { // Note Preset Delays
                    var presetDelayIdx = curByteVal & 0x0F;
                    var delay          = PresetDelays[presetDelayIdx];
                    curTick += delay;
                } else if(curByteVal <= 0xCA) {       // 0x00 ~ 0xCA
                    var cmdType = curByteVal switch { // 다른 파일에 유틸리티 함수로 만들 것
                        <= 0xB4 => CmdType.NOTE_ON,   // 0x00 <= curByteVal <= 0xB4 (byte: 0x00 ~ 0xFF)
                        0xB5    => CmdType.NOTE_OFF,
                        0xB6    => CmdType.NOTE_OFF_ENV, // Note off env
                        0xB7    => CmdType.ENV_RELEASE,  // env release
                        0xB8    => CmdType.INSTRUMENT,
                        0xBE    => CmdType.PANNING,

                        0xC0 => CmdType.PRE_PORTA,
                        0xC2 => CmdType.VIBRATO,
                        0xC3 => CmdType.VIB_RANGE,
                        0xC4 => CmdType.VIB_SHAPE,
                        0xC5 => CmdType.PITCH,
                        0xC6 => CmdType.HINT_ARPEGGIO,
                        0xC7 => CmdType.HINT_VOLUME,
                        0xC8 => CmdType.VOL_SLIDE,
                        0xC9 => CmdType.HINT_PORTA,
                        0xCA => CmdType.HINT_LEGATO,
                        // ...
                        _ => CmdType.INVALID
                    };

                    valueCount = cmdType switch {
                        CmdType.NOTE_ON      or 
                        CmdType.NOTE_OFF     or
                        CmdType.NOTE_OFF_ENV or
                        CmdType.ENV_RELEASE => 0,
                            
                        CmdType.INSTRUMENT  or
                        CmdType.VIB_RANGE   or
                        CmdType.VIB_SHAPE   or
                        CmdType.PITCH       or
                        CmdType.HINT_VOLUME or
                        CmdType.HINT_LEGATO => 1,
                            
                        CmdType.PANNING       or
                        CmdType.PRE_PORTA     or
                        CmdType.VIBRATO       or
                        CmdType.HINT_ARPEGGIO or
                        CmdType.VOL_SLIDE     or
                        CmdType.HINT_PORTA => 2,
                            
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    var orderNum = MiscellaneousConversionUtil.GetOrderNum(curTick);
                    var value1   = valueCount >= 1 ? curChData[i+1] : curByteVal;  // value1 = curByteVal when valueCount is 0
                    var value2   = valueCount >= 2 ? curChData[i+2] : -1;
                    // var value1   = valueCount >= 1 ? BinCmdStreamParsingMethods.GetValue1(curChData[i+1], cmdType) : curByteVal;
                    // var value2   = valueCount >= 2 ? BinCmdStreamParsingMethods.GetValue2(curChData[i+2], cmdType) : -1;

                    // if(cmdType.EqualsAny(CmdType.NOTE_ON, CmdType.HINT_PORTA, CmdType.HINT_LEGATO) && ch is >= 0 and <= 5)
                    // value1 += 12; // Increases octave of FM channels by 1

                    var cmdStruct = new FurnaceCommand(curTick, orderNum, ch, cmdType, value1, value2);

                    switch(ch) {
                        case >= 0 and <= 8: // 0~5: FM 1~6, 6~8: SSG 1~3
                            NoteCmds[ch].Add(cmdStruct);
                            break;
                        case >= 9 and <= 14: // Drum
                            DrumCmds.Add(cmdStruct);
                            break;
                        // default: ADPCM << Not in use
                    }
                }

                i += 1+valueCount;
            }
        }
        
        var furnaceCmdStructTweaker = new FurnaceCmdStructTweaker();
        furnaceCmdStructTweaker.InsertNoteOffAtStartOfEachOrder();
        furnaceCmdStructTweaker.RemoveUnnecessaryPortamentoCommands();
        furnaceCmdStructTweaker.RemoveUnnecessaryLegatoCommands();
        furnaceCmdStructTweaker.FixRetriggerCommands();
        furnaceCmdStructTweaker.ReorderCommands();
        
        return true;
    }
    
    [Obsolete("Text Command Stream has been removed since Furnace v0.6.2")]
    #region ParseTextCommandStream_Legacy
    private bool ParseTextCommandStream(string txtCmdFilePath)
    {
        try { Sr = new StreamReader(txtCmdFilePath); } catch(ArgumentException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(PathTooLongException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_PATH_TOO_LONG, e);
            return false;
        } catch(DirectoryNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(FileNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(Exception e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(UNKNOWN_ERROR, e);
            return false;
        }

        var curReadingLineNum = 0;

        var firstLine = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
        if(firstLine == null) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!firstLine.Equals("# Furnace Command Stream")) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_CMD_STREAM);
            return false;
        }

#if RELEASE
        try {
#endif
        NoteCmds = new List<FurnaceCommand>[9];
        for(var chNum = 0; chNum < NoteCmds.Length; chNum++)
            NoteCmds[chNum] = [];
        DrumCmds = [];

        // 본격적인 Stream 파싱 시작
        var furnaceCmdStructTweaker        = new FurnaceCmdStructTweaker();
        var curTick                = -1;
        var isCurrentSectionStream = false;
        while(!Sr.EndOfStream) {
            var line = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;

            if(!isCurrentSectionStream) {
                if(line.Equals("[Stream]"))
                    isCurrentSectionStream = true;
                continue;
            }

            if(line.Contains(">> TICK "))
                curTick = int.Parse(line[8..]);
            else if(line.Contains(">> END") || line.Contains(">> LOOP"))
                furnaceCmdStructTweaker.RemoveDuplicatedCommandsAtTheEnd(ref curTick, line);
            else {
                var split    = line.Trim().Split(" ");
                var orderNum = MiscellaneousConversionUtil.GetOrderNum(curTick);
                var channel  = byte.Parse(split[0].Remove(split[0].Length - 1));
                var cmdType  = split[1];
                var value1   = int.Parse(split[2]);
                var value2   = int.Parse(split[3]);

                if(cmdType.EqualsAny("NOTE_ON", "HINT_PORTA", "HINT_LEGATO") && channel is >= 0 and <= 5)
                    value1 += 12; // Increases octave of FM channels by 1

                var cmdStruct = new FurnaceCommand(curTick, orderNum, channel, cmdType, value1, value2);

                switch(channel) {
                    case >= 0 and <= 5: // FM 1~6
                        NoteCmds[channel].Add(cmdStruct);
                        break;
                    case >= 6 and <= 8: // SSG 1~3
                        NoteCmds[channel].Add(cmdStruct);
                        break;
                    case >= 9 and <= 14: // Drum
                        DrumCmds.Add(cmdStruct);
                        break;
                    // default: ADPCM << 사용안함
                }
            }
        }

        for(byte chNum = 0; chNum < NoteCmds.Length; chNum++) //  각 채널의 첫 명령의 Tick이 0이 아니면 MML에 쉼표(r)를 넣기 위해 Tick이 0인 명령 삽입
            if(NoteCmds[chNum].Count != 0 && NoteCmds[chNum][0].Tick != 0)
                NoteCmds[chNum].Insert(0, new FurnaceCommand(0, 0, chNum, "NOTE_OFF", 0, 0));
        if(DrumCmds.Count != 0 && DrumCmds[0].Tick != 0)
            DrumCmds.Insert(0, new FurnaceCommand(0, 0, 16, "NOTE_ON", 0, 0));  // Channel Number outside 9~14 in DrumCmds are regarded as Rest

        furnaceCmdStructTweaker.InsertNoteOffAtStartOfEachOrder();
        furnaceCmdStructTweaker.RemoveUnnecessaryPortamentoCommands();
        furnaceCmdStructTweaker.RemoveUnnecessaryLegatoCommands();
        furnaceCmdStructTweaker.FixRetriggerCommands();
        furnaceCmdStructTweaker.ReorderCommands();
#if RELEASE
        } catch(Exception e) {
            var errMsg = $"An error has occured while parsing the command stream at line {curReadingLineNum}.\n\nStackTrace: {e.StackTrace}\n\nMsg: {e.Message}\n\n\n\n";
            LogTextBox.Text = errMsg;
        }
#endif
        return true;
    }
    #endregion 

    private bool Convert()
    {
        ResultOutputTextBox.Clear();
        var resultOutput = new StringBuilder();

        var songName = MetaTitleTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : SongInfo.SongName;
        var composer = MetaComposerTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : SongInfo.Author;
        var arranger = MetaArrangerTextbox.Text;
        var tempo    = int.TryParse(MetaTempoTextbox.Text, out _) ? MetaTempoTextbox.Text : CmdStreamToMMLUtil.ConvertTickrateToTempo(Subsong.TickRate).ToString();
        var option   = MetaOptionTextbox.Text.Length != 0 ? MetaOptionTextbox.Text : "/v/c";
        var filename = MetaFilenameTextbox.Text.Length != 0 ? MetaFilenameTextbox.Text : ".M2";
        var zenlen   = MetaZenlenTextbox.Text.Length != 0 ? MetaZenlenTextbox.Text : "192";
        var voldown  = GetVoldownMeta();

        /* Metadata */
        var metaSb = new StringBuilder();
        metaSb.AppendLine(";;; Converted with Furnace2MML").AppendLine()
              .AppendLine("; Metadata")
              .AppendLine($"#Title\t\t{songName}")
              .AppendLine($"#Composer\t{composer}")
              .AppendLine($"#Arranger\t{arranger}")
              .AppendLine($"#Tempo\t\t{tempo}")
              .AppendLine($"#Option\t\t{option}")
              .AppendLine($"#Filename\t{filename}")
              .AppendLine($"#Zenlen\t\t{zenlen}")
              .AppendLine($"#Volumedown\t{voldown}")
              .AppendLine($"{Memo}")
               // .AppendLine("#Memo\t\tConverted with FurnaceCommandStream2MML")
              .AppendLine()
              .AppendLine();
        resultOutput.Append(metaSb);
        PublicValue.MetadataOutput = metaSb;
        
        /* Instrument Definition */
        resultOutput.AppendLine(";;; Instrument Definition");
        var instSb = ConvertFurnaceToMML.ConvertInstrument(new StringBuilder());
        resultOutput.Append(instSb);
        PublicValue.InstDefOutput = instSb;


        /* Initialize Order StringBuilder */
        resultOutput.AppendLine(";;; Note Channels");
        var orderSbArrLen = MaxOrderNum + 1;
        var orderSbArr = new StringBuilder[orderSbArrLen];
        for(var orderNum = 0; orderNum < orderSbArrLen; orderNum++) {
            orderSbArr[orderNum] = new StringBuilder();
            orderSbArr[orderNum].AppendLine($"; [{orderNum:X2}|{orderNum:D3}] ({CmdStreamToMMLUtil.GetOrderStartTick(orderNum)}~{CmdStreamToMMLUtil.GetOrderStartTick(orderNum+1)-1} Tick)");
        }

        var loopPointOrder = TxtOutputToMMLUtil.GetLoopPoint(OtherEffects);
        if(loopPointOrder != -1)
            AppendLoop(orderSb[loopPointOrder]);

        /* Convert FM/SSG, Drum */
        ConvertFurnaceToMML.ConvertNotesToMML(orderSbArr);
        ConvertFurnaceToMML.ConvertDrumsToMML(orderSbArr);
        
        /* Output results to ResultOutputTextBox */
        foreach (var orderSb in orderSbArr)
            resultOutput.Append(orderSb).AppendLine();

        PublicValue.NoteChannelsOutput = orderSbArr;
        ResultOutputTextBox.Text = resultOutput.ToString();
        return true;

        #region Local Functions
        /* ---------------------------------------- Local Functions ------------------------------------------- */
        string GetVoldownMeta()
        {
            var fmVoldown  = MetaVoldownFMTextbox.Text;
            var ssgVoldown = MetaVoldownSSGTextbox.Text;
            var rhyVoldown = MetaVoldownRhythmTextbox.Text;

            var sb = new StringBuilder();
            sb.Append(fmVoldown.Length != 0 ? $"F{fmVoldown}" : "F15")
               .Append(ssgVoldown.Length != 0 ? $"S{ssgVoldown}" : "")
               .Append(rhyVoldown.Length != 0 ? $"R{rhyVoldown}" : "");

            return sb.ToString();
        }

        #endregion
    }

    private void CountCharSize()
    {
        var outputText = ResultOutputTextBox.Text;
        CharCountLabel.Content = $"Character Count: {outputText.Length:N0}";

        var sizeKb       = Encoding.Default.GetByteCount(outputText) / 1000f;
        var limitPercent = sizeKb / 61;
        SizeLabel.Content = $"Size/SizeLimit:\n    {sizeKb:N2}/61 KB ({limitPercent:P1})";
    }

    private void ConvertStartButton_Click(object sender, RoutedEventArgs e)
        => StartConvert();

    private void ClipboardCopyButton_Click(object sender, RoutedEventArgs e)
    {
        var textToCopy = new StringBuilder();
        
        if(MetadataCopyCheckbox is { IsChecked: true })  
            textToCopy.Append(PublicValue.MetadataOutput);
        if(InstDefCopyCheckbox is { IsChecked: true }) 
            textToCopy.Append(PublicValue.InstDefOutput);
        if(NoteChCopyCheckbox is { IsChecked: true })
            foreach(var channelOutput in PublicValue.NoteChannelsOutput)
                textToCopy.Append(channelOutput);
        
        Clipboard.SetTextAsync(textToCopy.ToString());
        LogInfo("Output is copied to clipboard.");
    }
    
}
