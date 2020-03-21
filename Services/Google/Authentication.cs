using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using JiraToSlides.Config;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;

using DriveApi = Google.Apis.Drive.v3;
using SlidesApi = Google.Apis.Slides.v1;

namespace JiraToSlides.GoogleAuth
{
    public static class Authentication
    {
        public static BaseClientService.Initializer BaseClientService 
        { 
            get {
                return _baseClientService.Value;
            }
        }

        private static readonly Lazy<BaseClientService.Initializer> _baseClientService = new Lazy<BaseClientService.Initializer>(() => {
            return Authenticate();
        });

        private static readonly List<string> Scopes = new List<string> 
        { 
            SlidesApi.SlidesService.Scope.Presentations,
            DriveApi.DriveService.Scope.Drive 
        };

        private static BaseClientService.Initializer Authenticate()
        {
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

            return new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = Configuration.ApplicationName,
            };
        }
    }
}