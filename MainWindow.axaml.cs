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

    private bool isCmdBinSelectedWithExplorer    = false;
    private bool isTxtOutputSelectedWithExplorer = false;
    
    public StreamReader Sr;
    public BinaryReader Br;
    
    private static readonly FilePickerFileType TextFileTypeFilter = new("Furnace Text Export Files") { Patterns = ["*.txt"] };
    private static readonly FilePickerFileType BinaryFileTypeFilter = new("Furnace Binary Command Stream Files") { Patterns = ["*.bin"] };

    public MainWindow()
    {
        InitializeComponent();
        this.Loaded  += OnWindowLoad;
        // this.Closing += OnClosing;

        PrintLog.InitLogTextRefField(LogTextBox);
        
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
        var fileTypeFilter = outputFileType switch {
            OutputFileType.TXT_OUTPUT => TextFileTypeFilter,
            OutputFileType.CMD_STREAM => BinaryFileTypeFilter,
            _                         => throw new ArgumentOutOfRangeException(nameof(outputFileType), outputFileType, null)
        };
        
        var topLevel = GetTopLevel(this);
        var file     = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Select a File",
            AllowMultiple = false,
            FileTypeFilter = [fileTypeFilter],
        });

        if(file.Count == 0)
            return;
        
        var filePath = file[0].TryGetLocalPath();
        if(filePath == null)
            return;
        
        GetFilePathFromFile(filePath, outputFileType);  //Get the path of specified file
            
        switch(outputFileType) {
            case OutputFileType.CMD_STREAM: isCmdBinSelectedWithExplorer    = true; break;
            case OutputFileType.TXT_OUTPUT: isTxtOutputSelectedWithExplorer = true; break;
            default:                        throw new ArgumentOutOfRangeException(nameof(outputFileType), outputFileType, null);
        }
    }

    public void GetFilePathFromFile(string filePath, OutputFileType outputFileType)
    {
        switch(outputFileType) {
            case OutputFileType.CMD_STREAM:
                CmdFilePathTextBox.Text = filePath;
                CmdFilePath             = filePath;
                
                if(!Util.GetFileExtensionFromPath(filePath).Equals("bin"))
                    ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_VALID_FCS);
                break;
            case OutputFileType.TXT_OUTPUT:
                TxtOutFilePathTextBox.Text = filePath;
                TxtOutFilePath             = filePath;
                
                if(!Util.GetFileExtensionFromPath(filePath).Equals("txt"))
                    ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_VALID_TXT);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid output file type: {outputFileType}");
        }
    }
    
    public void GetFilePathFromTextBox(OutputFileType outputFileType)
    {
        switch(outputFileType) {
            case OutputFileType.CMD_STREAM:
                CmdFilePath = CmdFilePathTextBox.Text ?? "???";
                break;
            case OutputFileType.TXT_OUTPUT:
                TxtOutFilePath = TxtOutFilePathTextBox.Text ?? "???";
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid output file type: {outputFileType}");
        }
    }

    private void StartConvert()
    {
        ClearPreviousData();
        
        if(!isCmdBinSelectedWithExplorer) 
            GetFilePathFromTextBox(OutputFileType.CMD_STREAM);
        if(!isTxtOutputSelectedWithExplorer)
            GetFilePathFromTextBox(OutputFileType.TXT_OUTPUT);
        
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

        // Read First 4 bytes: "FCS\0"
        var first4Byte = Br.ReadBytes(4); 
        if(first4Byte.Length != 4) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!(first4Byte[0] == 'F' && first4Byte[1] == 'C' && first4Byte[2] == 'S' && first4Byte[3] == '\0')) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_CMD_STREAM);
            return false;
        }

        // Parse channel count
        var chCountBin = Br.ReadBytes(4);
        var chCount    = Util.GetIntFrom4Bytes(chCountBin);
        
       // Initialize Data Arrays. These arrays are in the PublicValues.cs
        ChDataStartAddr = new int[chCount];
        PresetDelays    = new byte[16];
        SpeedDialCmds  = new byte[16];
        ChData          = new List<byte>[chCount];

        // Parse pointers to channel data
        for(var ch=0; ch<chCount; ch++) {
            var chStartAddrBin = Br.ReadBytes(4);
            ChDataStartAddr[ch] = Util.GetIntFrom4Bytes(chStartAddrBin);
        }
        
        // Parse delay presets
        for(var ch=0; ch<chCount; ch++) {
            var binByte = Br.ReadByte();
            PresetDelays[ch] = binByte;
        }
        
        // Parse speed dial commands
        for(var ch=0; ch<chCount; ch++) {
            var binByte = Br.ReadByte();
            SpeedDialCmds[ch] = binByte;
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

            var curTick = Subsong.GetVirtTempoInDecimal() == 1 ? 0 : -1;  // tick value starts with 1 if the Virtual Tempo in Decimal is 0.5. should be subtracted by 1.
            // var curVol  = 0xFF;
            // var curDelay = -1;

            var curChDataLen = curChData.Count;
            for(var i=0; i<curChDataLen;) {
                var curByteVal = curChData[i];
                
                var cmdType = CmdType.INVALID;
                var valueCount = 0;
                
                switch(curByteVal) {
                    case <= 0xCA: {  // 0x00 ~ 0xCA
                        cmdType = BinCmdStreamParsingMethods.GetCmdType(curByteVal); 
                        break;
                    }
                    case >= 0xD0 and <= 0xDF: {  // Speed Dial Commands
                        var spdDialCmdIdx = curByteVal & 0x0F;
                        var spdDialCmdVal = SpeedDialCmds[spdDialCmdIdx];

                        cmdType = spdDialCmdVal switch {
                            0x9D => CmdType.HINT_ARP_TIME,
                        
                            _ => throw new ArgumentOutOfRangeException($"Unknown CmdType (on SpdDialCmds): {cmdType}")
                        };
                        break;
                    }
                    case >= 0xE0 and <= 0xEF: {  // Note Preset Delays
                        var presetDelayIdx = curByteVal & 0x0F;
                        var delay          = PresetDelays[presetDelayIdx];
                        curTick += delay;

                        i += 1;
                        continue;
                    }
                    case 0xFC: {  // wait (16-bit)   FC 12 34 --> FC: wait (16-bit), 12: firstByte, 34: secondByte
                        var firstByte  = curChData[i + 1];
                        var secondByte = curChData[i + 2];
                        var waitValue  = secondByte << 8 | firstByte;  // Little-Endian
                        curTick += waitValue;
                        
                        i += 3;  // i += 1+2;  ((1 command + 2 values) read)
                        continue;
                    }
                    case 0xFD: {  // wait (8-bit)
                        var waitValue  = curChData[i + 1];
                        curTick += waitValue;
                        
                        i += 2;  // i += 1+1;  ((1 command + 1 values) read)
                        continue;
                    }
                    case 0xFE: {  // wait (one tick)
                        curTick += 1;
                        
                        i += 1;
                        continue;
                    }
                    case 0xFF:
                        i += 1;
                        continue;
                }

                valueCount = BinCmdStreamParsingMethods.GetValueCount(cmdType);

                var orderNum = MiscellaneousConversionUtil.GetOrderNum(curTick);
                var value1   = valueCount >= 1 ? curChData[i+1] : curByteVal;  // value1 == curByteVal when valueCount is 0
                var value2   = valueCount >= 2 ? curChData[i+2] : -1;
                
                if(cmdType.EqualsAny(CmdType.NOTE_ON, CmdType.HINT_PORTA, CmdType.HINT_LEGATO) && ch is >= 0 and <= 5)
                    value1 += 12; // Increases octave of FM channels by 1

                var cmdListRef = ch switch {
                    >= 0 and <= 8  => NoteCmds[ch],
                    >= 9 and <= 14 => DrumCmds,
                    _              => null
                };

                var cmdStruct = new FurnaceCommand(curTick, ch, cmdType, value1, value2);

                switch(ch) {
                    case >= 0 and <= 8:  NoteCmds[ch].Add(cmdStruct); break; // 0~5: FM 1~6, 6~8: SSG 1~3
                    case >= 9 and <= 14: DrumCmds.Add(cmdStruct); break;     // Drum
                    // default: ADPCM << Not in use
                }
                
                i += 1+valueCount;
            }
        }
        
        
        for(byte chNum = 0; chNum < NoteCmds.Length; chNum++) //  각 채널의 첫 명령의 Tick이 0이 아니면 MML에 쉼표(r)를 넣기 위해 Tick이 0인 명령 삽입
            if(NoteCmds[chNum].Count != 0 && NoteCmds[chNum][0].Tick != 0)
                NoteCmds[chNum].Insert(0, new FurnaceCommand(0, 0, chNum, "NOTE_OFF", 0, 0));
        if(DrumCmds.Count != 0 && DrumCmds[0].Tick != 0)
            DrumCmds.Insert(0, new FurnaceCommand(0, 0, 16, "NOTE_ON", 0, 0));  // Channel Number outside 9~14 in DrumCmds are regarded as Rest
        
        var furnaceCmdStructTweaker = new FurnaceCmdStructTweaker();
        furnaceCmdStructTweaker.InsertNoteOffAtStartOfEachOrder();
        furnaceCmdStructTweaker.RemoveUnnecessaryPortamentoBinaryCommands();
        furnaceCmdStructTweaker.RemoveUnnecessaryLegatoCommands();
        furnaceCmdStructTweaker.RemoveUnnecessaryArpeggioCommands();
        furnaceCmdStructTweaker.ReorderCommands();
        furnaceCmdStructTweaker.InsertCmdForZeroLenCmd();
        return true;
    }

    private bool Convert()
    {
        var debugOutput = DebugOutputCheckbox is { IsChecked: true };
        
        ResultOutputTextBox.Clear();
        var resultOutput = new StringBuilder();

        var songName = MetaTitleTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : SongInfo.SongName;
        var composer = MetaComposerTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : SongInfo.Author;
        var arranger = MetaArrangerTextbox.Text;
        var tempo    = int.TryParse(MetaTempoTextbox.Text, out _) ? MetaTempoTextbox.Text : CmdStreamToMMLUtil.ConvertTickrateToTempo(Subsong.TickRate).ToString();
        var option   = MetaOptionTextbox.Text.Length != 0 ? MetaOptionTextbox.Text : "/v/c";
        var filename = MetaFilenameTextbox.Text.Length != 0 ? MetaFilenameTextbox.Text : ".M2";
        var zenlen   = MetaZenlenTextbox.Text.Length != 0 ? MetaZenlenTextbox.Text : PublicValue.Zenlen.ToString();
        var voldown  = GetVoldownMeta();

        /* Metadata */
        var metaSb = new StringBuilder();
        metaSb.AppendLine(";;; Converted with Furnace2MML").AppendLine()
              .AppendLine(debugOutput ? "; Metadata" : "")
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
        resultOutput.Append(debugOutput ? ";;; Instrument Definition\n" : "");
        var instSb = ConvertFurnaceToMML.ConvertInstrument(new StringBuilder(), debugOutput);
        resultOutput.Append(instSb);
        PublicValue.InstDefOutput = instSb;


        /* Initialize Order StringBuilder */
        resultOutput.Append(debugOutput ? ";;; Note Channels\n" : "");
        var orderSbArrLen = MaxOrderNum + 1;
        var orderSbArr = new StringBuilder[orderSbArrLen];
        for(var orderNum = 0; orderNum < orderSbArrLen; orderNum++) {
            orderSbArr[orderNum] = new StringBuilder();
            orderSbArr[orderNum].Append(debugOutput ? $"; [{orderNum:X2}|{orderNum:D3}] ({CmdStreamToMMLUtil.GetOrderStartTick(orderNum)}~{CmdStreamToMMLUtil.GetOrderStartTick(orderNum+1)-1} Tick)\n" : "");
        }

        LoopStartOrder = TxtOutputToMMLUtil.GetLoopStartOrder(OtherEffects);
        LoopStartTick = PublicValue.OrderStartTimes[LoopStartOrder].StartTick;
        if(LoopStartOrder != -1)
            ConvertFurnaceToMML.AppendLoop(orderSbArr[LoopStartOrder]);

        /* Convert FM/SSG, Drum */
        ConvertFurnaceToMML.ConvertNotesToMML(orderSbArr);
        ConvertFurnaceToMML.ConvertDrumsToMML(orderSbArr);
        
        /* Output results to ResultOutputTextBox */
        foreach(var orderSb in orderSbArr)
            resultOutput.Append(orderSb);

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
            sb.Append(fmVoldown.Length != 0 ? $"F{fmVoldown}" : "F20")
               .Append(ssgVoldown.Length != 0 ? $"S{ssgVoldown}" : "S-10")
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
