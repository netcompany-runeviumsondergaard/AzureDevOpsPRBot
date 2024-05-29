using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static AzureDevOpsPRBot.Program;

namespace AzureDevOpsPRBot;

public class PullRequestService
{
    private readonly HttpClient _client;
    private readonly ConfigurationService _configurationService;

    public PullRequestService(ConfigurationService configurationService)
    {
        _configurationService = configurationService;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var pat = _configurationService.GetPat();
        var isPatValid = IsPatValid(pat).Result;
        while (!isPatValid)
        {
            _configurationService.DeletePatFile();
            pat = _configurationService.GetPat();
            isPatValid = IsPatValid(pat).Result;
        }
    }

    private async Task<bool> IsPatValid(string pat)
    {
        var base64Token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var response = await _client.GetAsync($"{baseUrl}");

        if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NonAuthoritativeInformation)
        {
            return true;
        }

        Console.WriteLine($"Response Status Code: {response.StatusCode}");
        return false;

    }

    public async Task<bool> BranchExists(string repositoryId, string branchName)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        // Request to get potential matches (may include substrings)
        var response = await _client.GetAsync($"{baseUrl}/{repositoryId}/refs?filter=heads/{branchName}&api-version={apiVersion}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch branch data: {response.StatusCode}");
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        try
        {
            // Deserialize the response into a suitable object
            var refs = JsonSerializer.Deserialize<RefResponse>(content, JsonOptions.DefaultOptions);
            if (refs == null || refs.Count == 0)
            {
                return false;
            }

            // Perform an exact match on the full reference path
            var exactRef = $"refs/heads/{branchName}";
            return refs.Value.Any(r => r.Name == exactRef);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON deserialization error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> BranchHasChanges(string repositoryId, string sourceBranch, string targetBranch)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        var response =
            await _client.GetAsync(
                $"{baseUrl}/{repositoryId}/diffs/commits?baseVersionType=branch&baseVersion={targetBranch}&targetVersionType=branch&targetVersion={sourceBranch}&api-version={apiVersion}");

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        var diffs = JsonSerializer.Deserialize<DiffResponse>(content, JsonOptions.DefaultOptions);

        return diffs!.ChangeCounts.Edit + diffs.ChangeCounts.Add + diffs.ChangeCounts.Delete > 0;
    }

    public async Task CreatePullRequest(string repositoryId, string sourceBranch, string targetBranch)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        var latestCommitId = await GetLatestCommitId(repositoryId, sourceBranch);
        var intermediateBranch = await CreateOrUpdateIntermediateBranch(repositoryId, sourceBranch, latestCommitId);

        if (intermediateBranch == null)
        {
            return;
        }

        var pullRequest = new
        {
            sourceRefName = $"refs/heads/{intermediateBranch}",
            targetRefName = $"refs/heads/{targetBranch}",
            title = $"Merging changes from {sourceBranch} to {targetBranch}",
            description = "Automated pull request to merge changes"
        };

        var json = JsonSerializer.Serialize(pullRequest, JsonOptions.DefaultOptions);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{baseUrl}/{repositoryId}/pullrequests?api-version={apiVersion}", data);

        var result = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Pull request created successfully.");
        }
        else if (response.StatusCode == HttpStatusCode.Conflict)
        {
            Console.WriteLine("Pull request already exists. No action performed.");
        }
        else
        {
            Console.WriteLine($"Failed to create pull request. Status code: {response.StatusCode}, Error: {result}");
        }
    }

    private async Task<string?> CreateOrUpdateIntermediateBranch(string repositoryId, string sourceBranch, string? commitId)
    {
        var intermediateBranch = $"{sourceBranch}-intermediate-{DateTime.Today:yyyy-MM-dd}";

        var branchExists = await BranchExists(repositoryId, intermediateBranch);
        if (!branchExists)
        {
            return await CreateBranch(repositoryId, intermediateBranch, commitId);
        }

        var baseUrl = _configurationService.GetValue(Constants.BaseUrl).Split("/_api")[0];
        var branchUrl = $"{baseUrl}/_git/{repositoryId}?version=GB{Uri.EscapeDataString(intermediateBranch)}";
       
        Console.WriteLine($"The intermediate branch '{intermediateBranch}' already exists for repository '{repositoryId}'.");
        Console.WriteLine($"Pleas delete the branch at: {branchUrl}");
        Console.WriteLine($"To delete the branch remotely, run the following git command from the '{repositoryId}' folder:");
        Console.WriteLine($"git push origin --delete {intermediateBranch}");
        return null;

    }


    private async Task<string?> CreateBranch(string repositoryId, string intermediateBranch, string? commitId)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);
        var branchRef = $"refs/heads/{intermediateBranch}";
        var branchCreation = new[]
        {
            new
            {
                name = branchRef,
                oldObjectId = "0000000000000000000000000000000000000000",
                newObjectId = commitId
            }
        };

        var json = JsonSerializer.Serialize(branchCreation, JsonOptions.DefaultOptions);
        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{baseUrl}/{repositoryId}/refs?api-version={apiVersion}", data);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to create branch {intermediateBranch}. Status code: {response.StatusCode}");
            return null;
        }

        Console.WriteLine($"Branch {intermediateBranch} created successfully.");
        return intermediateBranch;
    }

    public async Task<bool> PullRequestExists(string repositoryId, string sourceBranch, string targetBranch)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        var response = await _client.GetAsync(
            $"{baseUrl}/{repositoryId}/pullrequests?targetRefName=refs/heads/{targetBranch}&api-version={apiVersion}");

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var prResponse = JsonSerializer.Deserialize<PullRequestResponse>(content, JsonOptions.DefaultOptions);
            if (prResponse == null)
            {
                return false;
            }

            // Regex to match source branch pattern
            Regex sourceBranchRegex = new($@"^refs/heads/{Regex.Escape(sourceBranch)}(-intermediate(-\d{{4}}-\d{{2}}-\d{{2}})?)?$", RegexOptions.IgnoreCase);

            // Check if any PR matches the source branch pattern
            return prResponse.Value.Any(pr => sourceBranchRegex.IsMatch(pr.SourceRefName));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON deserialization error: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> GetLatestCommitId(string repositoryId, string branchName)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        var response =
            await _client.GetAsync(
                $"{baseUrl}/{repositoryId}/commits?searchCriteria.itemVersion.version={branchName}&searchCriteria.top=1&api-version={apiVersion}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var commits = JsonSerializer.Deserialize<CommitResponse>(content, JsonOptions.DefaultOptions);
            return commits!.Count > 0 ? commits.Value[0].CommitId : null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON deserialization error: {ex.Message}");
            return null;
        }
    }

    private static class JsonOptions
    {
        public static JsonSerializerOptions DefaultOptions { get; } = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}