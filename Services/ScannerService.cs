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

    public async Task<List<ScanItem>> ScanAsync(IProgress<string> progress)
    {
        var items = new List<ScanItem>();
        
        var targets = new (string key, string path, string description, string category)[]
        {
            ("Temp", Path.Combine(_localAppData, "Temp"), "系统临时文件", "Green"),
            ("User Temp", Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? ""), "用户临时目录", "Green"),
            ("Downloads", Path.Combine(_home, "Downloads"), "下载文件夹内容", "Yellow"),
            ("Npm Cache", Path.Combine(_home, ".npm"), "Node.js 缓存", "Green"),
            ("NuGet Packages", Path.Combine(_home, ".nuget", "packages"), "NuGet 包缓存", "Green"),
            ("Pip Cache", Path.Combine(_localAppData, "pip", "Cache"), "Python pip 缓存", "Green"),
            ("Chrome Cache", Path.Combine(_localAppData, "Google", "Chrome", "User Data", "Default", "Cache"), "Chrome 浏览器缓存", "Green"),
            ("Edge Cache", Path.Combine(_localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache"), "Edge 浏览器缓存", "Green"),
            ("Maven Repo", Path.Combine(_home, ".m2", "repository"), "Maven 仓库", "Green"),
            ("Gradle Cache", Path.Combine(_home, ".gradle", "caches"), "Gradle 缓存", "Green"),
            ("Cargo Registry", Path.Combine(_home, ".cargo", "registry"), "Rust Cargo 仓库", "Green"),
            ("Yarn Cache", Path.Combine(_localAppData, "Yarn", "Cache"), "Yarn 缓存", "Green"),
            ("Recycle Bin", "C:\\$Recycle.Bin", "回收站内容", "Yellow"),
        };

        foreach (var target in targets)
        {
            if (string.IsNullOrEmpty(target.path)) continue;

            if (Directory.Exists(target.path))
            {
                progress?.Report($"正在扫描: {target.key}");
                long size = await Task.Run(() => GetDirectorySize(target.path));
                if (size > 512) // Only show items larger than 0.5KB
                {
                    items.Add(new ScanItem
                    {
                        Name = target.key,
                        Path = target.path,
                        SizeBytes = size,
                        Category = target.category,
                        Description = target.description
                    });
                }
            }
        }

        return items;
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            size += dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                          .Sum(f => f.Length);
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception) { }
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
