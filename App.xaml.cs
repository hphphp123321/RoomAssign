using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media.Imaging;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.WPF;

namespace RoomAssign;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 配置更新器：传入 AppCast URL 及安全检查器（例如 Ed25519Checker）
        var sparkle = new SparkleUpdater("https://file.hpnas.life/appcast.xml",
            new Ed25519Checker(SecurityMode.Unsafe, "Mr4+YFcg8p/g24a+aJ+A1DxesPtJZYFEGY2P2LScAqM="))
        {
            // 设置 WPF UI 工厂（可自定义UI）
            UIFactory = new UIFactory(),
            // 是否在更新后自动重启应用（根据需求设置）
            RelaunchAfterUpdate = false
        };

        sparkle.CheckForUpdatesAtUserRequest(false);
        // 启动自动更新循环，参数 true 表示立即进行首次更新检查
        sparkle.StartLoop(false, TimeSpan.FromDays(10));
    }
}