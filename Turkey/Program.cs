using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;

namespace Turkey
{
    class Program
    {
        public static readonly Option verboseOption = new Option(
            new string[] { "--verbose", "-v" },
            "Show verbose output", new Argument<bool>());

        public static readonly Option compatibleOption = new Option(
            new string[] { "--compatible", "-c" },
            "Make output compatible with dotnet-bunny", new Argument<bool>());

        public static readonly Option logDirectoryOption = new Option(
            new string[] { "--log-directory", "-l" },
            "Set directory for writing log files", new Argument<string>());

        public static async Task<int> Run(bool verbose, bool compatible, string logDir)
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            DirectoryInfo logDirectory;
            if (string.IsNullOrEmpty(logDir))
            {
                logDirectory = currentDirectory;
            }
            else
            {
                logDirectory = new DirectoryInfo(logDir);
            }

            LogWriter logWriter = new LogWriter(logDirectory);

            Cleaner cleaner = new Cleaner();

            DotNet dotnet = new DotNet();

            using (HttpClient client = new HttpClient())
            {
                NuGet nuget = new NuGet(client);
                bool live = await nuget.IsPackageLiveAsync("Microsoft.NetCore.App", dotnet.LatestRuntimeVersion);
                if (!live)
                {
                    Console.WriteLine("WARNING: Not possible to test non-live packages");
                }
            }

            SystemUnderTest system = new SystemUnderTest(
                runtimeVersion: dotnet.LatestRuntimeVersion,
                sdkVersion: dotnet.LatestSdkVersion,
                platformIds: new PlatformId().CurrentIds
            );

            TestRunner runner = new TestRunner(
                cleaner: cleaner,
                system: system,
                root: currentDirectory,
                verboseOutput: verbose,
                logWriter: logWriter);


            TestOutput outputFormat = new TestOutputFormats.NewOutput();
            if (compatible)
            {
                outputFormat = new TestOutputFormats.DotNetBunnyOutput();
            }
            var results = await runner.ScanAndRunAsync(outputFormat);

            int exitCode = (results.Total == results.Passed) ? 0 : 1;
            return exitCode;
        }

        static async Task<int> Main(string[] args)
        {
            Func<bool, bool, string, Task<int>> action = Run;
            var rootCommand = new RootCommand(description: "A test runner for running standalone bash-based or xunit tests",
                                              handler: CommandHandler.Create(action));

            var parser = new CommandLineBuilder(rootCommand)
                .AddOption(compatibleOption)
                .AddOption(verboseOption)
                .AddOption(logDirectoryOption)
                .UseVersionOption()
                .UseHelp()
                .UseParseErrorReporting()
                .Build();
            return await parser.InvokeAsync(args);
        }
    }
}
