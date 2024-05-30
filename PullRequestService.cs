using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using static AzureDevOpsPRBot.Program;

namespace AzureDevOpsPRBot;

public class PullRequestService
{
    private readonly HttpClient _client;
    private readonly ConfigurationService _configurationService;
    private readonly string _azureGitUri;
    private readonly string _apiVersion;

    public PullRequestService(ConfigurationService configurationService)
    {
        _configurationService = configurationService;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var baseUri = new Uri(_configurationService.GetValue(Constants.BaseUrl));
        _azureGitUri = new Uri(baseUri, "_apis/git/repositories").ToString();
        _apiVersion = _configurationService.GetValue(Constants.ApiVersion);

    }

    public async Task InitializeAsync()
    {
        await InitializePatAuthentication();
    }

    private async Task InitializePatAuthentication()
    {
        while (true)
        {
            var pat = _configurationService.GetPat();
            if (!await IsPatValid(pat))
            {
                ConfigurationService.DeletePatFile();
                Console.WriteLine("Invalid PAT provided. Trying again...");
                continue;
            }

            break;
        }
    }

    private async Task<bool> IsPatValid(string pat)
    {
        SetAuthorizationHeader(pat);
        var response = await _client.GetAsync(_azureGitUri);

        if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NonAuthoritativeInformation)
        {
            return true;
        }

        Console.WriteLine($"Failed to authenticate. Response Status Code: {response.StatusCode}");
        return false;
    }

    private void SetAuthorizationHeader(string pat)
    {
        var base64Token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);
    }

    public async Task<bool> BranchExists(string repositoryId, string branchName)
    {
        const string resource = "refs";

        var queryParams = new Dictionary<string, string>
        {
            { "filter", $"heads/{branchName}" },
            { "api-version", _apiVersion }
        };

        var url = ConstructUrl(_azureGitUri, repositoryId, resource, queryParams);

        // Request to get potential matches (may include substrings)
        var response = await _client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch branch data: {response.StatusCode}");
            return false;
        }

        var content = await response.Content.ReadAsStringAsync();
        try
        {
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
        const string resource = "diffs/commits"; // The API resource path

        var queryParams = new Dictionary<string, string>
        {
            { "baseVersionType", "branch" },
            { "baseVersion", targetBranch },
            { "targetVersionType", "branch" },
            { "targetVersion", sourceBranch },
            { "api-version", _apiVersion }
        };

        var url = ConstructUrl(_azureGitUri, repositoryId, resource, queryParams);
        var response = await _client.GetAsync(url);

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
        var latestCommitId = await GetLatestCommitId(repositoryId, sourceBranch);
        var intermediateBranch = await CreateIntermediateBranch(repositoryId, sourceBranch, latestCommitId);

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

        var queryParams = new Dictionary<string, string>
        {
            { "api-version", _apiVersion }
        };

        var requestUrl = ConstructUrl(_azureGitUri, repositoryId, "pullrequests", queryParams);
        var response = await _client.PostAsync(requestUrl, data);
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

    private async Task<string?> CreateIntermediateBranch(string repositoryId, string sourceBranch, string? commitId)
    {
        var intermediateBranch = $"{sourceBranch}-intermediate-{DateTime.Now:yyMMddHHmm}";

        var branchExists = await BranchExists(repositoryId, intermediateBranch);
        if (!branchExists)
        {
            return await CreateBranch(repositoryId, intermediateBranch, commitId);
        }

        var baseUrl = _configurationService.GetValue(Constants.BaseUrl);
        var branchUrl = ConstructUrl(baseUrl, repositoryId, "_git", 
            queryParams: new Dictionary<string, string>
            {
                { "version", $"GB{Uri.EscapeDataString(intermediateBranch)}" }
            });

        // Log messages about the existing branch
        Console.WriteLine($"The intermediate branch '{intermediateBranch}' already exists for repository '{repositoryId}'.");
        Console.WriteLine($"Please delete the branch at: {branchUrl}");
        Console.WriteLine($"To delete the branch remotely, run the following git command from the '{repositoryId}' folder:");
        Console.WriteLine($"git push origin --delete {intermediateBranch}");

        return null;
    }

    private async Task<string?> CreateBranch(string repositoryId, string intermediateBranch, string? commitId)
    {
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

        var requestUrl = ConstructUrl(_azureGitUri, repositoryId, "refs", 
            queryParams: new Dictionary<string, string>
            {
                { "api-version", _apiVersion }
            });
        var response = await _client.PostAsync(requestUrl, data);
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
        var apiVersion = _configurationService.GetValue(Constants.ApiVersion);

        // Construct the request URL using ConstructUrl
        var queryParams = new Dictionary<string, string>
        {
            { "api-version", apiVersion },
            { "targetRefName", $"refs/heads/{targetBranch}" }
        };
        var requestUrl = ConstructUrl(_azureGitUri, repositoryId, "pullrequests", queryParams);

        try
        {
            var response = await _client.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var prResponse = JsonSerializer.Deserialize<PullRequestResponse>(content, JsonOptions.DefaultOptions);
            if (prResponse == null)
            {
                return false;
            }

            // Regex to match source branch pattern
            Regex sourceBranchRegex = new(
                    $@"^refs/heads/{Regex.Escape(sourceBranch)}-intermediate(-\d{{10}}?)?$",
                    RegexOptions.IgnoreCase);

            // Check if any PR matches the source branch pattern
            return prResponse.Value.Any(pr => pr.SourceRefName != null && sourceBranchRegex.IsMatch(pr.SourceRefName));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON deserialization error: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> GetLatestCommitId(string repositoryId, string branchName)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "searchCriteria.itemVersion.version", branchName },
            { "searchCriteria.top", "1" },
            { "api-version", _apiVersion }
        };

        var requestUrl = ConstructUrl(_azureGitUri, repositoryId, "commits", queryParams);
        var response = await _client.GetAsync(requestUrl);

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

    private static string ConstructUrl(string baseUri, string repositoryId = "", string resource = "", Dictionary<string, string>? queryParams = null, string additionalPath = "")
    {
        // Ensure the baseUri ends with a slash to avoid issues when concatenating
        if (!baseUri.EndsWith('/'))
            baseUri += "/";

        // Construct the full path ensuring repository and resources are correctly included
        var fullPath = $"{repositoryId}/{resource}{additionalPath}".TrimStart('/'); // Avoid leading slashes
        var uriBuilder = new UriBuilder(new Uri(new Uri(baseUri), fullPath));
        var query = HttpUtility.ParseQueryString(string.Empty);

        if (queryParams != null)
        {
            foreach (var param in queryParams)
            {
                // Add directly to the query collection without encoding here, if required
                query[param.Key] = param.Value;
            }
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }

    private static class JsonOptions
    {
        public static JsonSerializerOptions DefaultOptions { get; } = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}