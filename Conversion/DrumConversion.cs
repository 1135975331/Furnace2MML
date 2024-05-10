using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Furnace2MML.Conversion;

public static class DrumConversion
{
	public static string ToDrumSoundSource(IEnumerable<int[]> drumChNums)
	{
		var sb = new StringBuilder();
		foreach(var drumChNum in drumChNums) {
			var mml = drumChNum[0] switch {
				9  => "\\b",  // bass drum
				10 => "\\s",  // snare drum
				11 => "\\c",  // cymbals
				12 => "\\h",  // hi-hat
				13 => "\\t",  // tom 
				14 => "\\i",  // rim shot
				_  => ""
			};
			sb.Append(mml);
		}
		
		return sb.ToString();
	}
    
    
    /*
     *  Ch   ins  
     * Kick  01 - @1 Bass Drum
     * Snare 01 - @2 Snare Drum 1
     * Snare 02 - @64 Snare Drum 2
     * Top   01 - @256 Hi-Hat Open
     * Top   02 - @512 Crash Cymbal
     * Top   03 - @1024 Ride Cymbal
     * HiHat 01 - @128 Hi-Hat Close
     * Tom   01 - @4 Low Tom
     * Tom   02 - @8 Middle Tom
     * Tom   03 - @16 High Tom
     * Rim   01 - @32 Rim Shot
     */
	public static string ToInternalSSGDrum(IEnumerable<int[]> drumChNums)
	{
		var mmlDrumPlayID = drumChNums.Select(drumCh => drumCh[0] switch {
				9  when drumCh[1] == 1 => 1,

				10 when drumCh[1] == 1 => 2,
				10 when drumCh[1] == 2 => 64,

				11 when drumCh[1] == 1 => 256,
				11 when drumCh[1] == 2 => 512,
				11 when drumCh[1] == 3 => 1024,

				12 when drumCh[1] == 1 => 128,

				13 when drumCh[1] == 1 => 4,
				13 when drumCh[1] == 2 => 8,
				13 when drumCh[1] == 3 => 16,

				14 when drumCh[1] == 1 => 32,

				_ => 0
			})
		   .Where(mmlDrumInstID => mmlDrumInstID != 0)
		   .Sum();

		return mmlDrumPlayID == 0 ? "r" : $"@{mmlDrumPlayID}";
	}
	
	public static bool GetIsAlreadyAddedDrumInst(int mmlDrumInstID, bool[] isAlreadyAddedDrumInst)
		=> isAlreadyAddedDrumInst[(int)Math.Log(mmlDrumInstID, 2)];
	
	
	public static void SetIsAlreadyAddedDrumInst(int mmlDrumInstID, bool[] isAlreadyAddedDrumInst, bool boolValue)
		=> isAlreadyAddedDrumInst[(int)Math.Log(mmlDrumInstID, 2)] = boolValue;
}