using System;
using JiraToSlides.Config;

using JiraApi = Atlassian.Jira;

namespace JiraToSlides.Jira
{
    public static class Authentication
    {
        public static JiraApi.Jira JiraClient 
        { 
            get {
                return _jira.Value;
            }
        }

        private static readonly Lazy<JiraApi.Jira> _jira = new Lazy<JiraApi.Jira>(() => {
            var jira = JiraApi.Jira.CreateRestClient(Configuration.JiraUrl, Configuration.JiraUsername, Configuration.JiraApiKey);
            jira.Issues.MaxIssuesPerRequest = 100;
            return jira;
        });
    }
}