using System;

#nullable disable

namespace Acorle.Models.Contexts
{
    public partial class Service
    {
        public string Zone { get; set; }
        public string Hash { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public int Weight { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime AddedTime { get; set; }
        public DateTime ExpireTime { get; set; }
    }
}
