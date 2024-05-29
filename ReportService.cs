namespace AzureDevOpsPRBot;

public static class ReportService
{
    // Helper method to print repo details in a formatted way
    private static void PrintRepoDetail(string repoId, string sourceBranch, string targetBranch)
    {
        Console.WriteLine($"{repoId,-30} | {sourceBranch,-20} | {targetBranch,-20}");
    }

    public static void DisplayPrSummary(List<(string RepoId, string SourceBranch, string TargetBranch)> needPrRepositories,
        List<string> noChangeRepositoryIds, List<(string RepoId, string BranchName)> nonExistentBranchRepositoryIds,
        List<(string RepoId, string SourceBranch, string TargetBranch)> repositoriesWithExistingPr)
    {
        var lineSeparator = new string('-', 75);

        Console.WriteLine("----------------- Pull Request Summary -----------------");

        // Display repositories with potential pull requests
        if (needPrRepositories.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Potential Pull Requests -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Source Branch",-20} | {"Target Branch",-20}");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, sourceBranch, targetBranch) in needPrRepositories)
            {
                PrintRepoDetail(repoId, sourceBranch, targetBranch);
            }
        }

        // Display repositories with no changes
        if (noChangeRepositoryIds.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with No Changes -----------------");
            Console.WriteLine($"{"Repository ID",-30} | Status");
            Console.WriteLine(new string('-', 45));
            foreach (var repo in noChangeRepositoryIds)
            {
                Console.WriteLine($"{repo,-30} | No Changes");
            }
        }

        // Display repositories with non-existent branches
        if (nonExistentBranchRepositoryIds.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Non-Existent Branches -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Branch Name",-30} | Status");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, branchName) in nonExistentBranchRepositoryIds)
            {
                // Set the console color to red for highlighting the error
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{repoId,-30} | {branchName,-30} | Does not exist");
                // Reset the console color
                Console.ResetColor();
            }
        }

        // Display repositories with existing pull requests
        if (repositoriesWithExistingPr.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Existing Pull Requests -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Source Branch",-20} | {"Target Branch",-20}");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, sourceBranch, targetBranch) in repositoriesWithExistingPr)
            {
                PrintRepoDetail(repoId, sourceBranch, targetBranch);
            }
        }
    }
}