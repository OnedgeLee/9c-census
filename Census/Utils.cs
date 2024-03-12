using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Serilog;

namespace Census
{
    public static class Utils
    {
        public static (BlockChain, IStore, IStateStore) LoadBlockChain(string storePath)
        {
            Uri uri = new Uri(storePath);
            (IStore store, IStateStore stateStore) = LoadStores(uri);
            BlockChain blockChain = LoadBlockChain(store, stateStore);
            Log.Logger.Information("BlockChain loaded");
            Log.Logger.Information("Block index: {Index}", blockChain.Tip.Index);
            Log.Logger.Information("Block hash: {BlockHash}", blockChain.Tip.Hash);
            Log.Logger.Information("Block state root hash: {StateRootHash}", blockChain.Tip.StateRootHash);
            return (blockChain, store, stateStore);
        }

        private static (IStore, IStateStore) LoadStores(Uri uri)
        {
#pragma warning disable CS0168 // The variable '_' is declared but never used
            // FIXME: This is used to forcefully load the RocksDBStore assembly
            // so that StoreLoaderAttribute can find the appropriate loader.
            RocksDBStore _;
#pragma warning restore CS0168

            return StoreLoaderAttribute.LoadStore(uri) ??
                throw new NullReferenceException("Failed to load store");
        }

        private static BlockChain LoadBlockChain(IStore store, IStateStore stateStore)
        {
            Guid canon = store.GetCanonicalChainId()
                ?? throw new NullReferenceException(
                    $"Failed to load canonical chain from {nameof(store)}");
            BlockHash genesisHash = store.IndexBlockHash(canon, 0)
                ?? throw new NullReferenceException(
                    $"Failed to load genesis block from {nameof(store)}");
            Block genesis = store.GetBlock(genesisHash);
            IBlockChainStates blockChainStates = new BlockChainStates(store, stateStore);
            IActionLoader actionLoader = new SingleActionLoader(typeof(MockAction));
            IActionEvaluator actionEvaluator = new ActionEvaluator(
                policyBlockActionGetter: _ => null,
                stateStore: stateStore,
                actionTypeLoader: actionLoader);

            return new BlockChain(
                policy: new Libplanet.Blockchain.Policies.BlockPolicy(),
                stagePolicy: new Libplanet.Blockchain.Policies.VolatileStagePolicy(),
                store: store,
                stateStore: stateStore,
                genesisBlock: genesis,
                blockChainStates: blockChainStates,
                actionEvaluator: actionEvaluator);
        }

        public static ITrie GetWorldTrie(BlockChain blockChain)
        {
            // Confirm block protocol version.
            Block tip = blockChain.Tip;
            if (tip.ProtocolVersion >= 5)
            {
                Log.Logger.Information(
                    "Block protocol version confirmed: {ProtocolVersion}",
                    tip.ProtocolVersion);
            }
            else
            {
                throw new ArgumentException($"Invalid block protocol version: {tip.ProtocolVersion}");
            }

            // Confirm trie metadata.
            ITrie worldTrie = blockChain.GetWorldState(tip.Hash).Trie;
            TrieMetadata? trieMetadata = worldTrie.GetMetadata();
            if (trieMetadata is { } metadata && metadata.Version == tip.ProtocolVersion)
            {
                Log.Logger.Information("Trie metadata confirmed: {Metadata}", metadata.Version);
            }
            else
            {
                throw new ArgumentException($"Invalid trie metadata: {trieMetadata}");
            }

            return worldTrie;
        }

        public static double EstimateProgress(string hex, bool reverse = true)
        {
            int high = int.Parse("ffff", System.Globalization.NumberStyles.HexNumber);
            int pos = int.Parse(hex.Substring(0, 4), System.Globalization.NumberStyles.HexNumber);

            return reverse
                ? (double)(high - pos) / high
                : (double)pos / high;
        }
    }
}
