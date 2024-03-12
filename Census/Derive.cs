using Cocona;
using Serilog;
using System.Globalization;
using ShellProgressBar;
using Libplanet.Crypto;
using Nekoyume.Action;

namespace Census
{
    public class Derive
    {
        public const int MaxAvatarCount = 3;

        [Command(
            "agent-to-avatar-addresses",
            Description =
                "Exports all possible avatar addresses from given agent addresses.  " +
                "Derived addresses may or may not be in use even if the agent address " +
                "the addresses are derived from has been recorded.")]
        public void AgentToAvatarAddress(
            [Option('i', Description = "Path of the input file containing agent addresses.")]
            string inputPath,
            [Option('o', Description = "Path of the output file.")]
            string outputPath)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            int lineCount = File.ReadAllLines(inputPath).Count();
            StreamReader input = new StreamReader(inputPath);
            StreamWriter output = new StreamWriter(outputPath);

            Log.Logger.Information("Generating avatar addresses from agent addresses");

            ProgressBar progressBar = new ProgressBar(1_000, "Unknown");
            IProgress<(double Progress, string Message)> progress =
                progressBar.AsProgress<(double Progress, string Message)>(
                    pair => pair.Message,
                    pair => pair.Progress);

            long lineRead = 0;
            while (input.ReadLine() is string line)
            {
                lineRead++;
                Address agentAddress = new Address(line);
                List<Address> avatarAddresses = Enumerable
                    .Range(0, MaxAvatarCount)
                    .Select(index => agentAddress.Derive(string.Format(
                        CultureInfo.InvariantCulture,
                        CreateAvatar.DeriveFormat,
                        index)))
                    .ToList();
                foreach (var address in avatarAddresses)
                {
                    output.WriteLine(address);
                }

                progress.Report(((double)lineRead / lineCount, $"{lineRead}/{lineCount}"));
            }

            progressBar.Dispose();
            output.Close();
        }
    }
}
