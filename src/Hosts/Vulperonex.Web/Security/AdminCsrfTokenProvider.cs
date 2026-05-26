using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Infrastructure.Security;

namespace Vulperonex.Web.Security;

    /// <summary>
    /// Singleton service that provides a secure CSRF token for the administration endpoints.
    /// 
    /// [Security Design &amp; Trade-offs]:
    /// 1. Token Rotation:
    ///    A strong random CSRF token (produced in Cryptographic Random Base64Url) is automatically generated upon every application restart and self-destructs on termination.
    ///    This will cause all previously opened admin browser tabs to be rejected on their first mutating request after a service restart.
    ///    Users must refresh their pages to obtain the new session's token. This is a reasonable security trade-off for local admin loopback desktop applications.
    /// 
    /// 2. Local Process Trust Trade-off:
    ///    Accessing `/api/overlay/csrf-token` is restricted to IP Loopback and Host allowlist.
    ///    This means other running local processes (such as the OneComme bridge, other browser tabs, compromised node_modules scripts, etc.)
    ///    may also have the opportunity to read this token when initiating a loopback request. This is a known security compromise based on
    ///    the absence of centralized user authentication for a single-machine desktop/admin application, allowing a certain level of cross-process trust.
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
            
            // Write random token and apply user-only permissions (ACL)
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
