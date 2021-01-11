namespace Taskr
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    using ColoredConsole;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    internal static class Program
    {
        private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Taskr.json");

        private static async Task Main(string[] args)
        {
            try
            {
                try
                {
                    // Console.SetWindowPosition(0, 0);
                    Console.SetWindowSize(Console.LargestWindowWidth - 10, Console.LargestWindowHeight - 10);
                }
                catch
                {
                    // Do nothing
                }

                Console.OutputEncoding = Encoding.UTF8;

                // Credit: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.1
                // Credit: https://thecodebuzz.com/using-httpclientfactory-in-net-core-console-application/
                IConfiguration configuration = null;
                var appSettingsFile = GetSettingsFile();
                var builder = Host
                .CreateDefaultBuilder(args) // .ConfigureHostConfiguration(configHost => { })
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config
                        .AddJsonFile(appSettingsFile, optional: true, reloadOnChange: true);
                    configuration = config.Build();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .Configure<AccountSettings>(configuration)
                        .AddHostedService<Worker>()
                        .AddSingleton<JsonSerializer>()
                        .AddTransient<IBacklogService, AzDoService>()
                        .AddTransient<IBacklogService, JiraService>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging
                    .ClearProviders()
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddDebug()
                    .AddConsole();
                })
                .UseConsoleLifetime();

                await builder.RunConsoleAsync(options => options.SuppressStatusMessages = true);
            }
            catch (Exception ex)
            {
                ColorConsole.WriteLine(ex.Message.White().OnRed());
            }
        }

        private static string GetSettingsFile()
        {
            if (!File.Exists(SettingsFile))
            {
                var settings = new AccountSettings { Query = AzDoService.DefaultQuery, Slicers = "Tags,Priority,IterationPath", Accounts = new[] { new Account { Name = "Account-1", Org = "Org-1", Project = "Project-1", Token = "PAT Token for Org-1/Project-1", Enabled = true }, new Account { Name = "Account-2", Type = AccountType.Jira, Org = "Org-2", Project = "Project-2", Token = "user@email.com:apiToken", Query = JiraService.DefaultQuery, Enabled = true } } };
                SetSettings(settings);
                ColorConsole.WriteLine("Update settings here: ".Red(), SettingsFile);
            }

            return SettingsFile;
        }

        private static string GetSettings()
        {
            GetSettingsFile();
            return File.ReadAllText(SettingsFile);
        }

        private static void SetSettings(AccountSettings settings)
        {
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings, new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore }));
        }
    }
}
