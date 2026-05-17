using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using DiIiS_NA.Core.Helpers.IO;
using DiIiS_NA.Core.Logging;

namespace DiIiS_NA.Core.Localization
{
    public static class Locale
    {
        private static readonly Logger Logger = LogManager.CreateLogger(nameof(Locale));
        private static readonly object SyncRoot = new();
        private static Dictionary<string, string> _texts;
        private static string _loadedLanguage;

        public static string Language => LocalizationConfig.Instance.Language;

        public static void Reload()
        {
            lock (SyncRoot)
            {
                _texts = LoadLanguage(Language);
                _loadedLanguage = Language;
            }
        }

        public static string T(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            EnsureLoaded();
            var template = _texts != null && _texts.TryGetValue(key, out var localized) && !string.IsNullOrEmpty(localized)
                ? localized
                : key;

            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private static void EnsureLoaded()
        {
            if (_texts != null && string.Equals(_loadedLanguage, Language, StringComparison.OrdinalIgnoreCase))
                return;

            Reload();
        }

        private static Dictionary<string, string> LoadLanguage(string language)
        {
            var fallback = LoadFile(Path.Combine(FileHelpers.AssemblyRoot, "Localization", "en-US.json"));
            if (string.IsNullOrWhiteSpace(language) || language.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                return fallback;

            var localized = LoadFile(Path.Combine(FileHelpers.AssemblyRoot, "Localization", $"{language}.json"));
            foreach (var pair in localized)
                fallback[pair.Key] = pair.Value;

            return fallback;
        }

        private static Dictionary<string, string> LoadFile(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                    return new Dictionary<string, string>();

                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fileName))
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load localization file '{fileName}': {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
    }
}
