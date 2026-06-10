using CommunityToolkit.Mvvm.ComponentModel;

namespace PCleaner.Models;

public partial class ScanItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Category { get; set; } = string.Empty; // Green, Yellow, Red
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected = true;

    public string SizeReadable
    {
        get
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            double dblSByte = SizeBytes;
            int i;
            long tempBytes = SizeBytes;
            for (i = 0; i < suffix.Length && tempBytes >= 1024; i++, tempBytes /= 1024)
            {
                dblSByte = tempBytes / 1024.0;
            }
            return $"{dblSByte:0.##} {suffix[i]}";
        }
    }
}
