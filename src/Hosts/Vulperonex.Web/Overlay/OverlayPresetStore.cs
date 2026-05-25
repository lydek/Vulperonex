using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Vulperonex.Web.Overlay;

public sealed class OverlayPresetStore
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly Regex SlugPattern = new("^[a-z0-9-]{1,64}$", RegexOptions.Compiled);
    private static readonly string[] AllowedExtensions = [".html", ".htm", ".css", ".js", ".json", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".woff2"];

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<OverlayPresetStore> logger;

    public OverlayPresetStore(IWebHostEnvironment environment, ILogger<OverlayPresetStore> logger)
    {
        this.environment = environment;
        this.logger = logger;

        try
        {
            var customRoot = GetCustomRootPath();
            if (Directory.Exists(customRoot))
            {
                foreach (var dir in Directory.GetDirectories(customRoot, ".upload-*"))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to clean up orphan upload directory: {Path}", dir);
                    }
                }

                foreach (var slugDir in Directory.GetDirectories(customRoot))
                {
                    string[] targets = ["production.new", "draft.new"];
                    foreach (var target in targets)
                    {
                        var targetPath = Path.Combine(slugDir, target);
                        if (Directory.Exists(targetPath))
                        {
                            try
                            {
                                Directory.Delete(targetPath, recursive: true);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to clean up orphan temp directory: {Path}", targetPath);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Orphan upload directory cleanup janitor failed.");
        }
    }


    private static readonly IReadOnlyList<OverlayPresetDescriptor> BuiltInPresets =
    [
        new("chat", "vulperonex-default", "builtin", "Vulperonex default", "/overlay/chat"),
        new("chat", "compact-line", "builtin", "Compact line", "/overlay/chat"),
        new("chat", "member-card-inline", "builtin", "Member card inline", "/overlay/chat"),
        new("member", "rotan-checkin", "builtin", "Rotan checkin", "/overlay/member"),
        new("alerts", "vulperonex-alerts", "builtin", "Vulperonex alerts", "/overlay/alerts"),
    ];

    public IReadOnlyList<OverlayPresetDescriptor> GetBuiltIns() => BuiltInPresets;

    public string GetSettingKeyForHub(string hubName) => hubName switch
    {
        "chat" => Vulperonex.Application.Settings.SystemSettingKey.OverlayChatPreset,
        "member" => Vulperonex.Application.Settings.SystemSettingKey.OverlayMemberPreset,
        "alerts" => Vulperonex.Application.Settings.SystemSettingKey.OverlayAlertsPreset,
        _ => throw new ArgumentOutOfRangeException(nameof(hubName)),
    };

    public bool IsSupportedHub(string hubName) =>
        string.Equals(hubName, "chat", StringComparison.Ordinal)
        || string.Equals(hubName, "member", StringComparison.Ordinal)
        || string.Equals(hubName, "alerts", StringComparison.Ordinal);

    public bool IsValidSlug(string slug) => SlugPattern.IsMatch(slug);

    public string? ResolveCustomRelativePath(string slug)
    {
        if (!IsValidSlug(slug))
        {
            return null;
        }

        var directory = Path.Combine(GetCustomRootPath(), slug);
        var indexPath = Path.Combine(directory, "index.html");
        return File.Exists(indexPath) ? $"/overlay/custom/{slug}/index.html" : null;
    }

    public async Task<OverlayCustomPresetMetadata> SaveAsync(string slug, IFormFile file, CancellationToken cancellationToken)
    {
        if (!IsValidSlug(slug))
        {
            throw new ArgumentException("Invalid slug.", nameof(slug));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".html" and not ".zip")
        {
            throw new InvalidDataException("Unsupported preset package format.");
        }

        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }

        var customRoot = GetCustomRootPath();
        Directory.CreateDirectory(customRoot);
        var tempRoot = Path.Combine(customRoot, $".upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            if (extension == ".html")
            {
                if (file.Length > 2 * 1024 * 1024)
                {
                    throw new InvalidDataException("HTML file size cannot exceed 2MB.");
                }
                await SaveHtmlAsync(tempRoot, file, cancellationToken);
            }
            else
            {
                if (file.Length > 10 * 1024 * 1024)
                {
                    throw new InvalidDataException("ZIP file size cannot exceed 10MB.");
                }
                await SaveZipAsync(tempRoot, file, cancellationToken);
            }

            var indexPath = Path.Combine(tempRoot, "index.html");
            if (!File.Exists(indexPath))
            {
                throw new InvalidDataException("Custom preset package must contain index.html at archive root.");
            }

            var destination = Path.Combine(customRoot, slug);
            var productionDir = Path.Combine(destination, "production");
            var draftDir = Path.Combine(destination, "draft");
            
            var historyStamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var historyDir = Path.Combine(destination, "history", historyStamp);

            if (Directory.Exists(draftDir)) Directory.Delete(draftDir, recursive: true);
            Directory.CreateDirectory(draftDir);
            CopyDirectory(tempRoot, draftDir);

            // Auto validate draft
            var issues = await ValidateDraftAsync(slug).ConfigureAwait(false);
            var blockingIssues = issues
                .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (blockingIssues.Length == 0)
            {
                // Atomic Deploy only if there are no validation errors
                await DeployDraftInternalAsync(slug).ConfigureAwait(false);
            }
            else
            {
                logger.LogWarning("Uploaded preset '{Slug}' contains validation errors and was not auto-deployed to production.", slug);
            }

            Directory.Delete(tempRoot, recursive: true);

            return BuildMetadata(slug, productionDir);
        }
        catch
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }

            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public IReadOnlyList<OverlayCustomPresetMetadata> ListCustom()
    {
        var customRoot = GetCustomRootPath();
        if (!Directory.Exists(customRoot))
        {
            return [];
        }

        return Directory.GetDirectories(customRoot)
            .Select(Path.GetFileName)
            .Where(name => name != null && IsValidSlug(name))
            .Select(slug => BuildMetadata(slug!, Path.Combine(customRoot, slug!)))
            .OrderBy(entry => entry.Slug, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlug(slug))
        {
            return false;
        }

        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }

        try
        {
            var directory = Path.Combine(GetCustomRootPath(), slug);
            if (!Directory.Exists(directory))
            {
                return false;
            }

            Directory.Delete(directory, recursive: true);
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public IReadOnlyList<OverlayPresetDescriptor> ListAll()
    {
        var custom = ListCustom()
            .SelectMany(entry => new[]
            {
                new OverlayPresetDescriptor("chat", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/production/index.html"),
                new OverlayPresetDescriptor("member", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/production/index.html"),
                new OverlayPresetDescriptor("alerts", $"custom:{entry.Slug}", "custom", entry.Slug, $"/overlay/custom/{entry.Slug}/production/index.html"),
            })
            .ToArray();

        return [.. BuiltInPresets, .. custom];
    }

    public string GetCustomRootPath() => Path.Combine(environment.WebRootPath, "overlay", "custom");

    // --- Draft CRUD API ---
    public Task<IReadOnlyList<OverlayFileDescriptor>> ListDraftFilesAsync(string slug)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));

        var draftDir = Path.Combine(GetCustomRootPath(), slug, "draft");
        if (!Directory.Exists(draftDir))
        {
            return Task.FromResult<IReadOnlyList<OverlayFileDescriptor>>([]);
        }

        var files = Directory.GetFiles(draftDir, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relPath = Path.GetRelativePath(draftDir, path).Replace(Path.DirectorySeparatorChar, '/');
                var info = new FileInfo(path);
                return new OverlayFileDescriptor(relPath, info.Length, new DateTimeOffset(info.LastWriteTimeUtc));
            })
            .OrderBy(f => f.RelativePath)
            .ToArray();

        return Task.FromResult<IReadOnlyList<OverlayFileDescriptor>>(files);
    }

    public async Task<string> ReadDraftFileAsync(string slug, string relativePath)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));
        
        var safePath = GetSafePath(slug, "draft", relativePath);
        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException("Draft file not found.", relativePath);
        }

        return await File.ReadAllTextAsync(safePath);
    }

    public async Task WriteDraftFileAsync(string slug, string relativePath, string content)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));

        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }
        try
        {
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(content);
            if (byteCount > 2 * 1024 * 1024)
            {
                throw new InvalidDataException("File size cannot exceed 2MB.");
            }

            var safePath = GetSafePath(slug, "draft", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(safePath)!);

            // Aggregate size limit validation (10MB)
            var draftDir = Path.Combine(GetCustomRootPath(), slug, "draft");
            long currentTotal = 0;
            if (Directory.Exists(draftDir))
            {
                var files = Directory.GetFiles(draftDir, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(safePath), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    currentTotal += new FileInfo(file).Length;
                }
            }

            if (currentTotal + byteCount > 10 * 1024 * 1024)
            {
                throw new InvalidDataException("Total preset draft size cannot exceed 10MB.");
            }

            await File.WriteAllTextAsync(safePath, content);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task DeleteDraftFileAsync(string slug, string relativePath)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));

        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }
        try
        {
            var safePath = GetSafePath(slug, "draft", relativePath);
            if (File.Exists(safePath))
            {
                File.Delete(safePath);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    // --- Deploy / Rollback / History ---
    public async Task DeployDraftAsync(string slug)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));
        
        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }

        try
        {
            await DeployDraftInternalAsync(slug);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task DeployDraftInternalAsync(string slug)
    {
        var validationIssues = await ValidateDraftAsync(slug).ConfigureAwait(false);
        var blockingIssues = validationIssues
            .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (blockingIssues.Length > 0)
        {
            throw new InvalidOperationException($"Draft validation failed: {string.Join(" | ", blockingIssues.Select(issue => issue.Message))}");
        }

        var destination = Path.Combine(GetCustomRootPath(), slug);
        var productionDir = Path.Combine(destination, "production");
        var draftDir = Path.Combine(destination, "draft");

        if (!Directory.Exists(draftDir))
        {
            throw new InvalidOperationException("Draft directory does not exist.");
        }

        var historyStamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var historyDir = Path.Combine(destination, "history", historyStamp);

        // Copy to temp directory first for atomic deployment
        var tempProdDir = Path.Combine(destination, "production.new");
        if (Directory.Exists(tempProdDir)) Directory.Delete(tempProdDir, recursive: true);
        Directory.CreateDirectory(tempProdDir);

        CopyDirectory(draftDir, tempProdDir);
        Directory.CreateDirectory(historyDir);
        CopyDirectory(draftDir, historyDir);

        // Atomic rename/move switch
        var prodOldDir = Path.Combine(destination, "production.old");
        if (Directory.Exists(prodOldDir)) Directory.Delete(prodOldDir, recursive: true);

        if (Directory.Exists(productionDir))
        {
            Directory.Move(productionDir, prodOldDir);
        }

        try
        {
            Directory.Move(tempProdDir, productionDir);
        }
        catch
        {
            // Rollback rename if switching failed
            if (Directory.Exists(prodOldDir))
            {
                Directory.Move(prodOldDir, productionDir);
            }
            throw;
        }

        if (Directory.Exists(prodOldDir)) Directory.Delete(prodOldDir, recursive: true);

        // Backwards compatibility: copy index.html to custom slug root
        var rootIndex = Path.Combine(destination, "index.html");
        var draftIndex = Path.Combine(draftDir, "index.html");
        if (File.Exists(draftIndex))
        {
            File.Copy(draftIndex, rootIndex, overwrite: true);
        }

        // Prune old history: keep last 10 versions
        var historyParentDir = Path.Combine(destination, "history");
        if (Directory.Exists(historyParentDir))
        {
            var historySubDirs = Directory.GetDirectories(historyParentDir)
                .Select(dir => new { Path = dir, Name = Path.GetFileName(dir) })
                .Where(x => x.Name != null && x.Name.Length == 14 && Regex.IsMatch(x.Name, "^[0-9]{14}$"))
                .OrderByDescending(x => x.Name)
                .ToList();

            if (historySubDirs.Count > 10)
            {
                for (int i = 10; i < historySubDirs.Count; i++)
                {
                    try
                    {
                        Directory.Delete(historySubDirs[i].Path, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to prune history directory: {Path}", historySubDirs[i].Path);
                    }
                }
            }
        }
    }

    public async Task<IReadOnlyList<OverlayValidationIssue>> ValidateDraftAsync(string slug)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));

        var draftDir = Path.Combine(GetCustomRootPath(), slug, "draft");
        var issues = new List<OverlayValidationIssue>();
        if (!Directory.Exists(draftDir))
        {
            issues.Add(new OverlayValidationIssue("error", "draft_missing", "Draft directory does not exist.", null, null));
            return issues;
        }

        if (!File.Exists(Path.Combine(draftDir, "index.html")))
        {
            issues.Add(new OverlayValidationIssue("error", "missing_index", "Draft preset must contain index.html.", "index.html", null));
        }

        foreach (var filePath in Directory.GetFiles(draftDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(draftDir, filePath).Replace(Path.DirectorySeparatorChar, '/');
            if (!ShouldValidateAsText(relativePath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath);
            issues.AddRange(ValidateFileContent(relativePath, content));
        }

        return issues;
    }

    public Task<IReadOnlyList<OverlayHistoryVersion>> ListHistoryVersionsAsync(string slug)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));

        var historyDir = Path.Combine(GetCustomRootPath(), slug, "history");
        if (!Directory.Exists(historyDir))
        {
            return Task.FromResult<IReadOnlyList<OverlayHistoryVersion>>([]);
        }

        var versions = Directory.GetDirectories(historyDir)
            .Select(Path.GetFileName)
            .Where(name => name != null && name.Length == 14) // yyyyMMddHHmmss
            .Select(stamp =>
            {
                DateTimeOffset createdAt;
                try
                {
                    createdAt = DateTimeOffset.ParseExact(stamp!, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal);
                }
                catch
                {
                    createdAt = DateTimeOffset.UtcNow;
                }
                return new OverlayHistoryVersion(stamp!, createdAt);
            })
            .OrderByDescending(v => v.VersionStamp)
            .ToArray();

        return Task.FromResult<IReadOnlyList<OverlayHistoryVersion>>(versions);
    }

    public async Task RollbackToVersionAsync(string slug, string versionStamp)
    {
        if (!IsValidSlug(slug)) throw new ArgumentException("Invalid slug.", nameof(slug));
        if (!Regex.IsMatch(versionStamp, "^[0-9]{14}$"))
        {
            throw new ArgumentException("Invalid version stamp.", nameof(versionStamp));
        }

        var semaphore = _locks.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another operation is already in progress for this preset.");
        }
        try
        {
            var destination = Path.Combine(GetCustomRootPath(), slug);
            var productionDir = Path.Combine(destination, "production");
            var draftDir = Path.Combine(destination, "draft");
            var historyDir = Path.Combine(destination, "history", versionStamp);

            if (!Directory.Exists(historyDir))
            {
                throw new FileNotFoundException($"History version '{versionStamp}' not found.");
            }

            // Atomic switch for production during rollback too!
            var tempProdDir = Path.Combine(destination, "production.new");
            if (Directory.Exists(tempProdDir)) Directory.Delete(tempProdDir, recursive: true);
            Directory.CreateDirectory(tempProdDir);
            CopyDirectory(historyDir, tempProdDir);

            var tempDraftDir = Path.Combine(destination, "draft.new");
            if (Directory.Exists(tempDraftDir)) Directory.Delete(tempDraftDir, recursive: true);
            Directory.CreateDirectory(tempDraftDir);
            CopyDirectory(historyDir, tempDraftDir);

            // Atomic rename/move switch for production
            var prodOldDir = Path.Combine(destination, "production.old");
            if (Directory.Exists(prodOldDir)) Directory.Delete(prodOldDir, recursive: true);

            if (Directory.Exists(productionDir))
            {
                Directory.Move(productionDir, prodOldDir);
            }

            try
            {
                Directory.Move(tempProdDir, productionDir);
            }
            catch
            {
                if (Directory.Exists(prodOldDir))
                {
                    Directory.Move(prodOldDir, productionDir);
                }
                throw;
            }

            if (Directory.Exists(prodOldDir)) Directory.Delete(prodOldDir, recursive: true);

            // Atomic rename/move switch for draft
            var draftOldDir = Path.Combine(destination, "draft.old");
            if (Directory.Exists(draftOldDir)) Directory.Delete(draftOldDir, recursive: true);

            if (Directory.Exists(draftDir))
            {
                Directory.Move(draftDir, draftOldDir);
            }

            try
            {
                Directory.Move(tempDraftDir, draftDir);
            }
            catch
            {
                if (Directory.Exists(draftOldDir))
                {
                    Directory.Move(draftOldDir, draftDir);
                }
                throw;
            }

            if (Directory.Exists(draftOldDir)) Directory.Delete(draftOldDir, recursive: true);

            // Backwards compatibility: copy index.html to custom slug root
            var rootIndex = Path.Combine(destination, "index.html");
            var historyIndex = Path.Combine(historyDir, "index.html");
            if (File.Exists(historyIndex))
            {
                File.Copy(historyIndex, rootIndex, overwrite: true);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    // --- Helpers & Validation Gate ---
    private string GetSafePath(string slug, string subDir, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
        {
            throw new InvalidOperationException("Path traversal detected.");
        }

        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        if (!string.IsNullOrEmpty(fileName))
        {
            var upperName = fileName.ToUpperInvariant();
            string[] reservedNames = ["CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
            if (reservedNames.Contains(upperName))
            {
                throw new InvalidOperationException("Reserved path name detected.");
            }
        }

        var baseDir = Path.GetFullPath(Path.Combine(GetCustomRootPath(), slug, subDir));
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var baseDirWithSeparator = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(baseDirWithSeparator, comparison) && fullPath != baseDir)
        {
            throw new InvalidOperationException("Path traversal detected.");
        }

        return fullPath;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileInfo = new FileInfo(file);
            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                continue; // Skip symlinks/junctions
            }

            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirInfo = new DirectoryInfo(subDir);
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                continue; // Skip symlinks/junctions
            }

            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    private IReadOnlyList<OverlayValidationIssue> ValidateFileContent(string relativePath, string content)
    {
        var issues = new List<OverlayValidationIssue>();
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (extension == ".html")
        {
            try
            {
                var parser = new AngleSharp.Html.Parser.HtmlParser();
                var document = parser.ParseDocument(content);
            }
            catch (AngleSharp.Html.Parser.HtmlParseException ex)
            {
                logger.LogWarning(ex, "HTML parsing failed (HtmlParseException) for file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "html_parse_error", "Failed to parse HTML file structure.", relativePath, null));
            }
            catch (AngleSharp.Dom.DomException ex)
            {
                logger.LogWarning(ex, "DOM exception while parsing HTML file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "html_parse_error", "Failed to parse HTML file structure.", relativePath, null));
            }
        }
        else if (extension == ".css")
        {
            try
            {
                var parser = new ExCSS.StylesheetParser();
                _ = parser.Parse(content);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                logger.LogWarning(ex, "CSS parsing failed (ArgumentOutOfRangeException) for file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "css_parse_error", "Failed to parse CSS stylesheet structure.", relativePath, null));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "CSS parsing failed (ArgumentException) for file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "css_parse_error", "Failed to parse CSS stylesheet structure.", relativePath, null));
            }
            catch (NullReferenceException ex)
            {
                logger.LogWarning(ex, "CSS parsing failed (NullReferenceException) for file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "css_parse_error", "Failed to parse CSS stylesheet structure.", relativePath, null));
            }
        }
        else if (extension == ".js")
        {
            try
            {
                var parser = new Acornima.Parser();
                parser.ParseScript(content);
            }
            catch (Acornima.ParseErrorException ex)
            {
                logger.LogWarning(ex, "JS parsing failed (ParseErrorException) for file: {Path}", relativePath);
                issues.Add(new OverlayValidationIssue("error", "js_parse_error", "Failed to parse JavaScript code structure.", relativePath, null));
            }
        }

        // Security Analysis
        if (extension is ".html" or ".js")
        {
            // SignalR Connection & Hub URL Check
            var hubUrlMatch = Regex.Match(content, @"\.withUrl\s*\(\s*['""]([^'""]+)['""]");
            if (hubUrlMatch.Success)
            {
                var hubUrl = hubUrlMatch.Groups[1].Value.Trim().ToLowerInvariant();
                var allowedHubs = new[] { "/hub/chat", "hub/chat", "/hub/member", "hub/member", "/hub/alerts", "hub/alerts", "/hub/widgets", "hub/widgets" };
                if (!allowedHubs.Any(allowed => hubUrl == allowed || hubUrl.StartsWith(allowed + "?")))
                {
                    issues.Add(new OverlayValidationIssue(
                        "error",
                        "disallowed_hub_url",
                        $"Disallowed SignalR hub URL referenced: '{hubUrlMatch.Groups[1].Value}'. Only local overlay hubs are allowed.",
                        relativePath,
                        null));
                }
            }

            // Sensitive API /api/ fetch prevention
            var apiMatch = Regex.Match(content, @"fetch\s*\(\s*['""]([^'""]*\/api\/[^'""]+)['""]|['""]([^'""]*\/api\/[^'""]+)['""]");
            if (apiMatch.Success)
            {
                issues.Add(new OverlayValidationIssue(
                    "error",
                    "unauthorized_api_call",
                    "Custom presets are not permitted to fetch administrative APIs.",
                    relativePath,
                    null));
            }

            // External URLs Warning
            var httpMatches = Regex.Matches(content, @"https?://[^\s'""`]+");
            foreach (Match match in httpMatches)
            {
                issues.Add(new OverlayValidationIssue(
                    "warning",
                    "external_url_reference",
                    $"External URL reference detected: '{match.Value}'. Avoid external dependencies for offline reliability.",
                    relativePath,
                    null));
            }
        }

        return issues;
    }

    private static bool ShouldValidateAsText(string relativePath)
    {
        return Path.GetExtension(relativePath).ToLowerInvariant() is ".html" or ".htm" or ".css" or ".js";
    }

    private static OverlayCustomPresetMetadata BuildMetadata(string slug, string directory)
    {
        var productionPath = Path.Combine(directory, "production");
        var targetDir = Directory.Exists(productionPath) ? productionPath : directory;

        var files = Directory.Exists(targetDir)
            ? Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories)
            : [];
        var sizeBytes = files.Sum(path => new FileInfo(path).Length);
        var uploadedAt = files.Length == 0
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(files.Max(path => File.GetLastWriteTimeUtc(path)));

        return new OverlayCustomPresetMetadata(slug, sizeBytes, uploadedAt);
    }

    private static async Task SaveHtmlAsync(string tempRoot, IFormFile file, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(tempRoot, "index.html");
        await using var target = File.Create(targetPath);
        await using var source = file.OpenReadStream();
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task SaveZipAsync(string tempRoot, IFormFile file, CancellationToken cancellationToken)
    {
        var rootPath = Path.GetFullPath(tempRoot);
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        await using var source = file.OpenReadStream();
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);

        long extractedBytes = 0;
        const long MaxExtractedBytes = 10 * 1024 * 1024; // 10MB unzipped limit

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var extension = Path.GetExtension(entry.FullName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension) || entry.FullName.EndsWith("web.config", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsupported file extension in package: '{entry.FullName}'.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(tempRoot, entry.FullName));
            if (!destinationPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                logger.LogWarning("Rejected custom preset zip entry outside root: {EntryName}", entry.FullName);
                throw new InvalidDataException("Zip entry escapes the destination root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var entryStream = entry.Open();
            await using var destination = File.Create(destinationPath);

            var buffer = new byte[8192];
            int bytesRead;
            long entryBytes = 0;
            while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                extractedBytes += bytesRead;
                entryBytes += bytesRead;
                if (extractedBytes > MaxExtractedBytes)
                {
                    throw new InvalidDataException("Expanded preset package exceeds the 10MB upload limit.");
                }
                if (entryBytes > 2 * 1024 * 1024)
                {
                    throw new InvalidDataException("Any single file in preset package cannot exceed 2MB.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public sealed record OverlayCustomPresetMetadata(
    string Slug,
    long SizeBytes,
    DateTimeOffset UploadedAt);

public sealed record OverlayPresetDescriptor(
    string HubName,
    string Key,
    string Kind,
    string Label,
    string RelativeUrl);

public sealed record OverlayFileDescriptor(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedAt);

public sealed record OverlayHistoryVersion(
    string VersionStamp,
    DateTimeOffset CreatedAt);

public sealed record OverlayValidationIssue(
    string Severity,
    string Code,
    string Message,
    string? FilePath,
    int? Line);
