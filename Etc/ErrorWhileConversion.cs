using System;
using static Furnace2MML.Etc.ErrorWhileConversion;

namespace Furnace2MML.Etc;


public enum ErrorWhileConversion
{
	/* 파일을 선택 중 발생한 에러 */
	NO_FILE_SELECTED,   // 경로를 지정하지 않은 경우
	FILE_NOT_FOUND,     // 지정된 경로에 파일이 없을 경우, 찾을 수 없는 경우
	FILE_NOT_VALID,     // 파일 형식이 지원되지 않는 경우, MIDI 파일이 아닌 경우
	FILE_PATH_TOO_LONG, // 파일 경로가 너무 긴 경우
	FILE_EMPTY,         // 빈 파일인 경우 (첫번째 출이 null인 경우)
        
	NOT_FURNACE_TEXT_EXPORT,  // Furnace Text Output이 아닌 경우
	SYSTEM_NOT_OPNA, // System이 NEC PC-98이 아닌 경우
	INVALID_VIRT_TEMPO, // Virtual Tempo가 서로 같지 않은 경우
	NOT_FURNACE_CMD_STREAM,  // Furnace Command Stream이 아닌 경우 (Furnace Command Stream 텍스트 파일들은 첫 출에 # Furnace Command Stream이 쓰여있음)
	
	CONVERTER_NOT_SELECTED,
	NO_INSTRUMENT,
	
	/*위에 해당하지 않는 에러*/
	UNKNOWN_ERROR
}

public static class ErrorWhileConversionMethods
{

	public static string GetErrorMessage(ErrorWhileConversion error, int lineBreakAmount = 0)
	{
		var errMsg = GetErrorMessageInternal(error);
		errMsg += new string('\n', lineBreakAmount);
        
		return errMsg;
	}

	public static string GetExceptionErrorMessage(ErrorWhileConversion error, Exception e, int lineBreakAmount = 0)
	{
		var errMsg = GetErrorMessageInternal(error);
		errMsg += $"\n\nStackTrace: {e.StackTrace}\nMessage: {e.Message}\n";
		errMsg += new string('\n', lineBreakAmount);
        
		return errMsg;
	}


	/// <summary>
	/// 
	/// </summary>
	/// <param name="error"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	private static string GetErrorMessageInternal(ErrorWhileConversion error)
	{
		var errMsg = error switch {
			NO_FILE_SELECTED   => "Error: No MIDI file is selected.",
			FILE_NOT_FOUND     => "Error: File is not found in that path.",
			FILE_NOT_VALID     => "Error: File is not valid. Please select MIDI(*.mid) file.",
			FILE_PATH_TOO_LONG => "Error: File path or name is too long. (paths must be less than 248 characters, and file names must be less than 260 characters)",
			FILE_EMPTY         => "Error: File is empty",
                
			NOT_FURNACE_TEXT_EXPORT => "Error: The file selected is not Furnace Text Output.",
			SYSTEM_NOT_OPNA      => "Error: System is not YM2608(OPNA).",
			INVALID_VIRT_TEMPO      => "Error: Virtual Tempo is not equal.",
			NOT_FURNACE_CMD_STREAM  => "Error: The file selected is not Furnace Command Stream. (Please check if the first line of the file is '# Furnace Command Stream')",
			
			CONVERTER_NOT_SELECTED => "Error: Converter is not selected.",
			NO_INSTRUMENT => "Error: No instrument selected.",
			
			UNKNOWN_ERROR => "Error: Unknown error.",
			
			_ => throw new ArgumentOutOfRangeException($"Falied to get error message, invalid error type: {error}")
		};
		
		return errMsg;
	}
	
}