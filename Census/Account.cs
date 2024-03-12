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
using Libplanet.Crypto;

namespace Census
{
    public class Account
    {
        [Command(
            "export-addresses",
            Description =
                "Exports all addresses from given account.  This " +
                "is done by iterating through all data for which an IValue " +
                "stored.  As such, this may take a very long time.")]
        public void ExportAddresses(
            [Option('p', Description = "Path of the chain storage to use.")]
            string storePath,
            [Option('a', Description = "The account address to traverse.")]
            string accountAddressHex,
            [Option('o', Description = "Path of the output file.")]
            string outputPath)
        {
            Address accountAddress = new Address(accountAddressHex);
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            StreamWriter output = new StreamWriter(outputPath);
            (BlockChain blockChain, IStore store, IStateStore stateStore) = Utils.LoadBlockChain(storePath);

            int predicateLength = Address.Size * 2;

            ITrie worldTrie = Utils.GetWorldTrie(blockChain);
            IWorldState world = new WorldBaseState(worldTrie, stateStore);
            IAccountState account = world.GetAccountState(accountAddress);
            ITrie accountTrie = account.Trie;
            Log.Logger.Information("Iterating over trie with state root hash {StateRootHash}", accountTrie.Hash);

            long addressCount = 0;
            string? currentAddress = null;

            Stopwatch timer = new Stopwatch();
            timer.Start();
            ProgressBar progressBar = new ProgressBar(1_000, "Unknown");
            IProgress<(double Progress, string Message)> progress =
                progressBar.AsProgress<(double Progress, string Message)>(
                    pair => pair.Message,
                    pair => pair.Progress);
            foreach ((KeyBytes keyBytes, IValue value) in accountTrie.IterateValues())
            {
                if (keyBytes.Length == predicateLength)
                {
                    addressCount++;
                    Address address = Utils.ToAddress(keyBytes);
                    currentAddress = ByteUtil.Hex(address.ByteArray);
                    output.WriteLine(currentAddress);
                }

                if (currentAddress is string hex)
                {
                    progress.Report(
                        (Utils.EstimateProgress(currentAddress), $"Address: {currentAddress}, address count: {addressCount}"));
                }
            }

            progressBar.Dispose();
            Log.Logger.Information("Total time: {ElapsedMilliseconds} ms", timer.ElapsedMilliseconds);
            Log.Logger.Information("Total address count: {AddressCount}", addressCount);

            output.Close();
            store.Dispose();
            stateStore.Dispose();
        }
    }
}
