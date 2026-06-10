using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCleaner.Models;
using PCleaner.Services;

namespace PCleaner.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ScannerService _scannerService = new();
    private readonly FileOpService _fileOpService = new();

    [ObservableProperty]
    private ObservableCollection<ScanItem> _scanItems = new();

    [ObservableProperty]
    private ObservableCollection<DiskInfo> _disks = new();

    [ObservableProperty]
    private DiskInfo? _selectedDisk;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _useRecycleBin = true;

    [ObservableProperty]
    private long _totalCleanableSize;

    public string TotalCleanableSizeReadable => FormatSize(TotalCleanableSize);

    public MainViewModel()
    {
        LoadDisks();
    }

    private void LoadDisks()
    {
        Disks = new ObservableCollection<DiskInfo>(_scannerService.GetDisks());
        SelectedDisk = Disks.FirstOrDefault(d => d.Name.StartsWith("C:"));
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        IsScanning = true;
        StatusText = "正在准备扫描...";
        ScanItems.Clear();
        TotalCleanableSize = 0;

        var progress = new Progress<string>(p => StatusText = p);
        var results = await _scannerService.ScanAsync(progress);

        foreach (var item in results)
        {
            ScanItems.Add(item);
        }

        UpdateTotalSize();
        IsScanning = false;
        StatusText = $"扫描完成，共发现 {ScanItems.Count} 个项";
    }

    [RelayCommand]
    private void CleanSelected()
    {
        var toClean = ScanItems.Where(i => i.IsSelected && i.Category == "Green").ToList();
        if (!toClean.Any())
        {
            MessageBox.Show("没有选中任何可自动清理的项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定要清理选中的 {toClean.Count} 个项吗？", "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _fileOpService.UseRecycleBin = UseRecycleBin;
        int successCount = 0;

        foreach (var item in toClean)
        {
            if (_fileOpService.Delete(item.Path))
            {
                ScanItems.Remove(item);
                successCount++;
            }
        }

        UpdateTotalSize();
        StatusText = $"清理完成，成功清理 {successCount} 个项";
    }

    [RelayCommand]
    private void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开文件夹: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteItem(ScanItem item)
    {
        if (item == null) return;
        if (MessageBox.Show($"确定要删除 {item.Name} 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _fileOpService.UseRecycleBin = UseRecycleBin;
        if (_fileOpService.Delete(item.Path))
        {
            ScanItems.Remove(item);
            UpdateTotalSize();
            StatusText = $"已删除: {item.Name}";
        }
        else
        {
            MessageBox.Show("删除失败，可能文件正在被使用。");
        }
    }

    private void UpdateTotalSize()
    {
        TotalCleanableSize = ScanItems.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
        OnPropertyChanged(nameof(TotalCleanableSizeReadable));
    }

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
