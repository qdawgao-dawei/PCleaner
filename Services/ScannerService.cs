using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PCleaner.Models;

namespace PCleaner.Services;

public class ScannerService
{
    private readonly string _home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly Dictionary<string, (string Name, string Desc, string Category, string Risk)> _knownApps = new()
    {
        { "Temp", ("系统临时文件", "应用程序生成的临时缓存和日志。", "Green", "") },
        { "npm", ("Node.js 缓存", "已下载的 npm 包缓存。", "Green", "") },
        { "NuGet", (".NET 包缓存", "NuGet 全局包缓存。", "Green", "") },
        { "pip", ("Python pip 缓存", "pip 下载的安装包缓存。", "Green", "") },
        { "Google\\Chrome\\User Data\\Default\\Cache", ("Chrome 缓存", "浏览器网页静态资源缓存。", "Green", "") },
        { "Microsoft\\Edge\\User Data\\Default\\Cache", ("Edge 缓存", "Edge 浏览器网页缓存。", "Green", "") },
        { "Spotify", ("Spotify 缓存", "离线下载的音乐和封面缓存。", "Green", "") },
        { "Discord", ("Discord 缓存", "聊天图片和视频缓存。", "Green", "") },
        { "Steam", ("Steam 缓存", "游戏着色器和下载缓存。", "Green", "") },
        { "WeChat Files", ("微信记录", "微信聊天记录、图片和视频。", "Yellow", "风险：直接删除可能导致聊天图片失效。") },
        { "Tencent", ("腾讯软件数据", "腾讯系列软件的运行数据。", "Yellow", "") },
        { "Downloads", ("下载文件夹", "用户手动下载的文件地。", "Yellow", "注意：包含个人重要文档。") },
        { "$Recycle.Bin", ("回收站", "已删除项的暂存地。", "Yellow", "提示：这是释放空间的最后一步。") }
    };

    public async Task<List<ScanItem>> ScanAsync(IProgress<string> progress)
    {
        var items = new List<ScanItem>();

        // 1. Scan Known Critical Hotspots
        var hotspots = new[]
        {
            Path.Combine(_localAppData, "Temp"),
            Path.Combine(_home, ".npm"),
            Path.Combine(_home, ".nuget", "packages"),
            Path.Combine(_home, "Downloads"),
            "C:\\$Recycle.Bin"
        };

        foreach (var path in hotspots)
        {
            if (Directory.Exists(path))
            {
                var item = await ScanDirectoryInternalAsync(path, progress);
                if (item != null && item.SizeBytes > 1024 * 1024) items.Add(item);
            }
        }

        // 2. Dynamic App Scanning (Local & Roaming)
        // This is the core logic from the skill: scan children and identify apps
        items.AddRange(await ScanAppDataAsync(_localAppData, "Local", progress));
        items.AddRange(await ScanAppDataAsync(_appData, "Roaming", progress));

        // 3. System Cleanups
        var progFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        if (Directory.Exists(progFiles))
        {
            items.Add(new ScanItem
            {
                Name = "应用安装目录",
                Path = progFiles,
                SizeBytes = await Task.Run(() => GetDirectorySize(progFiles)),
                Category = "Red",
                CategorySortIndex = 2,
                Description = "系统安装的应用本体所在地。不建议手动删除。",
                RiskHint = "警告：请使用系统‘设置’进行卸载。"
            });
        }

        // Filter and Deduplicate
        return items.GroupBy(i => i.Path)
                    .Select(g => g.First())
                    .Where(i => i.SizeBytes > 1024 * 1024 * 5) // Minimum 5MB to show
                    .OrderByDescending(i => i.SizeBytes)
                    .ToList();
    }

    private async Task<List<ScanItem>> ScanAppDataAsync(string root, string label, IProgress<string> progress)
    {
        var results = new List<ScanItem>();
        if (!Directory.Exists(root)) return results;

        try
        {
            var subDirs = Directory.GetDirectories(root);
            foreach (var dir in subDirs)
            {
                var name = Path.GetFileName(dir);
                progress?.Report($"正在诊断 {label}: {name}");

                var item = await ScanDirectoryInternalAsync(dir, progress);
                if (item != null) results.Add(item);
            }
        }
        catch { }
        return results;
    }

    private async Task<ScanItem?> ScanDirectoryInternalAsync(string path, IProgress<string> progress)
    {
        try
        {
            long size = await Task.Run(() => GetDirectorySize(path));
            if (size <= 0) return null;

            var name = Path.GetFileName(path);
            var item = new ScanItem { Name = name, Path = path, SizeBytes = size };

            // Match against known apps for better metadata
            bool matched = false;
            foreach (var entry in _knownApps)
            {
                if (path.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    item.Name = entry.Value.Name;
                    item.Description = entry.Value.Desc;
                    item.Category = entry.Value.Category;
                    item.CategorySortIndex = item.Category == "Green" ? 0 : 1;
                    item.RiskHint = entry.Value.Risk;
                    item.IsSelected = item.Category == "Green";
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                item.Name = $"{name} 数据";
                item.Description = "识别到该应用占用了一定空间。通常包含应用配置、缓存或用户生成数据。";
                item.Category = "Yellow";
                item.CategorySortIndex = 1;
                item.IsSelected = false;
            }

            return item;
        }
        catch { return null; }
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            // Non-recursive first level to avoid infinite deep dives for performance, 
            // but the skill expects total size. We'll do a safe recursion.
            size = GetSizeSafe(dirInfo, 0);
        }
        catch { }
        return size;
    }

    private long GetSizeSafe(DirectoryInfo d, int depth)
    {
        if (depth > 5) return 0; // Prevent hanging on symlink loops
        long size = 0;
        try
        {
            foreach (var f in d.EnumerateFiles()) size += f.Length;
            foreach (var sub in d.EnumerateDirectories()) size += GetSizeSafe(sub, depth + 1);
        }
        catch { }
        return size;
    }

    public List<DiskInfo> GetDisks()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DiskInfo
            {
                Name = d.Name,
                TotalSpace = d.TotalSize,
                FreeSpace = d.TotalFreeSpace
            }).ToList();
    }
}
