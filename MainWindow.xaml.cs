using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RoomAssign
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<HouseCondition> CommunityConditions { get; set; }
        private CancellationTokenSource cts;
        private ISelector? Selector { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            CommunityConditions = new ObservableCollection<HouseCondition>();
            // 添加初始一行默认数据
            CommunityConditions.Add(new HouseCondition("正荣景苑", 0, 0, 0, 0, HouseType.OneRoom));
            CommunityDataGrid.ItemsSource = CommunityConditions;

            // 将 Console 输出重定向到 LogTextBox 控件
            Console.SetOut(new TextBoxStreamWriter(LogTextBox));
        }

        private void AddCommunity_Click(object sender, RoutedEventArgs e)
        {
            CommunityConditions.Add(new HouseCondition("", 0, 0, 0, 0));
        }

        private void DeleteCommunity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is HouseCondition condition)
            {
                CommunityConditions.Remove(condition);
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            cts = new CancellationTokenSource();
            var operationMode = GetOperationMode(((ComboBoxItem)OperationModeComboBox.SelectedItem).Content.ToString());
            var driverType = GetDriverType(((ComboBoxItem)BrowserComboBox.SelectedItem).Content.ToString());
            var userAccount = AccountTextBox.Text;
            var userPassword = PasswordBox.Password;
            var cookie = CookieTextBox.Text;
            var applyerName = ApplyerTextBox.Text;
            var date = StartDatePicker.SelectedDate ?? DateTime.Now;
            var hour = HourTextBox.Text;
            var minute = MinuteTextBox.Text;
            var second = SecondTextBox.Text;
            var startTime = $"{date:yyyy-MM-dd} {hour}:{minute}:{second}";
            var autoConfirm = AutoConfirmCheckBox.IsChecked ?? false;

            // 读取点击触发间隔(ms)值
            if (!int.TryParse(ClickIntervalTextBox.Text, out int clickInterval))
            {
                clickInterval = 200;
            }

            var communityList = new List<HouseCondition>(CommunityConditions);

            await Task.Run(async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(applyerName))
                    {
                        Console.WriteLine("申请人名称不能为空");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(startTime))
                    {
                        Console.WriteLine("选房开始时间不能为空");
                        return;
                    }

                    if (communityList.Count == 0)
                    {
                        Console.WriteLine("社区条件不能为空");
                        return;
                    }

                    switch (operationMode)
                    {
                        case OperationMode.Click:
                            Console.WriteLine("正在启动浏览器...");
                            // 根据选择的浏览器创建相应的驱动
                            IWebDriver driver = null!;
                            Dispatcher.Invoke(() => { driver = GetDriver(driverType); });
                            // 自动化流程
                            using (driver)
                            {
                                Selector = new DriverSelector(
                                    driver: driver,
                                    userAccount: userAccount,
                                    userPassword: userPassword,
                                    applyerName: applyerName,
                                    communityList: communityList,
                                    startTime: startTime,
                                    cancellationToken: cts.Token,
                                    clickIntervalMs: clickInterval,
                                    cookie: cookie);

                                await Selector.RunAsync();
                            }

                            break;
                        case OperationMode.Http:
                            // 通过 Http 发包的方式进行自动化
                            Selector = new HttpSelector(
                                applyerName: applyerName,
                                communityList: communityList,
                                startTime: startTime,
                                cancellationToken: cts.Token,
                                clickIntervalMs: clickInterval,
                                cookie: cookie);

                            await Selector.RunAsync();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("自动化过程中出现错误: " + ex.Message);
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        StartButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                    });
                }
            }, cts.Token);
        }


        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // 通过取消任务和退出浏览器来尝试停止自动化
            try
            {
                cts?.Cancel();
                Selector?.Stop();
                Console.WriteLine("已停止抢房。");
            }
            catch (Exception ex)
            {
                Console.WriteLine("停止抢房时出现错误: " + ex.Message + ex.StackTrace);
            }
            finally
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }

        private OperationMode GetOperationMode(string mode)
        {
            return mode switch
            {
                "Http发包" => OperationMode.Http,
                "模拟点击" => OperationMode.Click,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        private DriverType GetDriverType(string type)
        {
            return type switch
            {
                "Chrome" => DriverType.Chrome,
                "Edge" => DriverType.Edge,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private IWebDriver GetDriver(DriverType driverType)
        {
            IWebDriver driver = null!;

            switch (driverType)
            {
                case DriverType.Chrome:
                    var chromeOptions = new ChromeOptions();
                    driver = new ChromeDriver(chromeOptions);
                    break;
                case DriverType.Edge:
                    var edgeOptions = new EdgeOptions();
                    edgeOptions.AddArgument("--edge-skip-compat-layer-relaunch");
                    driver = new EdgeDriver(edgeOptions);
                    break;
                default:
                    break;
            }

            return driver;
        }
    }
}