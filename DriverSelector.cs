using OpenQA.Selenium;
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

    public void Login(string userAccount, string userPassword, string cookie = null)
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
                var houseTypeString = row.FindElement(By.XPath("./td[9]")).Text.Trim();
                var houseType =  EnumHelper.GetEnumValueFromDescription<HouseType>(houseTypeString);

                var selectButton = row.FindElement(By.XPath("./td[1]//a"));

                firstOption ??= selectButton;

                if (commName == condition.CommunityName && houseType == condition.HouseType &&
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

                if (commName == condition.CommunityName && houseType == condition.HouseType &&
                    (floorNo == condition.FloorNo || condition.FloorNo == 0))
                {
                    floorMatch = selectButton;
                }
                
                if (commName == condition.CommunityName && houseType == condition.HouseType)
                {
                    firstOption = selectButton;
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
                Console.WriteLine("所有条件都不满足，选择了一个房源");
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
}