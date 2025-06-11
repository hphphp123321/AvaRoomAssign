using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Cookie = OpenQA.Selenium.Cookie;

namespace AvaRoomAssign.Models
{
    public class DriverSelector : ISelector
    {
        private readonly IWebDriver _driver;
        private readonly string _userAccount;
        private readonly string _userPassword;
        private readonly string _applyerName;
        private readonly List<HouseCondition> _communityList;
        private readonly string _startTime;
        private readonly CancellationToken _cancellationToken;
        private readonly bool _autoConfirm;
        private readonly int _clickIntervalMs;
        private readonly string? _cookie;

        public DriverSelector(
            IWebDriver driver,
            string userAccount,
            string userPassword,
            string applyerName,
            List<HouseCondition> communityList,
            string startTime,
            CancellationToken cancellationToken,
            bool autoConfirm = false,
            int clickIntervalMs = 200,
            string? cookie = null)
        {
            _driver = driver;
            _userAccount = userAccount;
            _userPassword = userPassword;
            _applyerName = applyerName;
            _communityList = communityList;
            _startTime = startTime;
            _cancellationToken = cancellationToken;
            _autoConfirm = autoConfirm;
            _clickIntervalMs = clickIntervalMs;
            _cookie = cookie;
        }

        public async Task RunAsync()
        {
            if ((string.IsNullOrWhiteSpace(_userAccount) || string.IsNullOrWhiteSpace(_userPassword))
                && string.IsNullOrWhiteSpace(_cookie))
            {
                Console.WriteLine("用户名、密码和cookie不能为空");
                return;
            }

            Login();
            NavigateToSelection();
            WaitUntilStartTime();
            if (_cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("操作已取消，退出选房流程");
                return;
            }

            Console.WriteLine("选房开始！");
            EnterSelectionPage();
            SwitchToIframe();
            
            var success = false;
            foreach (var condition in _communityList)
            {
                Console.WriteLine($"正在搜索 {condition.CommunityName}...");
                SearchCommunity(condition.CommunityName);
                Console.WriteLine($"开始选择 {condition.CommunityName} 的房源...");
                
                success = TryFindAndSelectHouse(condition);
                if (!success)
                {
                    Console.WriteLine($"志愿 {condition.CommunityName} 未中签，继续下一个志愿...");
                    continue;
                }

                ConfirmSelection();

                if (_autoConfirm)
                {
                    FinalConfirm();
                    if (!CheckSuccess())
                    {
                        Console.WriteLine("房间已被他人抢先，尝试下一个志愿...");
                        SwitchToIframe();
                        success = false;
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine("请手动进行最终确认");
                }

                // 成功选房，退出循环
                success = true;
                break;
            }

            if (!success)
            {
                Console.WriteLine("所有志愿均未中签，流程结束");
            }

            // 无限等待或根据需要退出
            await Task.Delay(-1, _cancellationToken);
        }

        private void Login()
        {
            var cookieSuccess = false;
            if (!string.IsNullOrEmpty(_cookie))
            {
                var seleniumCookie = new Cookie(
                    "SYS_USER_COOKIE_KEY", _cookie, "ent.qpgzf.cn", "/",
                    DateTime.Now.AddHours(10));
                _driver.Navigate().GoToUrl("https://ent.qpgzf.cn/");
                _driver.Manage().Cookies.AddCookie(seleniumCookie);
                _driver.Navigate().GoToUrl("https://ent.qpgzf.cn/CompanyHome/Main");
                _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
                try
                {
                    _driver.FindElement(By.Id("mainCompany"));
                    Console.WriteLine("使用 Cookie 登录成功");
                    cookieSuccess = true;
                }
                catch
                {
                    Console.WriteLine("使用 Cookie 登录失败，尝试手动登录");
                }
            }

            if (cookieSuccess) return;

            _driver.Navigate().GoToUrl("https://ent.qpgzf.cn/SysLoginManage");
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            _driver.FindElement(By.Name("UserAccount")).SendKeys(_userAccount);
            _driver.FindElement(By.Name("PD")).SendKeys(_userPassword);
            _driver.FindElement(By.ClassName("CompanyloginButton")).Click();
            Console.WriteLine("请手动完成拖拽验证码...");
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(600));
            wait.Until(d => d.Url == "https://ent.qpgzf.cn/CompanyHome/Main");
            Console.WriteLine("登录成功！");
        }

        private void NavigateToSelection()
        {
            _driver.Navigate().GoToUrl("https://ent.qpgzf.cn/RoomAssign/Index");
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            _driver.FindElement(By.CssSelector($"input[name='{_applyerName}']")).Click();
        }

