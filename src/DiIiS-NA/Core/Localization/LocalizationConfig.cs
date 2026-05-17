namespace DiIiS_NA.Core.Localization
{
    public sealed class LocalizationConfig : DiIiS_NA.Core.Config.Config
    {
        public static readonly LocalizationConfig Instance = new();

        public string Language => GetString(nameof(Language), "en-US");

        private LocalizationConfig() : base("Localization")
        {
        }
    }
}
