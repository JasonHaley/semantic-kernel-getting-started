using Microsoft.Extensions.Configuration;

namespace HelloWorld.Plugin2.Console.Configuration;

internal class Configuration
{
    public static IConfigurationRoot ConfigureAppSettings()
    {
        var config = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true)
#if DEBUG
                        .AddJsonFile(GetUserJsonFilename(), optional: true, reloadOnChange: true)
#endif
                        .Build();
        return config;
    }

    static string GetUserJsonFilename()
    {
        return $"appsettings.user_{Environment.UserName.ToLower()}.json";
    }
}
