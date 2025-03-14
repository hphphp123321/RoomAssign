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
        private IWebDriver driver;

        public MainWindow()
        {
            InitializeComponent();
            CommunityConditions = new ObservableCollection<HouseCondition>();
            // 添加初始一行默认数据
            CommunityConditions.Add(new HouseCondition("境秋华庭", 0, 0, 0, 0));
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
            var operationMode = ((ComboBoxItem)OperationModeComboBox.SelectedItem).Content.ToString();
            var userAccount = AccountTextBox.Text;
            var userPassword = PasswordBox.Password;
            var cookie = CookieTextBox.Text;
            var applyerName = ApplyerTextBox.Text;
            var date = StartDatePicker.SelectedDate ?? DateTime.Now;
            var hour = HourTextBox.Text;
            var minute = MinuteTextBox.Text;
            var second = SecondTextBox.Text;
            var startTime = $"{date:yyyy-MM-dd} {hour}:{minute}:{second}";

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
                    Console.WriteLine("正在启动浏览器...");
                    // 根据选择的浏览器创建相应的驱动
                    Dispatcher.Invoke(() =>
                    {
                        var selectedBrowser = ((ComboBoxItem)BrowserComboBox.SelectedItem).Content.ToString();
                        if (selectedBrowser == "Chrome")
                        {
                            driver = new ChromeDriver();
                        }
                        else
                        {
                            driver = new EdgeDriver();
                        }
                    });
                    // 自动化流程（HouseSelector.Run 内部逻辑保持不变）
                    using (driver)
                    {
                        var selector = new HouseSelector(driver, clickInterval);
                        await selector.Run(
                            mode: GetOperationMode(operationMode),
                            userAccount: userAccount,
                            userPassword: userPassword,
                            applyerName: applyerName,
                            communityList: communityList,
                            startTime: startTime,
                            cancellationToken: cts.Token,
                            cookie: cookie);
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
                if (driver != null)
                {
                    driver.Quit();
                    driver = null;
                    Console.WriteLine("已停止抢房。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("停止抢房时出现错误: " + ex.Message);
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
    }
}