namespace AzureDevOpsPRBot;

public static class ReportService
{
    public static void DisplayPrSummary(List<(string RepositoryId, string SourceBranch, string TargetBranch)> prSummary, List<string> noChangeList, List<(string RepositoryId, string BranchName)> nonExistentBranches)
    {
        Console.WriteLine("\n----------------- Pull Request Summary -----------------\n");

        // Display repositories with potential pull requests
        Console.WriteLine("----------------- Repositories with Potential Pull Requests -----------------\n");
        Console.WriteLine($"{ "Repository ID",-30} | { "Source Branch",-20} | { "Target Branch",-20}");
        Console.WriteLine(new string('-', 75));
        foreach (var (repositoryId, sourceBranch, targetBranch) in prSummary)
        {
            Console.WriteLine($"{ repositoryId,-30} | { sourceBranch,-20} | { targetBranch,-20}");
        }

        // Display repositories with no changes
        Console.WriteLine("\n----------------- Repositories with No Changes -----------------\n");
        Console.WriteLine($"{ "Repository ID",-30} | Status");
        Console.WriteLine(new string('-', 45));
        foreach (var repo in noChangeList)
        {
            Console.WriteLine($"{ repo,-30} | No Changes");
        }

        // Display repositories with non-existent branches
        Console.WriteLine("\n----------------- Repositories with Non-Existent Branches -----------------\n");
        Console.WriteLine($"{ "Repository ID",-30} | { "Branch Name",-30} | Status");
        Console.WriteLine(new string('-', 75));
        foreach (var (repositoryId, branchName) in nonExistentBranches)
        {
            // Set the console color to red for highlighting the error
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{ repositoryId,-30} | { branchName,-30} | Does not exist");
            // Reset the console color
            Console.ResetColor();
        }

        Console.WriteLine("-------------------------------------------------------\n");
    }


}