        private void WaitUntilStartTime()
        {
            var start = DateTime.ParseExact(_startTime, "yyyy-MM-dd HH:mm:ss", null);
            while (true)
            {
                if (_cancellationToken.IsCancellationRequested) return;
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

        private void EnterSelectionPage()
        {
            Console.WriteLine("等待选房开始，尝试进入选房页面...");
            while (true)
            {
                try
                {
                    var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(2));
                    var btn = wait.Until(ExpectedConditions.ElementExists(By.CssSelector("a[onclick='assignRoom(1)']")));
                    Thread.Sleep(50);
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
                    Thread.Sleep(_clickIntervalMs);
                    var iframe = wait.Until(ExpectedConditions.ElementExists(By.Id("iframeDialog")));
                    if (iframe.GetAttribute("src").Contains("ApplyIDs"))
                    {
                        Console.WriteLine("成功进入选房页面！");
                        break;
                    }
                    Console.WriteLine("当前时间段无法分配房源，关闭弹窗后继续尝试...");
                    var closeBtn = _driver.FindElement(By.ClassName("ui-dialog-titlebar-close"));
                    Thread.Sleep(50);
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", closeBtn);
                    Thread.Sleep(_clickIntervalMs);
                }
                catch (Exception)
                {
                    try
                    {
                        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(2));
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
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            var iframe = wait.Until(ExpectedConditions.ElementExists(By.Id("iframeDialog")));
            _driver.SwitchTo().Frame(iframe);
        }

        private void SwitchBack()
        {
            _driver.SwitchTo().DefaultContent();
        }

        private void SearchCommunity(string community)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var input = wait.Until(ExpectedConditions.ElementExists(By.Id("SearchEntity__CommonSearchCondition")));
            input.Clear();
            input.SendKeys(community);
            var btn = wait.Until(ExpectedConditions.ElementExists(By.Id("submitButton")));
            Thread.Sleep(50);
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        }

        private bool TryFindAndSelectHouse(HouseCondition condition)
        {
            // 临时禁用隐式等待，快速判断行数
            var originalWait = _driver.Manage().Timeouts().ImplicitWait;
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

            var waitTable = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            var table = waitTable.Until(ExpectedConditions.ElementExists(By.Id("common-table")));

            var rows = _driver.FindElements(By.XPath("//table[@id='common-table']/tbody/tr"));

            _driver.Manage().Timeouts().ImplicitWait = originalWait;

            if (rows.Count == 0) return false;

            IWebElement? bestMatch = null;
            IWebElement? floorMatch = null;
            IWebElement? firstOption = null;

            var comm = string.Empty;
            var bNo = 0;
            var fText = string.Empty;
            double price = 0;
            double area = 0;
            var typeDesc = string.Empty;

            foreach (var row in rows)
            {
                try
                {
                    comm = row.FindElement(By.XPath("./td[2]")).Text.Trim();
                    bNo = int.Parse(row.FindElement(By.XPath("./td[3]")).Text.Trim());
                    fText = row.FindElement(By.XPath("./td[4]")).Text.Trim();
                    var fNo = int.Parse(fText[..Math.Min(2, fText.Length)]);
                    price = double.Parse(row.FindElement(By.XPath("./td[6]")).Text.Trim());
                    area = double.Parse(row.FindElement(By.XPath("./td[8]")).Text.Trim());
                    typeDesc = row.FindElement(By.XPath("./td[9]")).Text.Trim();
                    var type = EnumHelper.GetEnumValueFromDescription<HouseType>(typeDesc);
                    var selectBtn = row.FindElement(By.XPath("./td[1]//a"));

                    firstOption ??= selectBtn;

                    if (comm == condition.CommunityName
                        && type == condition.HouseType
                        && HouseCondition.FilterEqual(bNo, condition.BuildingNo)
                        && HouseCondition.FilterFloor(fNo, condition.FloorRange)
                        && HouseCondition.FilterPrice(price, condition.MaxPrice)
                        && HouseCondition.FilterArea(area, condition.LeastArea))
                    {
                        bestMatch = selectBtn;
                        break;
                    }

                    if (comm == condition.CommunityName
                        && type == condition.HouseType
                        && HouseCondition.FilterFloor(fNo, condition.FloorRange))
                    {
                        floorMatch = selectBtn;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析房源信息时出错: {ex.Message}");
                    continue;
                }
            }

            var toClick = bestMatch ?? floorMatch ?? firstOption;

            if (toClick == null) return false;
            
            try
            {
                Thread.Sleep(50);
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", toClick);
                Console.WriteLine($"已选择匹配房源：名字: {comm}, " +
                                    $"幢号: {bNo}, " +
                                    $"楼层: {fText}, " +
                                    $"价格: {price}, " +
                                    $"面积: {area}, " +
                                    $"类型: {typeDesc}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"点击选择房源时出错: {ex.Message}");
                return false;
            }
        }

        private void ConfirmSelection()
        {
            SwitchBack();
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            var btn = wait.Until(ExpectedConditions.ElementExists(By.XPath("//button/span[text()='确定']")));
            Thread.Sleep(50);
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        }

        private void FinalConfirm()
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
            var btn = wait.Until(ExpectedConditions.ElementExists(By.XPath("//button/span[text()='最终确认']")));
            Thread.Sleep(50);
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
        }

        private bool CheckSuccess()
        {
            try
            {
                var dialog = _driver.FindElement(By.Id("sysConfirm"));
                var msg = dialog.Text;
                if (msg.Contains("此房间已经被其他申请人选中"))
                {
                    var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(2));
                    var btn = wait.Until(ExpectedConditions.ElementExists(By.XPath("//button/span[text()='确定']")));
                    Thread.Sleep(50);
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", btn);
                    return false;
                }
            }
            catch
            {
                // 未找到提示框，视为成功
            }
            return true;
        }

        public void Stop()
        {
            _driver.Quit();
        }
        
    }
} 