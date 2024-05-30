using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace AzureDevOpsPRBot;

public class ConfigurationService
{
    private readonly IConfigurationRoot _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private const string PatFilePath = "pat";

    public ConfigurationService(IDataProtectionProvider dataProtectionProvider)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables();

        _configuration = builder.Build();
        _dataProtectionProvider = dataProtectionProvider;
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

    public string GetPat()
    {
        var pat = Environment.GetEnvironmentVariable("PAT");
        var isPatStored = File.Exists(PatFilePath);

        if (string.IsNullOrEmpty(pat) && isPatStored)
        {
            pat = DecryptPat();
        }
        else if (string.IsNullOrEmpty(pat) && !isPatStored)
        {
            Console.WriteLine("This application requires a Personal Access Token (PAT) for authentication.");

            while (string.IsNullOrEmpty(pat))
            {
                Console.Write("Please enter your PAT: ");
                pat = ReadPassword();
                if(string.IsNullOrEmpty(pat))
                {
                    Console.WriteLine("\nWarning: You haven't provided a PAT. A valid PAT is required to proceed.\n");
                }
            }

            Console.WriteLine("Encrypting and storing the PAT...");
            // Encrypt and store PAT
            EncryptAndSavePat(pat);
        }

        return pat!;
    }

    private void EncryptAndSavePat(string pat)
    {
        var protector = _dataProtectionProvider.CreateProtector("PAT_Protector");
        var encryptedPat = protector.Protect(pat);
        File.WriteAllText(PatFilePath, encryptedPat);

        var fileInfo = new FileInfo(PatFilePath);
        fileInfo.Attributes |= FileAttributes.Hidden;
    }

    private string DecryptPat()
    {
        var protector = _dataProtectionProvider.CreateProtector("PAT_Protector");
        var encryptedPat = File.ReadAllText(PatFilePath);
        var decryptedPat = protector.Unprotect(encryptedPat);

        return decryptedPat;
    }

    public static void DeletePatFile()
    {
        if (File.Exists(PatFilePath))
        {
            File.Delete(PatFilePath);
        }
    }

    private static string ReadPassword()
    {
        var password = new StringBuilder();
        while (true)
        {
            var info = Console.ReadKey(true);
            if (info.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (info.Key == ConsoleKey.Backspace)
            {
                if (password.Length <= 0)
                {
                    continue;
                }

                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b"); // Moves the cursor back one space, writes a blank space, then moves back again
            }
            else
            {
                password.Append(info.KeyChar);
                Console.Write("*"); // Masking the character
            }
        }
        Console.WriteLine(); // Ensure the cursor moves to the next line after Enter is pressed
        return password.ToString();
    }
}