using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
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

        var pat = _configurationService.GetValue(Constants.PAT);
        var base64Token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);
    }


    public async Task<bool> BranchExists(string repositoryId, string branchName)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        var response =
            await _client.GetAsync($"{baseUrl}/{repositoryId}/refs?filter=heads/{branchName}&api-version={apiVersion}");

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        var refs = JsonConvert.DeserializeObject<RefResponse>(content);

        return refs!.Count > 0;
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
        var diffs = JsonConvert.DeserializeObject<DiffResponse>(content);

        return diffs!.ChangeCounts.Edit + diffs.ChangeCounts.Add + diffs.ChangeCounts.Delete > 0;
    }

    public async Task CreatePullRequest(string repositoryId, string sourceBranch, string targetBranch)
    {

        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);


        var latestCommitId = await GetLatestCommitId(repositoryId, sourceBranch);
        var intermediateBranch = await CreateIntermediateBranch(repositoryId, sourceBranch, latestCommitId);

        var pullRequest = new
        {
            sourceRefName = $"refs/heads/{intermediateBranch}",
            targetRefName = $"refs/heads/{targetBranch}",
            title = $"Merging changes from {sourceBranch} to {targetBranch}",
            description = "Automated pull request to merge changes"
        };

        var json = JsonConvert.SerializeObject(pullRequest);
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
            Console.WriteLine(
                $"Failed to create pull request. Status code: {response.StatusCode}, Error: {result}");
        }
    }

    private async Task<string?> CreateIntermediateBranch(string repositoryId, string sourceBranch, string? commitId)
    {
        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);
        var intermediateBranch = $"{sourceBranch}_intermediate";

        var branchCreation = new[]
        {
            new
            {
                name = $"refs/heads/{intermediateBranch}",
                oldObjectId = "0000000000000000000000000000000000000000",
                newObjectId = commitId
            }
        };

        var json = JsonConvert.SerializeObject(branchCreation);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{baseUrl}/{repositoryId}/refs?api-version={apiVersion}", data);

        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Intermediate branch {intermediateBranch} created successfully.");
            return intermediateBranch;
        }

        Console.WriteLine($"Failed to create intermediate branch. Status code: {response.StatusCode}");
        return null;
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
        var commits = JsonConvert.DeserializeObject<CommitResponse>(content);

        return commits!.Count > 0 ? commits.Value[0].CommitId : null;
    }
}