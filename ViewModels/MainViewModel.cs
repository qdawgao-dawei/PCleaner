using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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

    public ICollectionView ScanItemsView { get; }

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

    [ObservableProperty]
    private double _cleanablePercent;

    [ObservableProperty]
    private ObservableCollection<ScanItem> _topItems = new();

    [ObservableProperty]
    private string _greenSizeText = "0 B";

    [ObservableProperty]
    private string _yellowSizeText = "0 B";

    [ObservableProperty]
    private string _redSizeText = "0 B";

    [ObservableProperty]
    private ObservableCollection<string> _longTermSuggestions = new();

    public string TotalCleanableSizeReadable => FormatSize(TotalCleanableSize);

    public MainViewModel()
    {
        ScanItemsView = CollectionViewSource.GetDefaultView(ScanItems);
        ScanItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        ScanItemsView.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
        ScanItemsView.SortDescriptions.Add(new SortDescription("SizeBytes", ListSortDirection.Descending));
        
        LoadDisks();
        PopulateDefaultSuggestions();
    }

    private void PopulateDefaultSuggestions()
    {
        LongTermSuggestions.Clear();
        LongTermSuggestions.Add("● 定期清理：定期运行本工具或使用系统自带的‘磁盘清理’。");
        LongTermSuggestions.Add("● 存储感知：在 Windows 设置中开启‘存储感知’，自动清理临时文件。");
        LongTermSuggestions.Add("● 大文件迁移：将下载文件夹中的大文件或长期不用的视频迁移至非系统盘或云端。");
        LongTermSuggestions.Add("● 软件卸载：检查‘设置 > 应用’，卸载那些占用巨大且不再使用的软件。");
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
        StatusText = "正在深入诊断磁盘占用...";
        ScanItems.Clear();
        TopItems.Clear();
        TotalCleanableSize = 0;
        CleanablePercent = 0;

        var progress = new Progress<string>(p => StatusText = p);
        var results = await _scannerService.ScanAsync(progress);

        foreach (var item in results)
        {
            ScanItems.Add(item);
        }

        // Identify Top 5 heavy hitters
        var tops = results.OrderByDescending(i => i.SizeBytes).Take(5).ToList();
        foreach (var top in tops) TopItems.Add(top);

        UpdateTotalSize();
        IsScanning = false;
        StatusText = $"诊断完成，识别到 {ScanItems.Count} 个主要占用源";
    }

    [RelayCommand]
    private void CleanSelected()
    {
        var toClean = ScanItems.Where(i => i.IsSelected && i.Category == "Green").ToList();
        if (!toClean.Any())
        {
            MessageBox.Show("没有选中任何可自动清理的项（绿色）。绿色项是系统确认安全的缓存，建议优先清理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定要一键清理选中的 {toClean.Count} 个安全项吗？\n这将立即释放约 {FormatSize(toClean.Sum(i => i.SizeBytes))} 空间。", "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _fileOpService.UseRecycleBin = UseRecycleBin;
        int successCount = 0;

        foreach (var item in toClean)
        {
            if (_fileOpService.Delete(item.Path))
            {
                ScanItems.Remove(item);
                if (TopItems.Contains(item)) TopItems.Remove(item);
                successCount++;
            }
        }

        UpdateTotalSize();
        LoadDisks(); // Refresh disk info
        StatusText = $"清理完成，成功执行 {successCount} 项安全清理任务";
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
            MessageBox.Show($"无法打开该路径。如果它是系统保护目录，可能需要管理员权限。\n错误信息: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteItem(ScanItem item)
    {
        if (item == null) return;
        
        string message = $"确定要删除‘{item.Name}’吗？";
        if (item.Category == "Yellow")
        {
            message += $"\n\n风险提示：{item.RiskHint}\n建议先打开文件夹核实内容。";
        }

        if (MessageBox.Show(message, "单项删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _fileOpService.UseRecycleBin = UseRecycleBin;
        if (_fileOpService.Delete(item.Path))
        {
            ScanItems.Remove(item);
            if (TopItems.Contains(item)) TopItems.Remove(item);
            UpdateTotalSize();
            LoadDisks(); // Refresh disk info
            StatusText = $"已成功移除: {item.Name}";
        }
        else
        {
            MessageBox.Show("操作失败。该目录可能正在被其他程序使用，或由于系统权限限制无法删除。");
        }
    }

    private void UpdateTotalSize()
    {
        TotalCleanableSize = ScanItems.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
        
        GreenSizeText = FormatSize(ScanItems.Where(i => i.Category == "Green").Sum(i => i.SizeBytes));
        YellowSizeText = FormatSize(ScanItems.Where(i => i.Category == "Yellow").Sum(i => i.SizeBytes));
        RedSizeText = FormatSize(ScanItems.Where(i => i.Category == "Red").Sum(i => i.SizeBytes));

        if (SelectedDisk != null && SelectedDisk.TotalSpace > 0)
        {
            CleanablePercent = (double)TotalCleanableSize / SelectedDisk.TotalSpace * 100;
        }
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
