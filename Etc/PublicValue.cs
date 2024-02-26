using System.Collections.Generic;
using System.Text;

namespace Furnace2MML.Etc;

public static class PublicValue
{
    public static SongInformation SongInfo;
    public static List<InstrumentDefinition> InstDefs = [];
    // public List<Subsong> Subsongs;
    public static SubsongData Subsong;
    public static string Memo = "";
    
    public static List<FurnaceCommand>[] NoteCmds = []; // 배열의 각 원소는 각 채널의 명령어들
    public static List<FurnaceCommand> DrumCmds = [];
    public static List<OtherEffect> OtherEffects = [];
	
    public static List<TickPerUnitChange> TickPerUnitChanges = [];
	public static List<OrderStartTick> OrderStartTicks = [];
	public static int EndTick = -1;
	public static int MaxOrderNum = -1;
    
	public static readonly int[] ValidFractionLength = [1, 2, 4, 8, 16, 32, 3, 6, 12, 24, 48, 96];
	// public static readonly long[] ValidFractionLength = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 96 };
    public static readonly byte[] OtherEffectTypes = [0x09, 0x0F, 0xF0,  0x0B, 0x0D, 0xFF];
    public static int CurDefaultNoteFractionLength = 4;

    public static StringBuilder MetadataOutput = null!;
    public static StringBuilder InstDefOutput = null!;
    public static StringBuilder[] NoteChannelsOutput = null!;
}														   
															
															