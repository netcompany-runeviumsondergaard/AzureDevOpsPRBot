using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDevOpsPRBot;

internal partial class Program
{
    private static async Task Main()
    {
        // Set up dependency injection
        ServiceCollection serviceCollection = [];
        serviceCollection.AddDataProtection(); // This adds IDataProtectionProvider to DI
        IServiceProvider services = serviceCollection.BuildServiceProvider();
        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();


        var configurationService = new ConfigurationService(dataProtectionProvider);
        var pullRequestService = new PullRequestService(configurationService);

        var sourceBranch = configurationService.GetValue(Constants.SourceBranch);
        var targetBranch = configurationService.GetValue(Constants.TargetBranch);
        var repositoryIds = configurationService.GetRepositoryList();

        var needPrRepositories = new List<(string RepositoryId, string SourceBranch, string TargetBranch)>();
        var noChangeRepositoryIds = new List<string>();
        var nonExistentBranchRepositoryIds = new List<(string RepositoryId, string BranchName)>();
        var repositoriesWithExistingPr = new List<(string RepositoryId, string SourceBranch, string TargetBranch)>();

        foreach (var repositoryId in repositoryIds)
        {
            var sourceBranchExists = await pullRequestService.BranchExists(repositoryId, sourceBranch);
            var targetBranchExists = await pullRequestService.BranchExists(repositoryId, targetBranch);

            if (sourceBranchExists && targetBranchExists)
            {
                if (await pullRequestService.BranchHasChanges(repositoryId, sourceBranch, targetBranch))
                {
                    // Check if a pull request already exists
                    var prExists = await pullRequestService.PullRequestExists(repositoryId,
                        $"{sourceBranch}-intermediate", targetBranch);
                    if (prExists)
                    {
                        repositoriesWithExistingPr.Add((repositoryId, sourceBranch, targetBranch));
                    }
                    else
                    {
                        needPrRepositories.Add((repositoryId, sourceBranch, targetBranch));
                    }
                }
                else
                {
                    noChangeRepositoryIds.Add(repositoryId);
                }
            }
            else
            {
                if (!sourceBranchExists)
                {
                    nonExistentBranchRepositoryIds.Add((repositoryId, sourceBranch));
                }
                if (!targetBranchExists)
                {
                    nonExistentBranchRepositoryIds.Add((repositoryId, targetBranch));
                }
            }
        }

        ReportService.DisplayPrSummary(needPrRepositories, noChangeRepositoryIds, nonExistentBranchRepositoryIds,
            repositoriesWithExistingPr);

        var isPullRequestsCreated = false;

        if (needPrRepositories.Count != 0)
        {
            Console.WriteLine("Do you want to create the above pull requests? (Y/N)");
            var userInput = Console.ReadLine()?.ToUpper();

            if (userInput == "Y")
            {
                foreach (var repositoryPrInfo in needPrRepositories)
                {
                    await pullRequestService.CreatePullRequest(repositoryPrInfo.RepositoryId,
                        repositoryPrInfo.SourceBranch, repositoryPrInfo.TargetBranch);
                }

                isPullRequestsCreated = true;
            }
        }

        if (!isPullRequestsCreated)
        {
            Console.WriteLine("-------------------------------------------------------");
            Console.WriteLine("\nEverything is up to date, no pull requests needed! :)");
        }

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();  // Added this line
    }
}