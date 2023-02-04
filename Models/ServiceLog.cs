using System;
using static Acorle.Models.ResponsePacket.Types;


namespace Acorle.Models
{
    public class ServiceLog
    {
        /* 请求开始时间 */
        public DateTime RequestBegin { get; set; }

        /* 请求的 Zone */
        public string Zone { get; set; }

        /* 请求的业务键 */
        public string Key { get; set; }

        /* 请求者 IP */
        public string Ip { get; set; }

        /* 请求者端口 */
        public int Port { get; set; }

        /* 请求者 User-Agent */
        public string UA { get; set; }

        /* 中心节点返回的响应码 */
        public ResponseCodeType ResponseCode { get; set; }

        /* 完成本次请求所花的时间 */
        public long ElapsedMs { get; set; }
    }
}
