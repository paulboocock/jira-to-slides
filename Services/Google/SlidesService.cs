using System.Collections.Generic;
using Google.Apis.Slides.v1.Data;
using JiraToSlides.GoogleAuth;
using System.Linq;

using SlidesApi = Google.Apis.Slides.v1;

namespace JiraToSlides.GoogleDrive
{
    public class SlidesService
    {
        public int DuplicationRequestCount => _requests.Count(r => r.DuplicateObject != null);
        public int UpdatePositionRequestCount => _requests.Count(r => r.UpdateSlidesPosition != null);
        public int DeleteRequestCount => _requests.Count(r => r.DeleteObject != null);
        public int TextReplacementRequestCount => _requests.Count(r => r.ReplaceAllText != null);

        private readonly List<Request> _requests = new List<Request>();

        public Presentation GetPresentation(string objectId) 
        {
            using var slidesService = new SlidesApi.SlidesService(Authentication.BaseClientService);
            return slidesService.Presentations.Get(objectId).Execute();
        }

        public void QueueDuplicationRequest(string objectId, string duplicateId = null) 
        {
            var request = new DuplicateObjectRequest { ObjectId = objectId };

            if (duplicateId != null)
            {
                request.ObjectIds = new Dictionary<string, string> { { objectId, duplicateId } };
            }

            _requests.Add(new Request { DuplicateObject = request });
        }

        public void QueueUpdatePositionRequest(string objectId, int newPostition) 
        {
            _requests.Add(new Request { UpdateSlidesPosition = new UpdateSlidesPositionRequest 
            { 
                SlideObjectIds = new List<string> { objectId }, 
                InsertionIndex = newPostition 
            }});
        }

        public void QueueDeleteRequest(string objectId)
        {
            _requests.Add(new Request { DeleteObject = new DeleteObjectRequest { ObjectId = objectId }});
        }

        public void QueueTextReplacementRequest(string searchText, string replacementText, string pageObjectId = null)
        {
            var request = new Request 
            { 
                ReplaceAllText = new ReplaceAllTextRequest 
                { 
                    ContainsText = new SubstringMatchCriteria
                    {
                        Text = searchText,
                        MatchCase = true
                    },
                    ReplaceText = replacementText
                }
            };

            if (pageObjectId != null)
            {
                request.ReplaceAllText.PageObjectIds = new List<string> { pageObjectId };
            }

            _requests.Add(request);
        }

        public QueueResponse ExecuteQueue(string objectId) 
        {
            using var slidesService = new SlidesApi.SlidesService(Authentication.BaseClientService);

            var batchUpdate = new BatchUpdatePresentationRequest { Requests = _requests };

            var batchResponse = slidesService.Presentations.BatchUpdate(batchUpdate, objectId).Execute();

            _requests.Clear();

            return new QueueResponse
            {
                DuplicatedObjectIds = batchResponse.Replies.Where(r => r.DuplicateObject != null).Select(r => r.DuplicateObject.ObjectId).ToList(),
                ReplaceTextOccurences = batchResponse.Replies.Where(r => r.ReplaceAllText != null).Sum(r => r.ReplaceAllText.OccurrencesChanged.GetValueOrDefault()),
                UpdatedPositionObjectIds = _requests.Where(r => r.UpdateSlidesPosition != null).SelectMany(r => r.UpdateSlidesPosition.SlideObjectIds).ToList(),
                DeletedObjectIds = _requests.Where(r => r.DeleteObject != null).Select(r => r.DeleteObject.ObjectId).ToList()
            };
        }
    }

    public class QueueResponse
    {
        public List<string> DuplicatedObjectIds = new List<string>();
        public List<string> UpdatedPositionObjectIds = new List<string>();
        public List<string> DeletedObjectIds = new List<string>();
        public int ReplaceTextOccurences = 0;
    }
}