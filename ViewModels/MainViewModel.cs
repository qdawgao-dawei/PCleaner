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
using PCleaner.Helpers;

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
    private bool _hasScannedData;

    [ObservableProperty]
    private bool _useRecycleBin = true;

    [ObservableProperty]
    private long _totalCleanableSize;

    [ObservableProperty]
    private long _greenTotalSize;

    [ObservableProperty]
    private long _yellowTotalSize;

    [ObservableProperty]
    private long _redTotalSize;

    [ObservableProperty]
    private long _otherUsedSize;

    [ObservableProperty]
    private long _freeSpaceSize;

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
        ScanItemsView.SortDescriptions.Add(new SortDescription("CategorySortIndex", ListSortDirection.Ascending));
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
        HasScannedData = true;
        StatusText = $"诊断完成，识别到 {ScanItems.Count} 个主要占用源";
    }

    [RelayCommand]
    private async Task CleanSelectedAsync()
    {
        var toClean = ScanItems.Where(i => i.IsSelected && i.Category == "Green").ToList();
        if (!toClean.Any())
        {
            MessageBox.Show("没有选中任何可自动清理的项（绿色）。绿色项是系统确认安全的缓存，建议优先清理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定要一键清理选中的 {toClean.Count} 个安全项吗？\n这将立即释放约 {FormatSize(toClean.Sum(i => i.SizeBytes))} 空间。", "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        IsScanning = true; // Use scanning state to show background progress
        StatusText = "正在后台执行静默清理...";
        
        _fileOpService.UseRecycleBin = UseRecycleBin;
        int successCount = 0;
        int failCount = 0;

        await Task.Run(() =>
        {
            foreach (var item in toClean)
            {
                if (_fileOpService.Delete(item.Path))
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        ScanItems.Remove(item);
                        if (TopItems.Contains(item)) TopItems.Remove(item);
                    });
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
        });

        UpdateTotalSize();
        LoadDisks(); 
        IsScanning = false;
        
        if (failCount > 0)
        {
            StatusText = $"清理完成：成功 {successCount} 项，{failCount} 项因文件锁定被跳过";
            MessageBox.Show($"清理任务已结束。\n\n成功清理: {successCount} 个项目\n跳过项目: {failCount} 个 (原因：文件正在被其他程序占用或受保护)\n\n建议：请尝试关闭相关的应用程序（如浏览器、开发工具等）后再重试。", "部分完成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            StatusText = $"清理完成，成功执行 {successCount} 项安全清理任务";
        }
    }

    [RelayCommand]
    private void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            // 特殊处理回收站：使用 shell 指令打开虚拟目录，而不是物理路径
            if (path.Contains("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process.Start("explorer.exe", "shell:RecycleBinFolder");
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开该路径。如果它是系统保护目录，可能需要管理员权限。\n错误信息: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ScanItem item)
    {
        if (item == null) return;
        
        string message = $"确定要删除‘{item.Name}’吗？";
        if (item.Category == "Yellow")
        {
            message += $"\n\n风险提示：{item.RiskHint}\n建议先打开文件夹核实内容。";
        }

        if (MessageBox.Show(message, "单项删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        StatusText = $"正在删除: {item.Name}...";
        _fileOpService.UseRecycleBin = UseRecycleBin;
        
        bool success = await Task.Run(() => _fileOpService.Delete(item.Path));

        if (success)
        {
            ScanItems.Remove(item);
            if (TopItems.Contains(item)) TopItems.Remove(item);
            UpdateTotalSize();
            LoadDisks();
            StatusText = $"已成功移除: {item.Name}";
        }
        else
        {
            StatusText = $"删除失败: {item.Name} 被占用";
            MessageBox.Show($"无法删除项目‘{item.Name}’。\n\n原因：文件可能正在被其他程序（如浏览器或后台服务）使用，或者该目录受系统保护。\n\n建议：请尝试关闭相关的应用程序后再重试。", "操作被拦截", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void UpdateTotalSize()
    {
        TotalCleanableSize = ScanItems.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
        
        GreenTotalSize = ScanItems.Where(i => i.Category == "Green").Sum(i => i.SizeBytes);
        YellowTotalSize = ScanItems.Where(i => i.Category == "Yellow").Sum(i => i.SizeBytes);
        RedTotalSize = ScanItems.Where(i => i.Category == "Red").Sum(i => i.SizeBytes);

        GreenSizeText = FormatSize(GreenTotalSize);
        YellowSizeText = FormatSize(YellowTotalSize);
        RedSizeText = FormatSize(RedTotalSize);

        if (SelectedDisk != null && SelectedDisk.TotalSpace > 0)
        {
            CleanablePercent = (double)TotalCleanableSize / SelectedDisk.TotalSpace * 100;
            
            // OtherUsed = TotalUsed - (Green + Yellow + Red scanned portions)
            long scannedTotal = GreenTotalSize + YellowTotalSize + RedTotalSize;
            OtherUsedSize = Math.Max(0, SelectedDisk.UsedSpace - scannedTotal);
            FreeSpaceSize = SelectedDisk.FreeSpace;
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
