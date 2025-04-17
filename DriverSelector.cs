using OpenQA.Selenium;
using OpenQA.Selenium.DevTools.V133.Debugger;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Cookie = OpenQA.Selenium.Cookie;

namespace RoomAssign;

public class DriverSelector(
    IWebDriver driver,
    string userAccount,
    string userPassword,
    string applyerName,
    List<HouseCondition> communityList,
    string startTime,
    CancellationToken cancellationToken,
    bool autoConfirm = false,
    int clickIntervalMs = 200,
    string cookie = null) : ISelector
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

    private void Login(string userAccount, string userPassword, string cookie = null)
    {
        var cookieSuccess = false;
        if (!string.IsNullOrEmpty(cookie))
        {
            var seleniumCookie = new Cookie(
                name: "SYS_USER_COOKIE_KEY",
                value: cookie,
                domain: "ent.qpgzf.cn",
                path: "/",
                expiry: DateTime.Now.AddHours(10),
                secure: false,
                isHttpOnly: false,
                sameSite: "Lax");
            driver.Navigate().GoToUrl("https://ent.qpgzf.cn/");
            driver.Manage().Cookies.AddCookie(seleniumCookie);
            driver.Navigate().GoToUrl("https://ent.qpgzf.cn/CompanyHome/Main");
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
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
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(600));
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

    private void SwitchBack()
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        driver.SwitchTo().DefaultContent();
    }

    private void SearchCommunity(string communityName)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(1));
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
        var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(20));
        var table = wait.Until(ExpectedConditions.ElementExists(By.Id("common-table")));
        var tbody = driver.FindElement(By.XPath("//table[@id='common-table']/tbody"));

        // var rows = new List<IWebElement>();

        // 如果 tbody 下有 tr 元素，则查找所有的行
        var rows = driver.FindElements(By.XPath("//table[@id='common-table']/tbody/tr"));

        foreach (var row in rows)
        {
            var commName = row.FindElement(By.XPath("./td[2]")).Text.Trim();
            var buildingNo = int.Parse(row.FindElement(By.XPath("./td[3]")).Text.Trim());
            // 注意：这里提取楼层时，根据实际情况调整截取逻辑
            var floorNoText = row.FindElement(By.XPath("./td[4]")).Text.Trim();
            var floorNo = int.Parse(floorNoText.Substring(0, Math.Min(2, floorNoText.Length)));
            var price = double.Parse(row.FindElement(By.XPath("./td[6]")).Text.Trim());
            var area = double.Parse(row.FindElement(By.XPath("./td[8]")).Text.Trim());
            var houseTypeString = row.FindElement(By.XPath("./td[9]")).Text.Trim();
            var houseType = EnumHelper.GetEnumValueFromDescription<HouseType>(houseTypeString);

            var selectButton = row.FindElement(By.XPath("./td[1]//a"));

            // 保存一个首次匹配到的房源（仅社区和房型匹配）作为备用
            firstOption ??= selectButton;

            // 使用辅助方法检查所有条件
            if (commName == condition.CommunityName &&
                houseType == condition.HouseType &&
                FilterEqual(buildingNo, condition.BuildingNo) &&
                FilterFloor(floorNo, condition.FloorRange) &&
                FilterPrice(price, condition.MaxPrice) &&
                FilterArea(area, condition.LeastArea))
            {
                bestMatch = selectButton;
                Console.WriteLine($"找到完全匹配房源: {commName} 幢:{buildingNo} 楼层:{floorNo} 价格:{price} 面积:{area}");
                found = true;
                break;
            }

            // 如果仅社区、房型和楼层匹配，则记录备用
            if (commName == condition.CommunityName &&
                houseType == condition.HouseType &&
                FilterFloor(floorNo, condition.FloorRange))
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
            Console.WriteLine("未找到完全匹配，选择了仅符合楼层条件的房源");
            break;
        }

        if (firstOption != null)
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", firstOption);
            Console.WriteLine("所有条件都不满足，选择了一个房源作为备用");
            break;
        }

        Console.WriteLine("11");

        try
        {
            // 定位到分页的 div 和当前页的链接
            var pagingDiv = driver.FindElement(By.ClassName("paging"));
            var currentPageElement = pagingDiv.FindElement(By.CssSelector("a[class='homepage'][class*='current']"));
            
            // 获取当前页的页码
            int currentPage = int.Parse(currentPageElement.Text);
            
            // 获取总页数
            var totalPagesElement = pagingDiv.FindElement(By.Id("TotalPages"));
            int totalPages = int.Parse(totalPagesElement.Text);
            
            // 判断是否为最后一页
            if (currentPage < totalPages)
            {
                // 如果不是最后一页，点击“下一页”按钮
                var nextPageButton = pagingDiv.FindElement(By.ClassName("page-next"));
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPageButton);
                Console.WriteLine("跳转到下一页...");
            }
            else
            {
                Console.WriteLine("已翻到最后一页，未找到合适的房源");
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("分页操作时发生错误: " + ex.Message);
        }

    }

    return found;
}

    private void ConfirmSelection()
    {
        try
        {
            SwitchBack();
            var confirmButton = driver.FindElement(By.XPath("//button/span[text()='确定']"));
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", confirmButton);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private void FinalConfirm()
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            var confirmButton =
                wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//button/span[text()='最终确认']")));
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", confirmButton);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task RunAsync()
    {
        if ((string.IsNullOrWhiteSpace(userAccount) || string.IsNullOrWhiteSpace(userPassword)) &&
            string.IsNullOrWhiteSpace(cookie))
        {
            Console.WriteLine("用户名、密码和cookie不能为空");
            return;
        }

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

        bool found = false;
        foreach (var condition in communityList)
        {
            Console.WriteLine($"正在搜索 {condition.CommunityName}...");
            SearchCommunity(condition.CommunityName);
            Console.WriteLine($"开始选择 {condition.CommunityName} 的房源...");
            found = SelectBestHouse(condition);
            if (found)
                break;
        }

        if (!found){
            Console.WriteLine("未找到所有房源");
            // 无限等待
            await Task.Delay(-1, cancellationToken);
            return;
        }

        ConfirmSelection();
        if (autoConfirm)
        {
            FinalConfirm();
        }
        else
        {
            Console.WriteLine("请手动确认选房结果");
        }
        
        Console.WriteLine("选房流程完成，请自行确认相关信息");
        // 无限等待
        await Task.Delay(-1, cancellationToken);
    }

    public void Stop()
    {
        driver.Quit();
    }
    
    /// <summary>
    /// 如果过滤值为 0，则不过滤，否则要求 actual 等于 filter。
    /// </summary>
    private bool FilterEqual(int actual, int filter) =>
        filter == 0 || actual == filter;

    /// <summary>
    /// 如果最大价格为 0，则不过滤，否则要求价格不大于 maxPrice。
    /// </summary>
    private bool FilterPrice(double price, int maxPrice) =>
        maxPrice == 0 || price <= maxPrice;

    /// <summary>
    /// 如果最小面积为 0，则不过滤，否则要求面积不小于 minArea。
    /// </summary>
    private bool FilterArea(double area, int minArea) =>
        minArea == 0 || area >= minArea;

    /// <summary>
    /// 如果楼层过滤条件为空或 null，则不过滤，否则解析出允许的楼层后检查当前楼层是否包含在内。
    /// </summary>
    private bool FilterFloor(int floor, string floorRange)
    {
        if (string.IsNullOrWhiteSpace(floorRange))
            return true;
        var allowedFloors = HouseCondition.ParseFloorRange(floorRange);
        return allowedFloors.Count == 0 || allowedFloors.Contains(floor);
    }

}