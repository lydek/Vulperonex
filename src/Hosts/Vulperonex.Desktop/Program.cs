using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Photino.NET;
using Vulperonex.Web;
using Vulperonex.Web.Logging;
using Vulperonex.Web.Ports;

internal static class Program
{
    private static Mutex _mutex = null!;
    private static CancellationTokenSource _cts = new();
    private static int _crashCount = 0;
    private const int MaxCrashCount = 3;
    private static WebApplication? _currentApp;

    [STAThread]
    private static void Main(string[] args)
    {
        // 1. NamedMutex ?桀祕靘皜?        _mutex = new Mutex(true, "Global\\Vulperonex.Desktop.Mutex", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Vulperonex 撌脣?瑁?銝准?, "Vulperonex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _mutex.Dispose();
            return;
        }

        try
        {
            RunDesktop(args);
        }
        finally
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
    }

    private static void RunDesktop(string[] args)
    {

        // 2. WebView2 蝻箏仃?菜葫
        if (!CheckWebView2Installed())
        {
            var result = MessageBox.Show(
                "?芸皜砍蝟餌絞摰? Microsoft Edge WebView2 Runtime?n暺??Ⅱ摰???摰雯?脰?銝?摰??n銝????嚗ttps://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "WebView2 蝻箏仃",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Error);
            if (result == DialogResult.OK)
            {
                Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            }
            return;
        }

        // 3. Port Allocation
        var probe = new SocketPortAvailabilityProbe();
        var allocator = new PortPairAllocator(probe);
        var ports = allocator.TryAllocate();
        if (ports == null)
        {
            MessageBox.Show("?⊥??????????????航撌脰◤雿??, "Vulperonex", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // 4. Initialize Photino Window
        var window = new PhotinoWindow()
            .SetTitle("Vulperonex ?批??)
            .SetSize(1200, 800)
            .Center();

        // 5. Setup IPC Bridge DTO ?詨捆??        window.RegisterWebMessageReceivedHandler((_, message) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var type = doc.RootElement.GetProperty("type").GetString();
                var payload = doc.RootElement.GetProperty("payload");

                var reply = JsonSerializer.Serialize(new
                {
                    type = $"{type}_reply",
                    payload = "ACK"
                });
                window.SendWebMessage(reply);
            }
            catch
            {
                // Ignore illegal formats
            }
        });

        // 6. Background Web Host lifecycle loop
        var webHostTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var webOptions = new WebApplicationOptions
                    {
                        Args = args,
                        ContentRootPath = AppContext.BaseDirectory,
                        WebRootPath = ResolveDesktopWebRootPath(),
                    };
                    var builder = VulperonexWebApplication.CreateBuilder(webOptions, configureDefaultLoopbackPorts: false);

                    builder.WebHost.ConfigureKestrel(kestrelOptions =>
                    {
                        kestrelOptions.Listen(IPAddress.Loopback, ports.ApiPort);
                        kestrelOptions.Listen(IPAddress.IPv6Loopback, ports.ApiPort);
                        kestrelOptions.Listen(IPAddress.Loopback, ports.OverlayPort);
                        kestrelOptions.Listen(IPAddress.IPv6Loopback, ports.OverlayPort);
                    });

                    _currentApp = VulperonexWebApplication.Build(builder);
                    await _currentApp.RunAsync(_cts.Token);
                    break; // Normal shutdown, break loop
                }
                catch (Exception ex)
                {
                    _crashCount++;
                    if (_crashCount > MaxCrashCount)
                    {
                        // Load Fallback HTML in Photino
                        window.LoadRawString(GetFallbackHtml());
                        break;
                    }

                    // On migration failure or boot error (except cancellation)
                    if (!_cts.Token.IsCancellationRequested && _crashCount == 1)
                    {
                        var result = MessageBox.Show(
                            $"???????澈?瑞宏憭望?嚗n?航炊??嚗ex.Message}\n暺??Ⅱ摰????亥?鞈?憭暸脰????,
                            "??憭望?",
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Error);
                        if (result == DialogResult.OK)
                        {
                            var logDir = SerilogConfigurator.ResolveLogDirectory();
                            Process.Start(new ProcessStartInfo("explorer.exe", logDir));
                        }
                    }

                    await Task.Delay(1000);
                }
            }
        });

        // Load SPA url
        window.Load(new Uri($"http://localhost:{ports.ApiPort}/"));

        // Keep photino running
        window.WaitForClose();

        // 7. Cleanup
        _cts.Cancel();
        if (_currentApp != null)
        {
            try { _currentApp.StopAsync().GetAwaiter().GetResult(); } catch { }
        }
        try { webHostTask.Wait(TimeSpan.FromSeconds(5)); } catch { }
    }

    private static bool CheckWebView2Installed()
    {
        try
        {
            var registryPaths = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
            };

            foreach (var path in registryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path)
                                ?? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path);
                if (key != null)
                {
                    var pv = key.GetValue("pv");
                    if (pv != null && pv.ToString() != "0.0.0.0")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string GetFallbackHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="zh-TW">
        <head>
            <meta charset="utf-8" />
            <title>Vulperonex ???啣虜</title>
            <style>
                body {
                    font-family: 'Segoe UI', Microsoft JhengHei, sans-serif;
                    margin: 48px;
                    color: #1f2937;
                    background-color: #f9fafb;
                }
                .error-container {
                    max-width: 600px;
                    margin: 0 auto;
                    padding: 24px;
                    border: 1px solid #fecaca;
                    background-color: #fef2f2;
                    border-radius: 8px;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);
                }
                h1 {
                    color: #dc2626;
                    font-size: 22px;
                    margin-top: 0;
                    border-bottom: 1px solid #fca5a5;
                    padding-bottom: 10px;
                }
                p {
                    font-size: 15px;
                    line-height: 1.6;
                }
            </style>
        </head>
        <body>
            <div class="error-container">
                <h1>?? 憭活???憭望?</h1>
                <p>??憭活??憭望?嚗?????? Vulperonex ????/p>
            </div>
        </body>
        </html>
        """;
    }

    private static string ResolveDesktopWebRootPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Hosts", "Vulperonex.Web", "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
}
