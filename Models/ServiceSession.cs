using System.Threading;
using Acorle.Models.Contexts;


namespace Acorle.Models
{
    public class ServiceSession
    {
        public Service Service { get; set; }

        private int currentLoadBalanceWeight;
        private int currentRequests;
        private long finishedRequests;
        private long failedRequests;

        public ServiceSession(Service service)
        {
            Service = service;
            currentLoadBalanceWeight = 0;
            currentRequests = 0;
            finishedRequests = 0;
            failedRequests = 0;
        }

        public int CurrentRequests => currentRequests;
        public long FinishedRequests => finishedRequests;
        public long FailedRequests => failedRequests;

        public int IncrementCurrentRequests() => Interlocked.Increment(ref currentRequests);
        public int DecrementCurrentRequests() => Interlocked.Decrement(ref currentRequests);
        public long IncrementFinishedRequests() => Interlocked.Increment(ref finishedRequests);
        public long IncrementFailedRequests() => Interlocked.Increment(ref failedRequests);

        public int CurrentLoadBalanceWeight => currentLoadBalanceWeight;
        public int SetCurrentLoadBalanceWeight(int weight) => Interlocked.Exchange(ref currentLoadBalanceWeight, weight);
    }
}
