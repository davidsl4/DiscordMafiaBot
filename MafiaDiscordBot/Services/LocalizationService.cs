using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
            foreach (var _localization in localizations)
            {
                var localization = _localization;
                tasks.Add(Task.Run(async () =>
                {
                    string
                        flag = localization["flag"],
                        name = localization["name"],
                        file = localization["file"];

                    if (flag == null || name == null || file == null)
                    {
                        Log.Error("Unable to load language from section {section}, one or more of the parameters are missing", localization.Key);
                        return;
                    }

                    if (!File.Exists(file))
                    {
                        Log.Error("Unable to load language {language} from section {section}, file wasn't found", name, localization.Key);
                        return;
                    }

                    JObject translations = JObject.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false));

                    Language language = new Language
                    {
                        Flag = flag,
                        Name = name,
                        Translations = translations
                    };

                    _localizations.TryAdd(localization.Key, language);
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public string GetLocalizedString(string key) => _localizations.TryGetValue("_", out Language lang) ? lang.Translations[key]?.Value<string>() : null;
        public string GetLocalizedString(string language, string key)
        {
            if (_localizations.TryGetValue(language, out Language lang))
                return lang.Translations[key]?.Value<string>() ?? GetLocalizedString(key);
            return GetLocalizedString(key);
        }
    }
}
