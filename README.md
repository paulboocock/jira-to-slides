# jira-to-slides
A dotnet application to turn a Jira project sprint into Google Slides

### Requirements
.NET Core 3.1 (https://dotnet.microsoft.com/download/dotnet-core)

### Build

```
dotnet build
```

### Run

- Create Jira Api Key: https://id.atlassian.com/manage/api-tokens
  - Input details into `config.json`
- Create OAuth 2.0 Client ID: https://console.cloud.google.com/apis/credentials
  - Input `client_id`, `client_secret` and `project_id` into `credentials.json`
  
```
dotnet run
```
