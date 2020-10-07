using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MafiaDiscordBot.Services
{
    class LocalizationService
    {
        class Language
        {
            public string Flag { get; set; }
            public string Name { get; set; }
            public JObject Translations { get; set; }
        }

        private readonly ConcurrentDictionary<string, Language> _localizations;
        private readonly IConfigurationSection _localizationsSection;

        public LocalizationService(IServiceProvider services)
        {
            _localizations = new ConcurrentDictionary<string, Language>();
            _localizationsSection = services.GetRequiredService<IConfigurationRoot>().GetSection("localizations");
        }

        public async Task LoadAllAsync()
        {
            HashSet<Task> tasks = new HashSet<Task>();
            var localizations = _localizationsSection.GetChildren();
            DirectoryInfo executableDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            foreach (var _localization in localizations)
            {
                var localization = _localization;
                tasks.Add(Task.Run(async () =>
                {
                    string
                        flag = localization["flag"],
                        name = localization["name"];
                    var patterns = localization.GetSection("files")?.GetChildren();

                    if (flag == null || name == null || patterns == null)
                    {
                        Log.Error("Unable to load language from section {section}, one or more of the parameters are missing", localization.Key);
                        return;
                    }

                    Language language = null;
                    foreach (var pattern in patterns)
                    {
                        foreach (var file in executableDirectory.GetFiles(pattern.Value))
                        {
                            JObject translations = JObject.Parse(await File.ReadAllTextAsync(file.FullName).ConfigureAwait(false));
                            language ??= new Language
                            {
                                Flag = flag,
                                Name = name,
                                Translations = null
                            };
                            if (language.Translations == null)
                                language.Translations = translations;
                            else
                                foreach (var translation in translations)
                                    language.Translations.Add(translation.Key, translation.Value);
                        }
                    }

                    if (language != null)
                        _localizations.TryAdd(localization.Key, language);
                    else
                        Log.Error("No translations added for language {language} ({language_key}) with the specified patterns",
                            name, localization.Key);
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public string GetLocalizedString(string key) => _localizations.TryGetValue("_", out var lang) ? lang.Translations[key]?.Value<string>() : null;
        public string GetLocalizedString(string language, string key)
        {
            if (language != null && _localizations.TryGetValue(language, out var lang))
                return lang.Translations[key]?.Value<string>() ?? GetLocalizedString(key);
            return GetLocalizedString(key);
        }
        public string GetLocalizedString(Models.Database.Guild forGuild, string key) =>
            GetLocalizedString(forGuild?.Localization, key);

        public (string, string) GetLocalizedString(string language, string key1, string key2) =>
            (GetLocalizedString(language, key1), GetLocalizedString(language, key2));
        public (string, string, string) GetLocalizedString(string language, string key1, string key2, string key3) => (
            GetLocalizedString(language, key1), GetLocalizedString(language, key2), GetLocalizedString(language, key3));
        public (string, string, string, string)
            GetLocalizedString(string language, string key1, string key2, string key3, string key4) => (
            GetLocalizedString(language, key1), GetLocalizedString(language, key2), GetLocalizedString(language, key3),
            GetLocalizedString(language, key4));
        public (string, string, string, string, string) GetLocalizedString(string language, string key1, string key2,
            string key3, string key4, string key5) => (GetLocalizedString(language, key1),
            GetLocalizedString(language, key2), GetLocalizedString(language, key3), GetLocalizedString(language, key4),
            GetLocalizedString(language, key5));
        public (string, string, string, string, string, string) GetLocalizedString(string language, string key1,
            string key2, string key3, string key4, string key5, string key6) => (GetLocalizedString(language, key1),
            GetLocalizedString(language, key2), GetLocalizedString(language, key3), GetLocalizedString(language, key4),
            GetLocalizedString(language, key5), GetLocalizedString(language, key6));
        public (string, string, string, string, string, string, string) GetLocalizedString(string language, string key1,
            string key2, string key3, string key4, string key5, string key6, string key7) => (
            GetLocalizedString(language, key1), GetLocalizedString(language, key2), GetLocalizedString(language, key3),
            GetLocalizedString(language, key4), GetLocalizedString(language, key5), GetLocalizedString(language, key6),
            GetLocalizedString(language, key7));
        public (string, string, string, string, string, string, string, string) GetLocalizedString(string language,
            string key1, string key2, string key3, string key4, string key5, string key6, string key7, string key8) => (
            GetLocalizedString(language, key1), GetLocalizedString(language, key2), GetLocalizedString(language, key3),
            GetLocalizedString(language, key4), GetLocalizedString(language, key5), GetLocalizedString(language, key6),
            GetLocalizedString(language, key7), GetLocalizedString(language, key8));

        public (string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2) => GetLocalizedString(forGuild?.Localization, key1, key2);
        public (string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3) => GetLocalizedString(forGuild?.Localization, key1, key2, key3);
        public (string, string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3, string key4) => GetLocalizedString(forGuild?.Localization, key1, key2, key3, key4);
        public (string, string, string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3, string key4, string key5) => GetLocalizedString(forGuild?.Localization, key1, key2, key3, key4, key5);
        public (string, string, string, string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3, string key4, string key5, string key6) => GetLocalizedString(forGuild?.Localization, key1, key2, key3, key4, key5, key6);
        public (string, string, string, string, string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3, string key4, string key5, string key6, string key7) => GetLocalizedString(forGuild?.Localization, key1, key2, key3, key4, key5, key6, key7);
        public (string, string, string, string, string, string, string, string) GetLocalizedString(Models.Database.Guild forGuild, string key1, string key2, string key3, string key4, string key5, string key6, string key7, string key8) => GetLocalizedString(forGuild?.Localization, key1, key2, key3, key4, key5, key6, key7, key8);
        
        public (string, string) GetGeneralString(string key1, string key2) => GetLocalizedString((string)null, key1, key2);
        public (string, string, string) GetGeneralString(string key1, string key2, string key3) => GetLocalizedString((string)null, key1, key2, key3);
        public (string, string, string, string) GetGeneralString(string key1, string key2, string key3, string key4) => GetLocalizedString((string)null, key1, key2, key3, key4);
        public (string, string, string, string, string) GetGeneralString(string key1, string key2, string key3, string key4, string key5) => GetLocalizedString((string)null, key1, key2, key3, key4, key5);
        public (string, string, string, string, string, string) GetGeneralString(string key1, string key2, string key3, string key4, string key5, string key6) => GetLocalizedString((string)null, key1, key2, key3, key4, key5, key6);
        public (string, string, string, string, string, string, string) GetGeneralString(string key1, string key2, string key3, string key4, string key5, string key6, string key7) => GetLocalizedString((string)null, key1, key2, key3, key4, key5, key6, key7);
        public (string, string, string, string, string, string, string, string) GetGeneralString(string key1, string key2, string key3, string key4, string key5, string key6, string key7, string key8) => GetLocalizedString((string)null, key1, key2, key3, key4, key5, key6, key7, key8);
    }
}
