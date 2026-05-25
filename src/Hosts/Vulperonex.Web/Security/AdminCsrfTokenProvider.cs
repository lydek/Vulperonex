using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Infrastructure.Security;

namespace Vulperonex.Web.Security;

    /// <summary>
    /// 提供管理端點的 CSRF 安全權杖單例服務。
    /// 
    /// 【安全架構設計與權衡 (Security Design &amp; Trade-offs)】：
    /// 1. Token 輪轉 (Token Rotation)：
    ///    每次應用程式重啟皆會自動產生強隨機 CSRF 權杖 (以 Cryptographic Random Base64Url 產出)，並於結束時自我銷毀。
    ///    這將使所有先前已開啟的管理端點瀏覽器分頁 (Browser Tabs) 於服務重啟後的首次變更請求 (Mutating Request) 被拒絕，
    ///    使用者必須重新整理頁面以重新取得新 Session 的 Token。對於本機 Admin Loopback 單機應用程式而言，這是合理的安全權衡。
    /// 
    /// 2. 本機跨程序信任 (Local Process Trust Trade-off)：
    ///    取得 `/api/overlay/csrf-token` 被限縮在 IP Loopback 及 Host 允許清單中。
    ///    這意謂著本機上其他運行的程序 (如 OneComme bridge, 瀏覽器其他分頁, compromised node_modules 腳本等)
    ///    在發起 loopback 請求時亦有機會讀取此 Token。這是身為單機案頭軟體 (Desktop/Admin App) 基於無中心化使用者認證
    ///    之下的已知安全設計妥協，允許本機程序進行一定程度的跨程序信任。
    /// </summary>
    public sealed class AdminCsrfTokenProvider : IDisposable
    {
    private readonly string _tokenPath;
    private readonly ILogger<AdminCsrfTokenProvider> _logger;

    public string Token { get; }

    public AdminCsrfTokenProvider(
        IWebHostEnvironment environment,
        IFileSystem fileSystem,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<AdminCsrfTokenProvider> logger)
    {
        _logger = logger;
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        Token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes);

        try
        {
            var configuredPath = configuration["Security:CsrfTokenPath"];
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                _tokenPath = configuredPath;
                var dir = Path.GetDirectoryName(_tokenPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            else
            {
                _tokenPath = Path.Combine(environment.ContentRootPath, ".admin-csrf-token");
            }
            
            // 寫入隨機 Token 並套用 user-only 權限 (ACL)
            File.WriteAllText(_tokenPath, Token);
            fileSystem.ApplyUserOnlyPermissions(_tokenPath);
            
            _logger.LogInformation("Successfully generated per-session CSRF token and secured ACL at: {Path}", _tokenPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to secure per-session CSRF token file.");
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tokenPath))
            {
                File.Delete(_tokenPath);
                _logger.LogInformation("Successfully cleaned up session CSRF token file: {Path}", _tokenPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up session CSRF token file: {Path}", _tokenPath);
        }
    }
}
