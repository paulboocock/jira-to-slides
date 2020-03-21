using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace JiraToSlides.Config
{
    public static class Configuration
    {
        public static string JiraUrl => _configuration.Value["JiraUrl"];
        public static string JiraUsername => _configuration.Value["JiraUsername"];
        public static string JiraApiKey => _configuration.Value["JiraApiKey"];
        public static string JiraSprintName => _configuration.Value["JiraSprintName"];
        public static string JiraProjectId => _configuration.Value["JiraProjectId"];
        public static string TemplatePresentationId => _configuration.Value["TemplatePresentationId"];
        public static string ApplicationName => _configuration.Value["ApplicationName"];

        private static readonly Lazy<Dictionary<string, string>> _configuration = new Lazy<Dictionary<string, string>>(() => {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("config.json"));
        });
    }
}
