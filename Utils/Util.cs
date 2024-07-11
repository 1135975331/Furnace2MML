using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Furnace2MML.Utils;

public static partial class Util
{
	public static string GetFileExtensionFromPath(string filePath)
	{ 
		var splitPathStr = filePath.Split('.');
		return splitPathStr[^1];
	}
    
	public static string? ReadLineCountingLineNum(this TextReader reader, ref int curNumberLine)
	{
		curNumberLine++;
		return reader.ReadLine();
	}
	
	public static int GetIntFrom4Bytes(byte[] bytesArr)
	{
		if(bytesArr.Length != 4)
			throw new ArgumentException($"4 bytes value (bytesArr.count == 4) is expected. ({bytesArr.Length})");
        
		// little endian
		return (bytesArr[3] << 2*3) + (bytesArr[2] << 2*2) + (bytesArr[1] << 2*1) + bytesArr[0];
	}

	public static string ToEscapedString(this string origin)
		=> origin.Replace(@"\", @"\\");
    
	/// <summary>
	/// Remove method for optimized and indexed for loop
	/// </summary>
	/// <param name="list"></param>
	/// <param name="elem"></param>
	/// <param name="listLen"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
    public static bool RemoveIdxLoop<T>(this List<T> list, ref int listLen, T elem)
    {
        listLen--;
        return list.Remove(elem);
    }

	public static void RemoveAtIdxLoop<T>(this List<T> list, ref int idx, ref int listLen)
	{
        list.RemoveAt(idx);
        listLen--;
        idx--;
	}
    
	public static void InsertAtIdxLoop<T>(this List<T> list, ref int idx, ref int listLen, T item)
	{
        list.Insert(idx+1, item);
        listLen++;
        idx++;
	}
	
	// Referenced code from: https://stackoverflow.com/questions/10293236/accessing-the-scrollviewer-of-a-listbox-from-c-sharp
	/*
	public static Visual GetDescendantByType(Visual element, Type type)
	{
		if (element == null)
			return null;
        
		if (element.GetType() == type)
			return element;
        
		Visual foundElement = null;
		if (element is FrameworkElement)
			(element as FrameworkElement).ApplyTemplate();
        
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) {
			var visual = VisualTreeHelper.GetChild(element, i) as Visual;
			foundElement = GetDescendantByType(visual, type);
			if (foundElement != null)
				break;
		}
        
		return foundElement;
	}
	*/

	public static bool EqualsAny<T>(this T source, params T[] otherStrings) 
		=> otherStrings.Any(elem => elem != null && elem.Equals(source));

	public static string TrimExceptWords(this string str)
		=> RegexExceptWords().Replace(str, "");
    public static string LeaveAlphabetOnly(this string str) 
	    => RegexExceptAlphabet().Replace(str, "");
    public static string LeaveNumberOnly(this string str) 
	    => RegexExceptNumber().Replace(str, "");
    
    [GeneratedRegex("[^a-zA-Z]")]
    private static partial Regex RegexExceptAlphabet();
    
    // [GeneratedRegex("^[0-9]*$")]
    [GeneratedRegex("[^0-9]")]
    private static partial Regex RegexExceptNumber();
    
    [GeneratedRegex(@"^[\s-]+|[\s0-9]+$|:(.*)")]
    private static partial Regex RegexExceptWords();
}