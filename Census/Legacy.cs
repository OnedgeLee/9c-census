using Bencodex.Types;
using Cocona;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Serilog;
using System.Diagnostics;
using ShellProgressBar;

namespace Census
{
    public class Legacy
    {
        [Command(
            "export-agents",
            Description =
                "Exports all agent addresses from the legacy account.  This " +
                "is done by iterating through all data and see if an IValue " +
                "stored is in a particular form.  As such, this may take " +
                "a very long time.")]
        public void ExportAgents(
            [Option('p', Description = "Path of the chain storage to use.")]
            string storePath,
            [Option('o', Description = "Path of the output file.")]
            string outputPath)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            StreamWriter output = new StreamWriter(outputPath);
            (BlockChain blockChain, IStore store, IStateStore stateStore) = Utils.LoadBlockChain(storePath);

            Text predicateKey = new Text("unlockedOptions");
            Text addressKey = new Text("address");

            ITrie worldTrie = Utils.GetWorldTrie(blockChain);
            IWorldState world = new WorldBaseState(worldTrie, stateStore);
            IAccountState account = world.GetAccountState(ReservedAddresses.LegacyAccount);
            ITrie accountTrie = account.Trie;
            Log.Logger.Information("Iterating over trie with state root hash {StateRootHash}", accountTrie.Hash);

            long valueCount = 0;
            long agentCount = 0;
            string? currentAgentAddress = null;

            Stopwatch timer = new Stopwatch();
            timer.Start();
            ProgressBar progressBar = new ProgressBar(1_000, "Unknown");
            IProgress<(double Progress, string Message)> progress =
                progressBar.AsProgress<(double Progress, string Message)>(
                    pair => pair.Message,
                    pair => pair.Progress);
            foreach ((KeyBytes _, IValue value) in accountTrie.IterateValues())
            {
                valueCount++;

                // Very crude way of checking whether value is a serialized agent.
                // This assumes any value in legacy account is an agent if and only if
                // it is a dictionary and it also contains the key "unlockedOptions".
                if (value is Dictionary dict && dict.ContainsKey(predicateKey))
                {
                    Binary address = (Binary)dict[addressKey];
                    currentAgentAddress = ByteUtil.Hex(address.ByteArray);
                    output.WriteLine(currentAgentAddress);
                    agentCount++;
                }

                if (currentAgentAddress is string hex)
                {
                    progress.Report(
                        (Utils.EstimateProgress(currentAgentAddress), $"Agent address: {currentAgentAddress}, value count: {valueCount}, agent count: {agentCount}"));

                    if (Utils.EstimateProgress(currentAgentAddress) > 0.04)
                    {
                        break;
                    }
                }
            }

            progressBar.Dispose();
            Log.Logger.Information("Total time: {ElapsedMilliseconds} ms", timer.ElapsedMilliseconds);
            Log.Logger.Information("Total value count: {ValueCount}", valueCount);
            Log.Logger.Information("Total agent count: {AgentCount}", agentCount);

            output.Close();
            store.Dispose();
            stateStore.Dispose();
        }
    }
}
