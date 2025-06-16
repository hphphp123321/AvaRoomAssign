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
            if (string.IsNullOrWhiteSpace(_userAccount) || string.IsNullOrWhiteSpace(_userPassword) ||
                string.IsNullOrWhiteSpace(_cookie))
            {
                LogManager.Error("用户名、密码和cookie不能为空");
                return;
            }

            await Login();
            NavigateToSelection();
            await WaitUntilStartTime();
            if (_cancellationToken.IsCancellationRequested)
            {
                LogManager.Warning("操作已取消，退出选房流程");
                return;
            }

            LogManager.Success("选房开始！");
            EnterSelectionPage();
            SwitchToIframe();
            
            var success = false;
            var searchSuccessCount = 0;
            foreach (var condition in _communityList)
            {
                LogManager.Info($"正在搜索 {condition.CommunityName}...");
                success = SearchAndSelectRoomAsync(condition);
                LogManager.Info($"开始选择 {condition.CommunityName} 的房源...");
                
                if (!success)
                {
                    LogManager.Warning($"志愿 {condition.CommunityName} 未中签，继续下一个志愿...");
                    searchSuccessCount++;
                    continue;
                }

                ConfirmSelection();

                if (!_autoConfirm)
                {
                    LogManager.Info("请手动进行最终确认");
                    await Task.Delay(30000, _cancellationToken);
                    break;
                }

                // 成功选房，退出循环
                success = true;
                break;
            }

            if (!success)
            {
                LogManager.Info("所有志愿均未中签，流程结束");
            }

            // 无限等待或根据需要退出
            await Task.Delay(-1, _cancellationToken);
        }

        private async Task Login()
        {
            if (TryLoginWithCookie())
            {
                LogManager.Success("使用 Cookie 登录成功");
                return;
            }
            else
            {
                LogManager.Warning("使用 Cookie 登录失败，尝试手动登录");
            }

            var loginUrl = "https://ent.qpgzf.cn/Account/Login";
            _driver.Navigate().GoToUrl(loginUrl);

            await Task.Delay(3000, _cancellationToken);

            LogManager.Info("请手动完成拖拽验证码...");
            await Task.Delay(10000, _cancellationToken);
            
            LogManager.Success("登录成功！");
        }

        private void NavigateToSelection()
        {
            _driver.Navigate().GoToUrl("https://ent.qpgzf.cn/RoomAssign/Index");
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            _driver.FindElement(By.CssSelector($"input[name='{_applyerName}']")).Click();
        }

        private async Task WaitUntilStartTime()
        {
            var start = DateTime.ParseExact(_startTime, "yyyy-MM-dd HH:mm:ss", null);
            var now = DateTime.Now;
            var timeUntilStart = start - now;

            while (timeUntilStart.TotalSeconds > 0 && !_cancellationToken.IsCancellationRequested)
            {
                if (timeUntilStart.TotalSeconds <= 1)
                {
                    LogManager.Success($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                    break;
                }
                LogManager.Info($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {timeUntilStart}");

                await Task.Delay(1000, _cancellationToken);
                
                now = DateTime.Now;
                timeUntilStart = start - now;
            }
        }

        private void EnterSelectionPage()
        {
            LogManager.Info("等待选房开始，尝试进入选房页面...");
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
                        LogManager.Success("成功进入选房页面！");
                        break;
                    }
                    LogManager.Warning("当前时间段无法分配房源，关闭弹窗后继续尝试...");
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
                        LogManager.Success("成功进入选房页面！");
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
                    LogManager.Error($"解析房源信息时出错: {ex.Message}");
                    continue;
                }
            }

            var toClick = bestMatch ?? floorMatch ?? firstOption;

            if (toClick == null) return false;
            
            try
            {
                Thread.Sleep(50);
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", toClick);
                LogManager.Success($"已选择匹配房源：名字: {comm}, " +
                                    $"幢号: {bNo}, " +
                                    $"楼层: {fText}, " +
                                    $"价格: {price}, " +
                                    $"面积: {area}, " +
                                    $"类型: {typeDesc}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error($"点击选择房源时出错: {ex.Message}");
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
        
        private bool TryLoginWithCookie()
        {
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
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private bool SearchAndSelectRoomAsync(HouseCondition condition)
        {
            SearchCommunity(condition.CommunityName);
            return TryFindAndSelectHouse(condition);
        }
    }
} 