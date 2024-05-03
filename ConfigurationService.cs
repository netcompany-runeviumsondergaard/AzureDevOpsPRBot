using AzureDevOpsPRBot;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;

public class ConfigurationService
{
   private readonly IConfigurationRoot _configuration;
   private readonly IDataProtectionProvider _dataProtectionProvider;
   private readonly string patFilePath = "pat";

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
       var isPatStored = File.Exists(patFilePath);

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
               pat = Console.ReadLine();

               if(string.IsNullOrEmpty(pat))
               {
                   Console.WriteLine("\nWarning: You haven't provided a PAT. A valid PAT is required to proceed.\n");
               }
           }


           Console.WriteLine("Encrypting and storing the PAT...");
           // Encrypt and store PAT
           EncryptAndSavePat(pat);
       }


       return pat;
   }

   private void EncryptAndSavePat(string pat)
   {
       var protector = _dataProtectionProvider.CreateProtector("PAT_Protector");
       var encryptedPat = protector.Protect(pat);
       File.WriteAllText(patFilePath, encryptedPat);

       var fileInfo = new FileInfo(patFilePath);
       fileInfo.Attributes |= FileAttributes.Hidden;
   }

   private string DecryptPat()
   {
       var protector = _dataProtectionProvider.CreateProtector("PAT_Protector");
       var encryptedPat = File.ReadAllText(patFilePath);
       var decryptedPat = protector.Unprotect(encryptedPat);

       return decryptedPat;
   }

   public void DeletePatFile()
   {
       if (File.Exists(patFilePath))
       {
           File.Delete(patFilePath);
       }
   }

}
