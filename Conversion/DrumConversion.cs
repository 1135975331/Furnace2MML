using System;
using System.Collections.Generic;
using System.Linq;
namespace FurnaceCommandStream2MML.Conversion;

public static class DrumConversion
{
    /*
     *  Ch   ins  
     * Kick     - @1 Bass Drum
     * Snare 00 - @2 Snare Drum 1
     * Snare 01 - @64 Snare Drum 2
     * Top   00 - @256 Hi-Hat Open
     * Top   01 - @512 Crash Cymbal
     * Top   02 - @1024 Ride Cymbal
     * HiHat    - @128 Hi-Hat Close
     * Tom   00 - @4 Low Tom
     * Tom   01 - @8 Middle Tom
     * Tom   02 - @16 High Tom
     * Rim      - @32 Rim Shot
     */
	public static string MidiDrumToMMLDrum(List<int[]> drumChNums)
	{
		var mmlDrumPlayID = drumChNums.Select(drumCh => drumCh[0] switch {
				9 => 1,

				10 when drumCh[1] == 0 => 2,
				10 when drumCh[1] == 1 => 64,

				11 when drumCh[1] == 0 => 256,
				11 when drumCh[1] == 1 => 512,
				11 when drumCh[1] == 2 => 1024,

				12 => 128,

				13 when drumCh[1] == 0 => 4,
				13 when drumCh[1] == 1 => 8,
				13 when drumCh[1] == 2 => 16,

				14 => 32,

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