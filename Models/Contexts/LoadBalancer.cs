#nullable disable

namespace Acorle.Models.Contexts
{
    public partial class LoadBalancer
    {
        public string Zone { get; set; }
        public string Service { get; set; }
        public uint Type { get; set; }
    }
}
