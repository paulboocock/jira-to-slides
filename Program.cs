using System;
using System.Collections.Generic;
using System.Linq;
using JiraToSlides.GoogleDrive;
using JiraToSlides.Config;
using JiraToSlides.Jira;

namespace JiraToSlides
{
    class Program
    {
        static void Main(string[] args)
        {
            var googleDrive = new DriveService();
            var googleSlides = new SlidesService();
            var jira = new JiraService();

            Console.WriteLine($"Retrieving {Configuration.JiraProjectId} Tasks for {Configuration.JiraSprintName}...");
            var issues = jira.GetIssuesForConfiguration();
            jira.PrintIssues(issues);

            Console.WriteLine($"Creating presentation from template ({Configuration.TemplatePresentationId})...");
            var presentationId = googleDrive.Duplicate(Configuration.TemplatePresentationId, $"{DateTime.UtcNow.ToString("s").Split("T")[0]} {Configuration.JiraProjectId} End of sprint");
            Console.WriteLine($"Created presentation from template. The new presentation ID is: {Configuration.TemplatePresentationId}");

            Console.WriteLine("Calculating required slides...");
            var presentation = googleSlides.GetPresentation(presentationId);
            var slideToDuplicateObjectId = presentation.Slides[4].ObjectId;

            var requiredDuplicates = issues.Count() / 3;
            var additionalDuplicate = issues.Count() % 3;

            while (googleSlides.DuplicationRequestCount < requiredDuplicates) 
            {
                googleSlides.QueueDuplicationRequest(slideToDuplicateObjectId);
            }

            if (additionalDuplicate > 0) 
            {
                googleSlides.QueueDuplicationRequest(presentation.Slides[additionalDuplicate + 1].ObjectId, "SLIDES_THEEXTRAONE");
                googleSlides.QueueUpdatePositionRequest("SLIDES_THEEXTRAONE", requiredDuplicates + 6);
           }

            foreach (var slide in presentation.Slides.Skip(2).Take(3))
            {
                googleSlides.QueueDeleteRequest(slide.ObjectId);
            }

            Console.WriteLine($"Creating duplicate slides...");
            var executionResponse = googleSlides.ExecuteQueue(presentationId);

            Console.WriteLine($"Created new duplicate slides:");
            executionResponse.DuplicatedObjectIds.ForEach(Console.WriteLine);

            Console.WriteLine($"Updating presentation placeholders...");
            executionResponse.DuplicatedObjectIds.Reverse();
            
            if (additionalDuplicate > 0) 
            {
                var additionalSlideId = executionResponse.DuplicatedObjectIds.First();
                executionResponse.DuplicatedObjectIds.Remove(additionalSlideId);
                executionResponse.DuplicatedObjectIds.Add(additionalSlideId);
            }

            var epicKeys = issues.Where(i => !string.IsNullOrEmpty(i.ParentIssueKey)).Select(i => i.ParentIssueKey).Distinct().ToList();
            var epics = jira.GetIssuesForKeys(epicKeys);

            googleSlides.QueueTextReplacementRequest("{epics}", string.Join('\n', epics.Select(e => e.Summary).OrderBy(s => s)));

            int i = 1, j = 0;
            foreach(var issue in issues.OrderBy(i => i.ParentIssueKey == null).ThenBy(i => epics.FirstOrDefault(e => e.Key == i.ParentIssueKey)?.Summary)) 
            {
                googleSlides.QueueTextReplacementRequest($"{{story-{i}}}", issue.Summary, executionResponse.DuplicatedObjectIds.ElementAt(j));
                googleSlides.QueueTextReplacementRequest($"{{progress-{i}}}", JiraService.ProgressMap(issue.Status), executionResponse.DuplicatedObjectIds.ElementAt(j));
                googleSlides.QueueTextReplacementRequest($"{{status-{i}}}", JiraService.StatusMap(issue.Status), executionResponse.DuplicatedObjectIds.ElementAt(j));

                if (++i > 3) {
                    ++j;
                    i = 1;
                }
            }

            var textReplaceResponse = googleSlides.ExecuteQueue(presentationId);
            Console.WriteLine($"Replaced {textReplaceResponse.ReplaceTextOccurences} text placeholders");
            Console.WriteLine($"Presentaton Created: https://docs.google.com/presentation/d/{presentationId}/edit");
        }
    }
}