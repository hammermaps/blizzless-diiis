using System;
using System.Collections.Generic;
using System.Text;

namespace DiIiS_NA.REST
{
    public sealed class RestConfig : Core.Config.Config
    {
        public string IP
        {
            get => GetString("IP", "127.0.0.1");
            set => Set("IP", value);
        }

        public bool Public
        {
            get => GetBoolean("Public", false);
            set => Set("Public", value);
        }

        public string PublicIP
        {
            get => GetString("PublicIP", "0.0.0.0");
            set => Set("PublicIP", value);
        }

        public int Port
        {
            get => GetInt("PORT", 8081);
            set => Set("PORT", value);
        } //8081

        /// <summary>
        /// API key required for protected endpoints (e.g. POST /api/v1/command).
        /// Set this to a strong secret value in config.ini.
        /// Leave empty to disable protected endpoints.
        /// </summary>
        public string ApiKey
        {
            get => GetString("ApiKey", "");
            set => Set("ApiKey", value);
        }

        public static RestConfig Instance { get; } = new();

        private RestConfig() : base("REST")
        {
        }
    }
}
