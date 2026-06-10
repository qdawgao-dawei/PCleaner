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
        
        var targets = new (string key, string path, string description, string category, string riskHint)[]
        {
            // Green: Automatic Cleanup
            ("系统临时文件", Path.Combine(_localAppData, "Temp"), "包含应用程序运行时生成的临时缓存和日志文件。清理后不影响应用正常运行，系统会自动重新生成所需文件。建议定期清理以释放空间。", "Green", ""),
            ("用户临时目录", Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? ""), "Windows 用户级的临时存储空间。包含安装程序残留和会话缓存。清理这些文件是非常安全的。", "Green", ""),
            ("Npm 缓存", Path.Combine(_home, ".npm"), "Node.js 包管理器的本地缓存。包含已下载过的 npm 包。清理后下次安装包时会从网络重新下载，速度可能稍慢。", "Green", ""),
            ("NuGet 缓存", Path.Combine(_home, ".nuget", "packages"), ".NET 开发的全局包缓存。包含所有已下载的项目依赖。清理可释放大量空间，编译新项目时会自动恢复。", "Green", ""),
            ("Pip 缓存", Path.Combine(_localAppData, "pip", "Cache"), "Python pip 的安装包缓存。包含历史下载的 whl 和源码包。清理这些离线文件不影响已安装的库。", "Green", ""),
            ("Maven 仓库", Path.Combine(_home, ".m2", "repository"), "Java Maven 项目的本地依赖仓库。长期积累会占用巨大空间。清理后 IDE 会根据项目需求重新下载依赖。", "Green", ""),
            ("Gradle 缓存", Path.Combine(_home, ".gradle", "caches"), "Gradle 构建工具的缓存和依赖。包含各种版本的库文件。清理后首次构建项目会比较慢，因为需要重下资源。", "Green", ""),
            ("浏览器缓存", Path.Combine(_localAppData, "Google", "Chrome", "User Data", "Default", "Cache"), "浏览器浏览网页时留下的图片和静态资源缓存。清理后网页首次加载可能稍慢，但不影响书签和登录态。", "Green", ""),
            
            // Yellow: Manual Review
            ("下载文件夹", Path.Combine(_home, "Downloads"), "用户手动下载的所有文件存放地。这里可能包含重要的文档、安装包或个人资料。建议进入文件夹手动筛选后再决定删除。", "Yellow", "注意：删除后可能无法找回你的个人下载文档。"),
            ("回收站", "C:\\$Recycle.Bin", "系统回收站暂存的已删除项。虽然不占用用户感知空间，但实际占用物理磁盘。建议清空前确认没有误删的重要文件。", "Yellow", "提示：清空回收站是释放空间的最后一步。"),
            ("WeChat 缓存", Path.Combine(_home, "Documents", "WeChat Files"), "微信的聊天记录、图片和视频。这里通常是占用空间的大头。建议在微信应用内使用其自带的清理工具进行精细操作。", "Yellow", "风险：直接删除可能导致聊天图片或视频无法查看。"),
            
            // Red: Caution
            ("应用安装目录", Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "系统安装的主要应用程序所在地。虽然占用大，但不建议手动删除此处的文件夹。如需卸载，请前往‘设置 > 应用’进行标准卸载。", "Red", "警告：手动删除可能导致应用损坏且残留注册表垃圾。"),
        };

        foreach (var target in targets)
        {
            if (string.IsNullOrEmpty(target.path)) continue;

            if (Directory.Exists(target.path))
            {
                progress?.Report($"正在诊断: {target.key}");
                long size = await Task.Run(() => GetDirectorySize(target.path));
                if (size > 1024 * 1024 * 1) // 只显示大于 1MB 的项，减少干扰
                {
                    items.Add(new ScanItem
                    {
                        Name = target.key,
                        Path = target.path,
                        SizeBytes = size,
                        Category = target.category,
                        Description = target.description,
                        RiskHint = target.riskHint
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
