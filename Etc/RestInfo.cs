namespace FurnaceCommandStream2MML.Etc;

public struct RestInfo
{
	public int IndexToAppend { get; set; }
	public long Length { get; set; }

	public RestInfo(int indexToAppend, long length)
	{
		IndexToAppend = indexToAppend;
		Length = length;
	}
	
	public override string ToString() => $"IndexToAppend: {IndexToAppend}, Length: {Length}";
}