using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace RoomAssign;

// 枚举类型，用于选择运行模式
public enum OperationMode
{
    Click, // 模拟点击方式
    Http // 发包方式
}

public class HouseSelector(IWebDriver driver, int clickIntervalMs = 200)
{
    private void WaitUntilStartTime(string startTime, CancellationToken cancellationToken)
    {
        var start = DateTime.ParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null);


        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var now = DateTime.Now;
            var remaining = start - now;
            if (remaining.TotalSeconds < 2)
            {
                Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                break;
            }

            Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {remaining}");
            Thread.Sleep(1000);
        }
    }

    public void Login(string userAccount, string userPassword, string cookie = null)
    {
        var cookieSuccess = false;
        if (!string.IsNullOrEmpty(cookie))
        {
            driver.Navigate().GoToUrl("https://ent.qpgzf.cn/CompanyHome/Main");
            var seleniumCookie = new Cookie("SYS_USER_COOKIE_KEY", cookie);
            driver.Manage().Cookies.AddCookie(seleniumCookie);
            driver.Navigate().Refresh();
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            try
            {
                driver.FindElement(By.Id("mainCompany"));
                Console.WriteLine("使用 Cookie 登录成功");
                cookieSuccess = true;
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("使用 Cookie 登录失败，尝试手动登录");
            }
        }

        if (cookieSuccess) return;
        driver.Navigate().GoToUrl("https://ent.qpgzf.cn/SysLoginManage");
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        driver.FindElement(By.Name("UserAccount")).SendKeys(userAccount);
        driver.FindElement(By.Name("PD")).SendKeys(userPassword);
        driver.FindElement(By.ClassName("CompanyloginButton")).Click();
        Console.WriteLine("请手动完成拖拽验证码...");
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
        wait.Until(d => d.Url == "https://ent.qpgzf.cn/CompanyHome/Main");
        Console.WriteLine("登录成功！");
    }

    private void NavigateToSelection(string applyerName)
    {
        driver.Navigate().GoToUrl("https://ent.qpgzf.cn/RoomAssign/Index");
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        driver.FindElement(By.CssSelector($"input[name='{applyerName}']")).Click();
    }

    private void StartAssignRoom()
    {
        Console.WriteLine("等待选房开始，尝试进入选房页面...");
        while (true)
        {
            try
            {
                var assignRoomBtn = driver.FindElement(By.CssSelector("a[onclick='assignRoom(1)']"));
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", assignRoomBtn);
                Thread.Sleep(clickIntervalMs);
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(2));
                var iframe = wait.Until(ExpectedConditions.ElementExists(By.Id("iframeDialog")));
                var src = iframe.GetAttribute("src");
                if (src.Contains("ApplyIDs"))
                {
                    Console.WriteLine("成功进入选房页面！");
                    break;
                }

                var alertBox = driver.FindElement(By.Id("sysAlert"));
                Console.WriteLine("当前时间段无法分配房源，关闭弹窗后继续尝试...");
                var closeButton = driver.FindElement(By.ClassName("ui-dialog-titlebar-close"));
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", closeButton);
                Thread.Sleep(clickIntervalMs);
            }
            catch (Exception)
            {
                try
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(2));
                    var iframe = wait.Until(ExpectedConditions.ElementIsVisible(By.Id("iframeDialog")));
                    var src = iframe.GetAttribute("src");
                    if (!src.Contains("ApplyIDs")) continue;
                    Console.WriteLine("成功进入选房页面！");
                    break;
                }
                catch (Exception)
                {
                    // 继续尝试
                }
            }
        }
    }

    private void SwitchToIframe()
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        var iframe = wait.Until(ExpectedConditions.ElementExists(By.Id("iframeDialog")));
        driver.SwitchTo().Frame(iframe);
    }

    private void SearchCommunity(string communityName)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        var searchInput =
            wait.Until(ExpectedConditions.ElementExists(By.Id("SearchEntity__CommonSearchCondition")));
        searchInput.Clear();
        searchInput.SendKeys(communityName);
        var searchBtn = wait.Until(ExpectedConditions.ElementExists(By.Id("submitButton")));
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", searchBtn);
    }

    private bool SelectBestHouse(HouseCondition condition)
    {
        var found = false;
        IWebElement bestMatch = null;
        IWebElement floorMatch = null;
        IWebElement firstOption = null;

        while (!found)
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var table = wait.Until(ExpectedConditions.ElementExists(By.Id("common-table")));
            var rows = driver.FindElements(By.XPath("//table[@id='common-table']/tbody/tr"));

            foreach (var row in rows)
            {
                var commName = row.FindElement(By.XPath("./td[2]")).Text.Trim();
                var buildingNo = int.Parse(row.FindElement(By.XPath("./td[3]")).Text.Trim());
                var floorNo = int.Parse(row.FindElement(By.XPath("./td[4]")).Text.Trim().Substring(0, 2));
                var price = double.Parse(row.FindElement(By.XPath("./td[6]")).Text.Trim());
                var area = double.Parse(row.FindElement(By.XPath("./td[8]")).Text.Trim());

                var selectButton = row.FindElement(By.XPath("./td[1]//a"));

                firstOption ??= selectButton;

                if (commName == condition.CommunityName &&
                    (buildingNo == condition.BuildingNo || condition.BuildingNo == 0) &&
                    (floorNo == condition.FloorNo || condition.FloorNo == 0) &&
                    (price <= condition.MaxPrice || condition.MaxPrice == 0) &&
                    (area >= condition.LeastArea || condition.LeastArea == 0))
                {
                    bestMatch = selectButton;
                    Console.WriteLine($"找到完全匹配房源: {commName} 幢:{buildingNo} 层:{floorNo} 价格:{price} 面积:{area}");
                    found = true;
                    break;
                }

                if (commName == condition.CommunityName &&
                    (floorNo == condition.FloorNo || condition.FloorNo == 0))
                {
                    floorMatch = selectButton;
                }
            }

            if (found)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", bestMatch);
                Console.WriteLine("成功选择完全匹配的房源");
                break;
            }

            if (floorMatch != null)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", floorMatch);
                Console.WriteLine("未找到完全匹配，选择了仅符合层号的房源");
                break;
            }

            if (firstOption != null)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", firstOption);
                Console.WriteLine("所有条件都不满足，选择了第一个房源");
                break;
            }

            try
            {
                var waitNext = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                var nextPage = waitNext.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("page-next")));
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPage);
                Console.WriteLine("跳转到下一页...");
                waitNext.Until(ExpectedConditions.StalenessOf(table));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("已翻到最后一页，未找到合适的房源");
                break;
            }
        }

        return found;
    }

    private void ConfirmSelection()
    {
        try
        {
            var confirmButton = driver.FindElement(By.XPath("//button/span[text()='确定']"));
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", confirmButton);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task Run(string userAccount, string userPassword, string applyerName,
        List<HouseCondition> communityList,
        string startTime, CancellationToken cancellationToken,
        OperationMode mode, string cookie = null)
    {
        // 校验参数
        if ((string.IsNullOrWhiteSpace(userAccount) || string.IsNullOrWhiteSpace(userPassword)) &&
            string.IsNullOrWhiteSpace(cookie))
        {
            Console.WriteLine("用户名、密码和cookie不能为空");
            return;
        }


        if (communityList.Count == 0)
        {
            Console.WriteLine("社区条件不能为空");
            return;
        }

        if (mode == OperationMode.Http)
        {
            // 发包模式必须传入 Cookie
            if (string.IsNullOrWhiteSpace(cookie))
            {
                Console.WriteLine("在发包模式下，Cookie 必填");
                return;
            }

            await RunHttp(applyerName, communityList, startTime, cancellationToken, cookie);
        }
        else // 模拟点击方式
        {
            RunClick(userAccount, userPassword, applyerName, communityList, startTime, cancellationToken, cookie);
        }
    }

    private async Task RunHttp(string applyerName,
        List<HouseCondition> communityList,
        string startTime, CancellationToken cancellationToken,
        string cookie)
    {
        using var client = new HttpClient();
        // 将 Cookie 添加到请求头
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        // 1. 通过 GET 请求获取 https://ent.qpgzf.cn/RoomAssign/Index 页面并解析申请人ID
        var applyerId = await GetApplyerId(client, applyerName);
        if (applyerId == null)
        {
            Console.WriteLine("无法获取申请人ID，退出");
            return;
        }

        // 2. 等待至选房开始时间
        WaitUntilStartTime(startTime, cancellationToken);
        Console.WriteLine("发包选房开始！");

        // 3. 通过发包匹配获取房间ID
        // 这里以 communityList 的第一个条件为示例
        var condition = communityList[0];
        var roomType = "一居室"; // TODO 先固定为一居室，后续可根据需求调整
        var roomId = await GetRoomId(client, applyerId, condition.CommunityName, roomType);
        if (roomId == null)
        {
            Console.WriteLine("无法获取匹配的房间ID，退出");
            return;
        }

        // TODO 定义发包时间窗口，例如选房开始后 10 秒内不断发包
        var endTime = DateTime.ParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null).AddSeconds(10);
        while (DateTime.Now < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            await SelectRoomAsync(client, applyerId, roomId);
            await Task.Delay(10, cancellationToken);
        }

        Console.WriteLine("发包流程完成");
    }

    private void RunClick(string userAccount, string userPassword, string applyerName,
        List<HouseCondition> communityList,
        string startTime, CancellationToken cancellationToken, string cookie = null)
    {
        Login(userAccount, userPassword, cookie);
        NavigateToSelection(applyerName);
        WaitUntilStartTime(startTime, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("操作已取消，退出选房流程");
            return;
        }

        Console.WriteLine("选房开始！");
        StartAssignRoom();
        SwitchToIframe();

        foreach (var condition in communityList)
        {
            Console.WriteLine($"正在搜索 {condition.CommunityName}...");
            SearchCommunity(condition.CommunityName);
            Console.WriteLine($"开始选择 {condition.CommunityName} 的房源...");
            var found = SelectBestHouse(condition);
            if (found)
                break;
        }

        ConfirmSelection();
        Console.WriteLine("选房流程完成");
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }

    /// <summary>
    /// 通过 GET 请求获取申请人ID，匹配 applyerName 对应的行
    /// </summary>
    private async Task<string?> GetApplyerId(HttpClient client, string applyerName)
    {
        try
        {
            var response = await client.GetAsync("https://ent.qpgzf.cn/RoomAssign/Index");
            var html = await response.Content.ReadAsStringAsync();
            // 正则匹配：通过 ondblclick 中的 show 函数参数获取申请人ID，同时匹配包含申请人名称的 <td>
            var pattern =
                $"<tr\\s+ondblclick=\"javascript:show\\('([^']+)'\\);\">[\\s\\S]*?<td>\\s*{Regex.Escape(applyerName)}\\s*</td>";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var applyerId = match.Groups[1].Value;
                Console.WriteLine($"成功获取申请人ID: {applyerId}");
                return applyerId;
            }
            else
            {
                Console.WriteLine($"未能从页面中找到申请人 \"{applyerName}\" 对应的 ID");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("获取申请人ID异常：" + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 通过 POST 请求匹配房源信息，返回匹配到的房间ID
    /// 逻辑参考你提供的 Python get_room_id 函数
    /// </summary>
    private async Task<string?> GetRoomId(HttpClient client, string applyIds, string communityName, string roomType)
    {
        // 此 URL 与参数根据实际情况调整
        string searchRoomUrl = "https://ent.qpgzf.cn/RoomAssign/SelectRoom";
        var data = new Dictionary<string, string>
        {
            { "ApplyIDs", applyIds },
            { "IsApplyTalent", "0" },
            { "type", "1" },
            { "SearchEntity._PageSize", "500" },
            { "SearchEntity._PageIndex", "1" }
        };
        try
        {
            var content = new FormUrlEncodedContent(data);
            var response = await client.PostAsync(searchRoomUrl, content);
            var html = await response.Content.ReadAsStringAsync();

            // 正则匹配：查找包含指定社区名称和房型的房源行，提取 selectRooms 的第一个参数即房间ID
            var pattern =
                $@"<tr>[\s\S]*?selectRooms\('([^']*)',[\s\S]*?{Regex.Escape(communityName)}[\s\S]*?{Regex.Escape(roomType)}[\s\S]*?</tr>";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var roomId = match.Groups[1].Value;
                Console.WriteLine($"获取到 {communityName} 最末房间ID：{roomId}");
                return roomId;
            }

            Console.WriteLine("未查询到匹配的房源信息...");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("获取房间ID异常：" + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 发包方式请求选房接口
    /// </summary>
    private async Task SelectRoomAsync(HttpClient client, string applyerId, string roomId)
    {
        var selectRoomUrl = "https://ent.qpgzf.cn/RoomAssign/AjaxSelectRoom";
        var data = new Dictionary<string, string>
        {
            { "ApplyIDs", applyerId },
            { "roomID", roomId }
        };
        var content = new FormUrlEncodedContent(data);
        Console.WriteLine("开始请求选房...");
        try
        {
            var response = await client.PostAsync(selectRoomUrl, content);
            var result = await response.Content.ReadAsStringAsync();
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("发包请求错误：" + ex.Message);
        }
    }
}