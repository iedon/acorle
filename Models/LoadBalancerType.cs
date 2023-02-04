namespace Acorle.Models
{
    public enum LoadBalancerType
    {
        NoLoadBalance = 0,
        LeastConnection = 1,
        Random = 2,
        SourceIpHash = 3,
        SmoothWeightRoundRobin = 4
    }
}
