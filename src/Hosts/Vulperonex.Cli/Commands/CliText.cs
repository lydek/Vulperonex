using System.Globalization;
using System.Reflection;
using System.Text.Json;

internal static class CliText
{
    private const string DefaultCulture = "en-US";
    private const string ManifestRelativePath = "Resources/I18n/manifest.json";
    private static readonly Lazy<I18nCatalog> Catalog = new(LoadCatalog);

    public static string Get(string key)
    {
        var catalog = Catalog.Value;
        var language = ResolveLanguage(catalog);
        if (catalog.Translations.TryGetValue(language, out var translations)
            && translations.TryGetValue(key, out var value))
        {
            return value;
        }

        return catalog.Translations[catalog.DefaultCulture].GetValueOrDefault(key, key);
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private static string ResolveLanguage(I18nCatalog catalog)
    {
        var requested = Environment.GetEnvironmentVariable("VULPERONEX_CLI_LANG");
        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = CultureInfo.CurrentUICulture.Name;
        }

        if (catalog.Translations.ContainsKey(requested))
        {
            return requested;
        }

        var neutralMatch = catalog.SupportedCultures.FirstOrDefault(culture =>
            culture.StartsWith(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)
            || culture.StartsWith(requested.Split('-')[0], StringComparison.OrdinalIgnoreCase));
        return neutralMatch ?? catalog.DefaultCulture;
    }

    private static I18nCatalog LoadCatalog()
    {
        var resourceRoot = LocateResourceRoot();
        var manifestPath = Path.Combine(resourceRoot, ManifestRelativePath);
        using var manifestStream = File.OpenRead(manifestPath);
        var manifest = JsonSerializer.Deserialize<I18nManifest>(manifestStream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("CLI i18n manifest is invalid.");

        var defaultCulture = string.IsNullOrWhiteSpace(manifest.DefaultCulture)
            ? DefaultCulture
            : manifest.DefaultCulture;
        var supportedCultures = manifest.SupportedCultures.Length == 0
            ? [defaultCulture]
            : manifest.SupportedCultures;
        if (!supportedCultures.Contains(defaultCulture, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("CLI i18n manifest must include the default culture.");
        }

        var translations = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var culture in supportedCultures)
        {
            var fileName = $"{culture}.json";
            var filePath = Path.Combine(Path.GetDirectoryName(manifestPath)!, fileName);
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"CLI i18n file is missing: {fileName}");
            }

            using var stream = File.OpenRead(filePath);
            translations[culture] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException($"CLI i18n file is invalid: {fileName}");
        }

        return new I18nCatalog(defaultCulture, supportedCultures, translations);
    }

    private static string LocateResourceRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var directPath = Path.Combine(baseDirectory, ManifestRelativePath);
        if (File.Exists(directPath))
        {
            return baseDirectory;
        }

        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyPath)
            && File.Exists(Path.Combine(assemblyPath, ManifestRelativePath)))
        {
            return assemblyPath;
        }

        return baseDirectory;
    }

    private sealed record I18nManifest(string DefaultCulture, string[] SupportedCultures);

    private sealed record I18nCatalog(
        string DefaultCulture,
        IReadOnlyList<string> SupportedCultures,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Translations);
}
