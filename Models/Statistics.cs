using System;
using System.Collections.Generic;


namespace Acorle.Models
{
    public class ServiceElement
    {
        public string Hash { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public int Weight { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime AddedTime { get; set; }
        public DateTime ExpireTime { get; set; }
        public int CurrentRequests { get; set; }
        public long FinishedRequests { get; set; }
        public long FailedRequests { get; set; }
    }

    public class Statistics
    {
        public string Zone { get; set; }
        public ICollection<ServiceElement> Services { get; set; }
    }
}
