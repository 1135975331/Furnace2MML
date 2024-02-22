using System.Linq;
namespace FurnaceCommandStream2MML.Etc;

public static class CheckIsValidValue
{
	/// <summary>
	/// MML 채널 입력값이 제대로 입력되었는지 확인하는 메소드
	/// MML 채널은 A~I 사이의 문자가 입력된 문자열만 허용된다.
	/// 소문자로 입력시 대문자로 변환된다.
	/// 값이 올바르면 true를 반환하고 result 매개변수에 mmlChannelName을 대입하고
	/// 올바르지 않으면 false를 반환하고 result 매개변수에 null을 대입한다.
	/// </summary>
	/// <param name="mmlChannelName">MML 채널 입력값</param>
	/// <param name="result">값이 올바르면 mmlChannelName, 올바르지 않으면 null</param>
	/// <returns>값이 올바른가의 여부</returns>
	public static bool CheckIsMMLChannelValid(string mmlChannelName, out string result)
	{
		var names = mmlChannelName.ToUpper().ToCharArray();
		
		var isValid = names.Length != 0 && names.All(name => name is >= 'A' and <= 'I');
		
		result = isValid ? mmlChannelName.ToUpper() : null;
		return isValid;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="instrumentNumber"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public static bool CheckIsInstrumentNumberValid(string instrumentNumber, out int result)
	{
		var isValid = int.TryParse(instrumentNumber, out var instNum);
		
		if(isValid)
			isValid = instNum is >= -1 and <= 255;
		
		result = isValid ? instNum : -1;
		return isValid;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="mmlChannelVolume"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public static bool CheckIsMMLChannelVolValid(string mmlChannelVolume, out int result)
	{
		var isValid = int.TryParse(mmlChannelVolume, out var mmlChannelVol);
		
		if(isValid)
			isValid = mmlChannelVol is >= -1 and <= 15;
		
		result = isValid ? mmlChannelVol : -1;
		return isValid;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="defaultOctave"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public static bool CheckIsDefaultOctaveValid(string defaultOctave, out int result)
	{
		if(defaultOctave.Equals("")) {  //  빈 문자열이 들어온 경우, 유효하며 -1이 입력된 것으로 간주함
			result = -1;
			return true;
		}
        
		var isValid = int.TryParse(defaultOctave, out var defaultOct);

		if(isValid)
			isValid = defaultOct is -1 or >= 1 and <= 8;
		
		result = isValid ? defaultOct : -1;
		return isValid;
	}
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="defaultNoteLength"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public static bool CheckIsDefaultNoteLengthValid(string defaultNoteLength, out int result)
	{
		if(defaultNoteLength.Equals("")) {  //  빈 문자열이 들어온 경우, 유효하며 -1이 입력된 것으로 간주함
			result = -1;
			return true;
		}
        
		var isValid = int.TryParse(defaultNoteLength, out var defaultNoteLen);
		
		if(isValid)
			isValid = PublicValue.ValidFractionLength.Any(fracLen => fracLen == defaultNoteLen) || defaultNoteLen == -1;
		
		result = isValid ? defaultNoteLen : -1;
		return isValid;
	}
}