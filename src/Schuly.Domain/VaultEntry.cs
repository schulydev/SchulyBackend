namespace Schuly.Domain
{
    public class VaultEntry : Base
    {
        // The vault namespace the entry belongs to: "plugin:<PluginName>" for plugin
        // vaults, "host" for the host's own vault.
        public required string PluginName { get; set; }
        public required string Key { get; set; }
        public required byte[] Nonce { get; set; }
        public required byte[] Tag { get; set; }
        public required byte[] Ciphertext { get; set; }
        public int KeyVersion { get; set; }
    }
}
