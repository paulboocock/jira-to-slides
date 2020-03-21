using JiraToSlides.GoogleAuth;

using DriveApi = Google.Apis.Drive.v3;

namespace JiraToSlides.GoogleDrive
{
    public class DriveService
    {
        public string Duplicate(string fileId, string newFileName)
        {
            using var driveService = new DriveApi.DriveService(Authentication.BaseClientService);

            var copyMetadata = new DriveApi.Data.File
            {
                Name = newFileName
            };

            return driveService.Files.Copy(copyMetadata, fileId).Execute().Id;
        }
    }
}