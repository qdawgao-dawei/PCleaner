namespace PCleaner.Models;

public class DiskInfo
{
    public string Name { get; set; } = string.Empty;
    public long TotalSpace { get; set; }
    public long FreeSpace { get; set; }
    public long UsedSpace => TotalSpace - FreeSpace;

    public string TotalSpaceReadable => FormatSize(TotalSpace);
    public string FreeSpaceReadable => FormatSize(FreeSpace);
    public string UsedSpaceReadable => FormatSize(UsedSpace);

    private static string FormatSize(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        double dblSByte = bytes;
        int i;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }
        return $"{dblSByte:0.##} {suffix[i]}";
    }
}
