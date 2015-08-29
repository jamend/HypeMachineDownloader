using System;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace HypeMachineDownloader
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--help") || !args.Contains("--account") || !args.Contains("--page"))
            {
                Console.WriteLine(@"
Arguments:
--help          Show this help
--account       Specify the account name
--page          Specify the page number
--limit         (optional) Specify the maximum number of tracks to download
");
                return;
            }

            ConfigureNLog();

            var accountIndex = Array.IndexOf(args, "--account");
            if (args.Length < accountIndex + 2 || string.IsNullOrEmpty(args[accountIndex + 1]))
            {
                Console.WriteLine("Invalid argument value for --account");
                return;
            }
            var account = args[accountIndex + 1];

            int page;
            var pageIndex = Array.IndexOf(args, "--page");
            if (args.Length < pageIndex + 2 || !int.TryParse(args[pageIndex + 1], out page))
            {
                Console.WriteLine("Invalid argument value for --page");
                return;
            }

            var limit = 0;
            var limitIndex = Array.IndexOf(args, "--limit");
            if (limitIndex != -1)
            {
                if (args.Length < limitIndex + 2 || !int.TryParse(args[limitIndex + 1], out limit))
                {
                    Console.WriteLine("Invalid argument value for --limit");
                    return;
                }
            }

            var worker = new Worker
            {
                Account = account,
                Page = page,
                Limit = limit
            };
            worker.Start();
        }

        private static void ConfigureNLog()
        {
            // TODO make this configurable
            const string logFile = "program.log";
            const string fileLayout = "[${level:padding=-5:fixedlength=true}] [${date}] ${logger:padding=-20:fixedlength=true:shortName=true}: ${message} ${exception:format=ToString}";
            const string consoleLayout = "[${level:padding=-5:fixedlength=true}] ${message} ${exception:format=ToString}";

            if (string.IsNullOrEmpty(logFile))
            {
                throw new Exception("Log file not configured in shared.config or shared.config not found");
            }

            var config = new LoggingConfiguration();

            var consoleTarget = new ConsoleTarget
            {
                Layout = consoleLayout
            };
            config.AddTarget("console", consoleTarget);

            var debuggerTarget = new DebuggerTarget
            {
                Layout = fileLayout
            };
            config.AddTarget("debugger", debuggerTarget);

            var fileTarget = new FileTarget
            {
                Layout = fileLayout,
                FileName = logFile
            };
            config.AddTarget("file", fileTarget);

            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, consoleTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, debuggerTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, fileTarget));

            LogManager.Configuration = config;
        }
    }
}
