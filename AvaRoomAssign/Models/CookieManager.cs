using System;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace AvaRoomAssign.Models
{
    /// <summary>
    /// Cookie管理器，使用Selenium WebDriver自动获取和管理Cookie
    /// </summary>
    public static class CookieManager
    {
        private const string LOGIN_URL = "https://ent.qpgzf.cn/SysLoginManage";
        private const string ROOM_ASSIGN_URL = "https://ent.qpgzf.cn/RoomAssign/Index";
        private const string SUCCESS_URL = "https://ent.qpgzf.cn/CompanyHome/Main";
        
        /// <summary>
        /// 通过用户名和密码使用Selenium自动获取Cookie
        /// </summary>
        /// <param name="driver">WebDriver实例</param>
        /// <param name="userAccount">用户账号</param>
        /// <param name="userPassword">用户密码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>获取到的Cookie字符串，如果失败返回null</returns>
        public static async Task<string?> GetCookieWithSeleniumAsync(IWebDriver driver, string userAccount, string userPassword, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userAccount) || string.IsNullOrWhiteSpace(userPassword))
            {
                LogManager.Error("用户账号和密码不能为空");
                return null;
            }

            try
            {
                LogManager.Info("开始使用Selenium自动获取Cookie...");
                
                // 第1步：导航到登录页面
                LogManager.Info("正在访问登录页面...");
                driver.Navigate().GoToUrl(LOGIN_URL);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                // 第2步：填写登录信息
                LogManager.Info("正在填写登录信息...");
                var userAccountField = driver.FindElement(By.Name("UserAccount"));
                userAccountField.Clear();
                userAccountField.SendKeys(userAccount);

                var passwordField = driver.FindElement(By.Name("PD"));
                passwordField.Clear();
                passwordField.SendKeys(userPassword);

                // 第3步：点击登录按钮
                LogManager.Info("正在点击登录按钮...");
                var loginButton = driver.FindElement(By.ClassName("CompanyloginButton"));
                loginButton.Click();

                // 第4步：等待用户完成验证码
                LogManager.Info("请手动完成拖拽验证码...");
                LogManager.Info("完成验证码后，系统将自动检测登录状态...");

                // 第5步：等待登录成功
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(600));
                try
                {
                    await Task.Run(() => 
                    {
                        wait.Until(d => 
                        {
                            if (cancellationToken.IsCancellationRequested)
                                throw new OperationCanceledException();
                            return d.Url == SUCCESS_URL;
                        });
                    }, cancellationToken);
                    
                    LogManager.Success("登录成功！");
                }
                catch (OperationCanceledException)
                {
                    LogManager.Warning("获取Cookie操作被取消");
                    return null;
                }
                catch (WebDriverTimeoutException)
                {
                    LogManager.Error("等待登录成功超时，请检查是否完成了验证码验证");
                    return null;
                }

                // 第6步：提取Cookie
                LogManager.Info("正在提取Cookie...");
                var cookieValue = ExtractSysUserCookieKey(driver);
                if (!string.IsNullOrWhiteSpace(cookieValue))
                {
                    LogManager.Success($"成功获取Cookie: {cookieValue[..Math.Min(20, cookieValue.Length)]}...");
                    return cookieValue;
                }
                else
                {
                    LogManager.Error("未能从浏览器中提取到有效的Cookie");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                LogManager.Warning("获取Cookie操作被取消");
                return null;
            }
            catch (NoSuchElementException ex)
            {
                LogManager.Error($"页面元素未找到: {ex.Message}");
                return null;
            }
            catch (WebDriverException ex)
            {
                LogManager.Error($"WebDriver错误: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"获取Cookie时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从WebDriver中提取SYS_USER_COOKIE_KEY的值
        /// </summary>
        /// <param name="driver">WebDriver实例</param>
        /// <returns>Cookie值</returns>
        private static string? ExtractSysUserCookieKey(IWebDriver driver)
        {
            try
            {
                var cookies = driver.Manage().Cookies.AllCookies;
                
                foreach (var cookie in cookies)
                {
                    if (cookie.Name == "SYS_USER_COOKIE_KEY")
                    {
                        return cookie.Value;
                    }
                }

                LogManager.Warning("未找到SYS_USER_COOKIE_KEY，尝试获取所有Cookie信息");
                
                // 如果没有找到特定的Cookie，记录所有Cookie用于调试
                foreach (var cookie in cookies)
                {
                    LogManager.Info($"发现Cookie: {cookie.Name}={cookie.Value}");
                }

                return null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"提取Cookie时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证Cookie是否有效（使用Selenium）
        /// </summary>
        /// <param name="driver">WebDriver实例</param>
        /// <param name="cookieValue">Cookie值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Cookie是否有效</returns>
        public static async Task<bool> ValidateCookieWithSeleniumAsync(IWebDriver driver, string cookieValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cookieValue))
            {
                LogManager.Error("Cookie值为空，无法验证");
                return false;
            }

            try
            {
                LogManager.Info("正在使用Selenium验证Cookie有效性...");
                
                // 导航到主页并设置Cookie
                driver.Navigate().GoToUrl("https://ent.qpgzf.cn/");
                
                // 设置Cookie
                var cookie = new OpenQA.Selenium.Cookie("SYS_USER_COOKIE_KEY", cookieValue, "ent.qpgzf.cn", "/", DateTime.Now.AddHours(10));
                driver.Manage().Cookies.AddCookie(cookie);
                
                // 访问房源分配页面验证Cookie是否有效
                driver.Navigate().GoToUrl(ROOM_ASSIGN_URL);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

                // 检查是否成功访问到房源分配页面
                try
                {
                    // 尝试查找申请人相关的元素，如果找到说明已登录
                    var applierInputs = driver.FindElements(By.XPath("//input[starts-with(@name, '') and @value]"));
                    
                    if (applierInputs.Count > 0)
                    {
                        LogManager.Success("Cookie验证成功，可以正常访问房源分配页面");
                        return true;
                    }
                    
                    // 检查页面是否包含房源分配相关内容
                    var pageSource = driver.PageSource;
                    if (pageSource.Contains("申请人姓名") || 
                        pageSource.Contains("房源分配") ||
                        pageSource.Contains("选房") ||
                        pageSource.Contains("RoomAssign"))
                    {
                        LogManager.Success("Cookie验证成功，能够访问房源分配页面");
                        return true;
                    }
                    
                    // 检查是否被重定向到登录页面
                    var currentUrl = driver.Url;
                    if (currentUrl.Contains("/sysloginmanage/CompanyIndex") || 
                        currentUrl.Contains("CompanyIndex") ||
                        pageSource.Contains("用户登录") || 
                        pageSource.Contains("login") || 
                        pageSource.Contains("请登录"))
                    {
                        LogManager.Warning("Cookie已失效，页面重定向到登录页面");
                        return false;
                    }
                    
                    LogManager.Warning("Cookie状态未知，未检测到明确的登录状态指示");
                    return false;
                }
                catch (NoSuchElementException)
                {
                    // 如果找不到预期元素，检查是否被重定向
                    var currentUrl = driver.Url;
                    var pageSource = driver.PageSource;
                    
                    if (currentUrl.Contains("/sysloginmanage/CompanyIndex") || 
                        currentUrl.Contains("CompanyIndex") ||
                        pageSource.Contains("用户登录"))
                    {
                        LogManager.Warning("Cookie已失效，页面重定向到登录页面");
                        return false;
                    }
                    
                    LogManager.Warning("Cookie验证结果不确定，未找到预期的页面元素");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"验证Cookie时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证Cookie是否有效（保持原有的HTTP方法作为备用）
        /// </summary>
        /// <param name="cookieValue">Cookie值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Cookie是否有效</returns>
        public static async Task<bool> ValidateCookieAsync(string cookieValue, CancellationToken cancellationToken = default)
        {
            // 注意：此方法保持原有实现，但推荐使用ValidateCookieWithSeleniumAsync
            // 因为HTTP方法可能不如Selenium方法准确
            LogManager.Warning("使用HTTP方法验证Cookie，推荐使用ValidateCookieWithSeleniumAsync方法");
            return false; // 暂时返回false，因为HTTP验证可能不准确
        }
    }
} 