using System.Configuration;
using System.Data;
using System.Windows;

namespace PCleaner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ev) => 
        {
            MessageBox.Show($"发生未处理的异常: {ev.ExceptionObject}", "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        base.OnStartup(e);
    }
}

