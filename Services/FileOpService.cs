using System;
using System.IO;
using PCleaner.Helpers;

namespace PCleaner.Services;

public class FileOpService
{
    public bool UseRecycleBin { get; set; } = true;

    public bool Delete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path) && !Directory.Exists(path))
            return false;

        if (UseRecycleBin)
        {
            return WindowsApi.SendToRecycleBin(path);
        }
        else
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
