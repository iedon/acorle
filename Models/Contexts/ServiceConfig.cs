using System;

#nullable disable

namespace Acorle.Models.Contexts
{
    public partial class ServiceConfig
    {
        public string Zone { get; set; }
        public string Key { get; set; }
        public string Hash { get; set; }
        public string Context { get; set; }
        public DateTime LastModified { get; set; }
    }
}
