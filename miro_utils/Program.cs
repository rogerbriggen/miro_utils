namespace miro_utils;

using System;
using System.Threading.Tasks;
using Serilog;
using miro_utils.MiroAutomation;
using Microsoft.Extensions.Configuration;
using CommandLine;
using Spectre.Console;

class CommonOptions
{
    [Option('n', "username", Required = false, HelpText = "Username for authentication")]
    public string? Username { get; set; }

    [Option('p', "userPassword", Required = true, HelpText = "User password for authentication")]
    public string? UserPassword { get; set; }
}

[Verb("backup", HelpText = "Backup Miro boards")]
class BackupOptions : CommonOptions
{ 
    
    
}

[Verb("changePassword", HelpText = "Change password of Miro boards")]
class ChangePasswordOptions : CommonOptions
{
    /*
    [Option('u', "urls", Required = false, HelpText = "List of URLs to change password")]
    public string[] Urls { get; set; }
    */

    [Option('b', "newBoardPassword", Required = true, HelpText = "New password for the boards")]
    public string? BoardPassword { get; set; }
}


class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Configure Serilog using the settings from appsettings.json
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            // Log application version
            var assembly = typeof(Program).Assembly;
            var version = assembly.GetName().Version;
            string appVersion = $"Miro Utils Version {version}";
            Log.Information(appVersion);
            Log.Information(new string('=', appVersion.Length));

            // Automatically bind configuration to MiroAutomationParams
            var automationParams = configuration.GetSection("MiroAutomationParams").Get<MiroAutomationParams>();

            // Load URLs and other settings
            //var urls = configuration.GetSection("backup_urls").Get<string[]>();

            if (automationParams == null)
            {
                Log.Warning("MiroAutomationParams not found in appsettings.json.");
                return;
            }

            // Parse command-line arguments
            var result = new Parser(with => with.HelpWriter = null).ParseArguments<BackupOptions, ChangePasswordOptions>(args);

            await result.MapResult(
                async (BackupOptions opts) => await HandleBackupAsync(opts, configuration, automationParams),
                async (ChangePasswordOptions opts) => await HandleChangePasswordAsync(opts, configuration, automationParams),
                errs => HandleErrors(result, errs, configuration) // Handle errors
            );

        }
        catch(Exception ex)
        {
            Log.Error(ex, "An error occurred during initialization.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task HandleBackupAsync(BackupOptions opts, IConfiguration configuration, MiroAutomationParams miroAutomationParams)
    {
        Log.Information("Starting backup");

        // We add our params
        if (!string.IsNullOrWhiteSpace(opts.Username))
        {
            miroAutomationParams.Username = opts.Username;
        }
        
        miroAutomationParams.UserPassword = opts.UserPassword;

        // We remove the other params
        miroAutomationParams.ChangePasswordParams = null;

        // Validate the parameters
        miroAutomationParams.Validate();

        // Run
        var result = await MiroAutomation.MiroAutomation.InitPlaywrightAsync(miroAutomationParams);

        // Output result
        Log.Information("Backup result: {Result}", result.Success ? "Success" : "Failure");
        Log.Information("Backup result details: {@MiroAutomationResult}", result);

        // Print results
        PrintResults(result);
    }

    static async Task HandleChangePasswordAsync(ChangePasswordOptions opts, IConfiguration configuration, MiroAutomationParams miroAutomationParams)
    {
        Log.Information("Start changing board password");

        // We add our params
        if (!string.IsNullOrWhiteSpace(opts.Username))
        {
            miroAutomationParams.Username = opts.Username;
        }
        miroAutomationParams.UserPassword = opts.UserPassword;

        // We add the change password params
        if (miroAutomationParams.ChangePasswordParams == null)
        {
            miroAutomationParams.ChangePasswordParams = new MiroAutomationChangePasswordParams();
        }

        miroAutomationParams.ChangePasswordParams.NewBoardPassword = opts.BoardPassword;
        /*
        if (opts.Urls != null && opts.Urls.Length > 0)
        {
            miroAutomationParams.ChangePasswordParams.Urls = opts.Urls.ToList();
        }
        */

        // We remove the other params
        miroAutomationParams.BackupParams = null;

        // Validate the parameters
        miroAutomationParams.Validate();

        // Run
        var result = await MiroAutomation.MiroAutomation.InitPlaywrightAsync(miroAutomationParams);

        // Output result
        Log.Information("Changing password result: {Result}", result.Success ? "Success" : "Failure");
        Log.Information("Changing password result details: {@MiroAutomationResult}", result);

        // Print results
        PrintResults(result);
    }

    static Task HandleErrors(ParserResult<object> parserResult, IEnumerable<Error> errors, IConfiguration configuration)
    {
        errors = errors.ToList();
        if (errors.Count() == 0)
        {
            // No errors, just return
            return Task.CompletedTask;
        }
        
        if (errors.All(e => e is UnknownOptionError))
        {
            // Only UnknownOptionError... probably he is given parameters for appsettings on command line
            return Task.CompletedTask;
        }

        // Show help text for missing required options
        var helpText = CommandLine.Text.HelpText.AutoBuild(
            parserResult,
            h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.Heading = "Miro Utils";
                h.Copyright = "Copyright (c) 2025 Roger Briggen";
                return h;
            }
        );
        Log.Error("\n{HelpText}", helpText);
        return Task.CompletedTask;
    }

    static void PrintResults(MiroAutomationResult miroAutomationResult)
    {
        if (miroAutomationResult.BackupResult != null)
        {
            PrintGenericResults(miroAutomationResult.BackupResult, "Backup Results");
        }
        if (miroAutomationResult.ChangePasswordResult != null)
        {
            PrintGenericResults(miroAutomationResult.ChangePasswordResult, "Change Password Results");
        }
    }

    static void PrintGenericResults(MiroAutomationGenericResult result, string title)
    {
        // Print the results of the operations
        // This is a placeholder for actual result printing logic
        var table = new Table();
        table.AddColumn("Result");
        table.AddColumn("Link");
        table.AddColumn("Board name");

        foreach (var item in result.Results)
        {
            var color = item.Value.Success ? "green" : "red";
            var statusText = item.Value.Success ? "Success" : "Failure";
            var status = $"[{color}]{statusText}[/]";
            //var status = item.Value.Success ? Emoji.Known.CheckMark : Emoji.Known.CrossMark;
            var link = $"[{color}]{item.Key}[/]";
            var boardName = item.Value.BoardName;
            table.AddRow(status, link, boardName);
        }

        var titleColor = result.Success ? "green" : "red";
        table.Title = new TableTitle($"\n[underline {titleColor}]{title}[/]");

        AnsiConsole.Write(table);
    }
}
