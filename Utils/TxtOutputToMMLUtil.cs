using System.Collections.Generic;
using Furnace2MML.Etc;

namespace Furnace2MML.Utils;

public static class TxtOutputToMMLUtil
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="effList">List of OtherEffects</param>
    /// <returns>Order number of Loop point. <br/> Return -1 if the song doesn't loop.</returns>
    public static int GetLoopPoint(List<OtherEffect> effList)
    {
        foreach(var eff in effList) {
            var effOrder = MiscellaneousConversionUtil.GetOrderNum(eff.Tick);
            
            switch(eff.EffType) {
                case 0x0B:  // Jump to Pattern
                    if(eff.Value <= effOrder)
                        return eff.Value;
                    break;
                
                case 0xFF:  // Stop Song
                    return -1;           
            }
        }
        return 0;
    }
}