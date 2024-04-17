using Microsoft.Extensions.Configuration;
using AzureDevOpsPRBot;

public class ConfigurationService
{
    private IConfigurationRoot _configuration;

    public ConfigurationService()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    public string GetValue(string key)
    {
        return _configuration[key] ?? throw new ArgumentNullException(key);
    }

    public List<string> GetRepositoryList()
    {
        return _configuration.GetSection(Constants.Repositories).Get<List<string>>() ??
               throw new ArgumentNullException(Constants.Repositories);
    }
}