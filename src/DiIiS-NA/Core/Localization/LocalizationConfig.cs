using DiIiS_NA.Core.Config;

namespace DiIiS_NA.Core.Localization
{
    public sealed class LocalizationConfig : Config
    {
        public static readonly LocalizationConfig Instance = new();

        public string Language => GetString(nameof(Language), "en-US");

        private LocalizationConfig() : base("Localization")
        {
            Set(nameof(Language), Language);
            Save();
        }
    }
}
