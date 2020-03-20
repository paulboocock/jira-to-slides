using Google.Apis.Auth.OAuth2;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Google.Apis.Drive.v3;
using System.Linq;
using Atlassian.Jira;

namespace JiraToSlides
{
    class Program
    {
        static readonly List<string> Scopes = new List<string> { SlidesService.Scope.PresentationsReadonly, DriveService.Scope.Drive };

        static void Main(string[] args)
        {
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("config.json"));

            using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);
            // The file token.json stores the user's access and refresh tokens, and is created
            // automatically when the authorization flow completes for the first time.
            var credPath = "token.json";
            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
            Console.WriteLine("Credential file saved to: " + credPath);

            var baseClientService = new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Jira To Slides",
            };

            using var driveService = new DriveService(baseClientService);
            var copyMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = $"{DateTime.UtcNow.ToString("s").Split("T")[0]} {config["JiraProjectId"]} End of sprint"
            };

            using var slidesService = new SlidesService(baseClientService);
            Console.WriteLine($"Creating presentation from template ID: {config["TemplatePresentationId"]}");
            var presentationId = driveService.Files.Copy(copyMetadata, config["TemplatePresentationId"]).Execute().Id;
            Console.WriteLine($"Created presentation from template. The new presentation ID is: {presentationId}");

            var jira = Jira.CreateRestClient(config["JiraUrl"], config["JiraUsername"], config["JiraApiKey"]);
            jira.Issues.MaxIssuesPerRequest = 100;
            var issuesQueryable = from issue in jira.Issues.Queryable
                                    where issue.Project == new LiteralMatch(config["JiraProjectId"]) && issue["Sprint"] == new LiteralMatch(config["JiraSprintName"])
                                    select issue;

            var issues = issuesQueryable.ToList().OrderByDescending(i => i.ParentIssueKey);
            Console.WriteLine(issues.Count() + " issues");

            foreach(var issue in issues)
            {
                Console.WriteLine(issue.Summary + " / " + issue.Status);
            }

            var requiredDuplicates = issues.Count() / 3;
            var additionalDuplicate = issues.Count() % 3;
            var presentation = slidesService.Presentations.Get(presentationId).Execute();
            var slideToDuplicateObjectId = presentation.Slides[4].ObjectId;
            var createWorkDoneSlides = new BatchUpdatePresentationRequest 
            {
                Requests = new int[requiredDuplicates].Select((i) => new Request {
                    DuplicateObject = new DuplicateObjectRequest { ObjectId = slideToDuplicateObjectId }
                }).ToList()
            };

            if (additionalDuplicate > 0) 
            {
                createWorkDoneSlides.Requests.Add(new Request { DuplicateObject = new DuplicateObjectRequest { ObjectId = presentation.Slides[additionalDuplicate + 1].ObjectId, ObjectIds = new Dictionary<string, string> { { presentation.Slides[additionalDuplicate + 1].ObjectId, "SLIDES_THEEXTRAONE" } } } });
                createWorkDoneSlides.Requests.Add(new Request { UpdateSlidesPosition = new UpdateSlidesPositionRequest { SlideObjectIds = new List<string> { "SLIDES_THEEXTRAONE" }, InsertionIndex = requiredDuplicates + 6 } });
            }

            foreach (var slide in presentation.Slides.Skip(2).Take(3))
            {
                createWorkDoneSlides.Requests.Add(new Request { DeleteObject = new DeleteObjectRequest { ObjectId = slide.ObjectId }});
            }

            var slideDuplicateResponse = slidesService.Presentations.BatchUpdate(createWorkDoneSlides, presentationId).Execute();
            var storySlideObjectIds = new List<string>();
            foreach (var reply in slideDuplicateResponse.Replies.Where(r => r.DuplicateObject != null))
            {
                storySlideObjectIds.Add(reply.DuplicateObject.ObjectId);
                Console.WriteLine($"New duplicate slide ID: {reply.DuplicateObject.ObjectId}");
            }

            storySlideObjectIds.Reverse();
            
            if (additionalDuplicate > 0) 
            {
                var additionalSlideId = storySlideObjectIds.First();
                storySlideObjectIds.Remove(additionalSlideId);
                storySlideObjectIds.Add(additionalSlideId);
            }

            var epicKeys = issues.Where(i => !string.IsNullOrEmpty(i.ParentIssueKey)).Select(i => i.ParentIssueKey).Distinct().ToList();
            var epics = jira.Issues.GetIssuesAsync(epicKeys).Result;

            var replaceTextRequest = new BatchUpdatePresentationRequest 
            {
                Requests = new List<Request> 
                {
                    new Request 
                    {
                        ReplaceAllText = new ReplaceAllTextRequest 
                        { 
                            ContainsText = new SubstringMatchCriteria 
                            {
                                Text = "{epics}",
                                MatchCase = true
                            },
                            ReplaceText = string.Join('\n', epics.Select(e => e.Value.Summary).OrderBy(s => s))
                        }
                    }
                }
            };

            int i = 1, j = 0;
            foreach(var issue in issues.OrderBy(i => i.ParentIssueKey == null ? "Z" : epics.First(e => e.Value.Key == i.ParentIssueKey).Value.Summary)) 
            {
                replaceTextRequest.Requests.Add(new Request
                {
                    ReplaceAllText = new ReplaceAllTextRequest
                    {
                        ContainsText = new SubstringMatchCriteria
                        {
                            Text = $"{{story-{i}}}",
                            MatchCase = true
                        },
                        ReplaceText = issue.Summary,
                        PageObjectIds = new List<string> { storySlideObjectIds.ElementAt(j) }
                    }
                });

                replaceTextRequest.Requests.Add(new Request
                {
                    ReplaceAllText = new ReplaceAllTextRequest
                    {
                        ContainsText = new SubstringMatchCriteria
                        {
                            Text = $"{{progress-{i}}}",
                            MatchCase = true
                        },
                        ReplaceText = ProgressMap(issue.Status),
                        PageObjectIds = new List<string> { storySlideObjectIds.ElementAt(j) }
                    }
                });

                replaceTextRequest.Requests.Add(new Request
                {
                    ReplaceAllText = new ReplaceAllTextRequest
                    {
                        ContainsText = new SubstringMatchCriteria
                        {
                            Text = $"{{status-{i}}}",
                            MatchCase = true
                        },
                        ReplaceText = StatusMap(issue.Status),
                        PageObjectIds = new List<string> { storySlideObjectIds.ElementAt(j) }
                    }
                });

                if (++i > 3) {
                    ++j;
                    i = 1;
                }
            }

            Console.WriteLine($"Updating presentation placeholders");
            var textReplaceResponse = slidesService.Presentations.BatchUpdate(replaceTextRequest, presentationId).Execute();

            var numTextReplacements = 0;
            foreach (var reply in textReplaceResponse.Replies)
            {
                numTextReplacements += reply.ReplaceAllText.OccurrencesChanged.GetValueOrDefault();
            }

            Console.WriteLine($"Replaced {numTextReplacements} text instances");
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