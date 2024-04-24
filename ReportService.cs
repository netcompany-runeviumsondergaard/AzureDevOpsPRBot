namespace AzureDevOpsPRBot;

public static class ReportService
{
    // Helper method to print repo details in a formatted way
    private static void PrintRepoDetail(string repoId, string sourceBranch, string targetBranch)
    {
        Console.WriteLine($"{repoId,-30} | {sourceBranch,-20} | {targetBranch,-20}");
    }

    public static void DisplayPrSummary(List<(string RepoId, string SourceBranch, string TargetBranch)> prSummary,
        List<string> noChangeList, List<(string RepoId, string BranchName)> nonExistentBranches,
        List<(string RepoId, string SourceBranch, string TargetBranch)> existingPrList)
    {
        var lineSeparator = new string('-', 75);

        Console.WriteLine("----------------- Pull Request Summary -----------------");

        // Display repositories with potential pull requests
        if (prSummary.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Potential Pull Requests -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Source Branch",-20} | {"Target Branch",-20}");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, sourceBranch, targetBranch) in prSummary)
            {
                PrintRepoDetail(repoId, sourceBranch, targetBranch);
            }
        }

        // Display repositories with no changes
        if (noChangeList.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with No Changes -----------------");
            Console.WriteLine($"{"Repository ID",-30} | Status");
            Console.WriteLine(new string('-', 45));
            foreach (var repo in noChangeList)
            {
                Console.WriteLine($"{repo,-30} | No Changes");
            }
        }

        // Display repositories with non-existent branches
        if (nonExistentBranches.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Non-Existent Branches -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Branch Name",-30} | Status");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, branchName) in nonExistentBranches)
            {
                // Set the console color to red for highlighting the error
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{repoId,-30} | {branchName,-30} | Does not exist");
                // Reset the console color
                Console.ResetColor();
            }
        }

        // Display repositories with existing pull requests
        if (existingPrList.Count != 0)
        {
            Console.WriteLine("----------------- Repositories with Existing Pull Requests -----------------");
            Console.WriteLine($"{"Repository ID",-30} | {"Source Branch",-20} | {"Target Branch",-20}");
            Console.WriteLine(lineSeparator);
            foreach (var (repoId, sourceBranch, targetBranch) in existingPrList)
            {
                PrintRepoDetail(repoId, sourceBranch, targetBranch);
            }
        }

        // If there are no pull requests needed, print a friendly message
        if (prSummary.Count != 0 || nonExistentBranches.Count != 0 || existingPrList.Count != 0 ||
            noChangeList.Any(repo => repo != "No Changes"))
        {
            return;
        }

        Console.WriteLine("-------------------------------------------------------");
        Console.WriteLine("\nEverything is up to date, no pull requests needed! :)");
    }
}