# AzureDevOpsPRBot

AzureDevOpsPRBot is a console application that automates the process of creating pull requests in Azure DevOps for specified repositories and branches.

## Prerequisites

- .NET Core 3.1 or later
- An Azure DevOps Personal Access Token (PAT) with appropriate permissions
- Visual Studio or any preferred .NET compatible IDE

## Configuration

The application uses an `appsettings.json` file for configuration. Here is an example of the settings:



```json
{
  "PAT": "<Your Personal Access Token>",
  "BaseUrl": "<Azure DevOps API Base URL>",
  "ApiVersion": "<API Version>",
  "SourceBranch": "<Branch from which changes will be pulled>",
  "TargetBranch": "<Branch into which changes will be merged>",
  "Repositories": ["<List of repositories in which PR will be created>"]
}
```

## Usage

The application checks the provided repositories for differences between the source and target branches. If there are changes, it generates a summary of potential pull requests. 

After displaying the summary, the application will prompt for user confirmation before creating the pull requests. 

## Error Handling

The application handles various scenarios such as non-existent branches and repositories without any changes between the source and target branches. It provides relevant error messages for these situations.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

Please see the license file in this repository for more details.
```

Please make sure to replace the placeholders in the `appsettings.json` example with your actual configuration values.