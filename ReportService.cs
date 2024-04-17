namespace AzureDevOpsPRBot;

public class ReportService
{
    public static void DisplayPrSummary(List<(string RepositoryId, string SourceBranch, string TargetBranch)> prSummary, List<string> noChangeList, List<(string RepositoryId, string BranchName)> nonExistentBranches)
    {
        Console.WriteLine("\n----------------- Pull Request Summary -----------------");
        Console.WriteLine("\n----------------- Repositories with Potential Pull Requests -----------------");
        foreach (var (repositoryId, sourceBranch, targetBranch) in prSummary)
        {
            Console.WriteLine($"Repository: {repositoryId}, Source Branch: {sourceBranch}, Target Branch: {targetBranch}");
        }

        Console.WriteLine("\n----------------- Repositories with No Changes -----------------");
        foreach (var repo in noChangeList)
        {
            Console.WriteLine($"Repository: {repo}");
        }

        Console.WriteLine("\n----------------- Repositories with Non-Existent Source Branches -----------------");
        foreach (var (repositoryId, branchName) in nonExistentBranches)
        {
            Console.WriteLine($"Repository: {repositoryId}, Branch: {branchName} does not exist");
        }

        Console.WriteLine("-------------------------------------------------------\n");
    }

}