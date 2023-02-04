#nullable disable

namespace Acorle.Models.Contexts
{
    public partial class Zone
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Secret { get; set; }
        public uint MaxServices { get; set; }
        public uint RegIntervalSeconds { get; set; }
        public uint RpcTimeoutSeconds { get; set; }
        public bool LogUserRequest { get; set; }
    }
}
