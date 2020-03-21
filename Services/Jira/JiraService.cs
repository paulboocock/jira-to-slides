using System;
using System.Linq;
using System.Collections.Generic;
using Atlassian.Jira;
using JiraToSlides.Config;

namespace JiraToSlides.Jira
{
    public class JiraService
    {
        public List<Issue> GetIssuesForConfiguration()
        {
            var jira = Authentication.JiraClient;
            var issues = (from issue in jira.Issues.Queryable
                            where issue.Project == new LiteralMatch(Configuration.JiraProjectId) 
                            && issue["Sprint"] == new LiteralMatch(Configuration.JiraSprintName)
                            select issue).ToList();

            return issues.OrderByDescending(i => i.ParentIssueKey).ToList();
        }

        public List<Issue> GetIssuesForKeys(List<string> keys)
        {
            var jira = Authentication.JiraClient;
            return jira.Issues.GetIssuesAsync(keys).Result.Select(i => i.Value).ToList();
        }

        public void PrintIssues(IEnumerable<Issue> issues) 
        {
            Console.WriteLine(issues.Count() + " issues");

            foreach(var issue in issues)
            {
                Console.WriteLine(issue.Summary + " / " + issue.Status);
            }
        }

        public static string ProgressMap(IssueStatus status) 
        {
            return status.Name.ToLower() switch
            {
                "code-complete" => "Complete",
                "done" => "Complete",
                "to do" => "Not Started",
                _ => status.Name,
            };
        }

        public static string StatusMap(IssueStatus status) 
        {
            return status.Name.ToLower() switch
            {
                "code-complete" => "Awaiting Review/Release",
                "done" => "Released",
                _ => string.Empty,
            };
        }
    }
}