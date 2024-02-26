using System;
using Avalonia.Controls;

namespace Furnace2MML.Etc;

public static class PrintLog
{
    public static TextBox LogTextBox = null!;

    public static void LogErr(string msg, int newLineCount = 2)
        => Log(msg, "Error", newLineCount);
    public static void LogWarn(string msg, int newLineCount = 2)
        => Log(msg, "Warning", newLineCount);
    public static void LogInfo(string msg, int newLineCount = 2)
        => Log(msg, "Info", newLineCount);
    public static void LogDebug(string msg, int newLineCount = 2)
        => Log(msg, "Debug", newLineCount);
    public static void LogTr(string msg, int newLineCount = 2)
        => Log(msg, "Trace", newLineCount);

    private static void Log(string msg, string logType, int newLineCount)
        => LogTextBox.Text += $"[{GetCurrentTime()}|{logType}] {msg}{new string('\n', newLineCount)}";

    private static string GetCurrentTime()
    {
        var now = DateTime.Now;
        return $"{now.Hour:00}:{now.Minute:00}:{now.Second:00}";
    }
}