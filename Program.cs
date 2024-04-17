namespace AzureDevOpsPRBot;

internal partial class Program
{
    private static async Task Main()
    {
        var configurationService = new ConfigurationService();
        var pullRequestService = new PullRequestService(configurationService);
        var reportService = new ReportService();

        var sourceBranch = configurationService.GetValue(Constants.SourceBranch);
        var targetBranch = configurationService.GetValue(Constants.TargetBranch);
        var repositories = configurationService.GetRepositoryList();

        var prSummary = new List<(string RepositoryId, string SourceBranch, string TargetBranch)>();
        var noChangeList = new List<string>();
        var nonExistentBranches = new List<(string RepositoryId, string BranchName)>();

        foreach (var repositoryId in repositories)
        {
            if (await pullRequestService.BranchExists(repositoryId, sourceBranch))
            {
                if (await pullRequestService.BranchHasChanges(repositoryId, sourceBranch, targetBranch))
                {
                    prSummary.Add((repositoryId, sourceBranch, targetBranch));
                }
                else
                {
                    noChangeList.Add(repositoryId);
                }
            }
            else
            {
                nonExistentBranches.Add((repositoryId, sourceBranch));
            }
        }

        ReportService.DisplayPrSummary(prSummary, noChangeList, nonExistentBranches);

        Console.WriteLine("Do you want to create the above pull requests? (Y/N)");
        var userInput = Console.ReadLine()?.ToUpper();

        if (userInput == "Y")
        {
            foreach (var valueTuple in prSummary)
            {
                await pullRequestService.CreatePullRequest(valueTuple.RepositoryId, valueTuple.SourceBranch,
                    valueTuple.TargetBranch);
            }
        }
    }
}