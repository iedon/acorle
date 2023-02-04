using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Google.Protobuf;


namespace Acorle.Core
{
    // 此静态类的作用是在内存中维护一份配置供程序的各个模块使用
    public static class Constants
    {
        public const int DefaultHttpResponseCode = StatusCodes.Status200OK;
        public const string StreamContentType = "application/octet-stream";
        public const string JsonContentType = "application/json";
        public const string ProtobufContentType = "application/x-protobuf";
        public static JsonSerializerOptions JsonSerializerOptionsGlobal = new()
        {
            // 不美化输出(即采用压缩输出而不格式化)
            WriteIndented = false,
            // 采用驼峰命名法命名输出变量
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
        };

        public static ByteString PacketMagic = ByteString.CopyFrom(new byte[] { 0xAC, 0x02, 0x1E });


        /* ---------- 以下这些属性都在初始化的时候设置 ---------- */

        // 本程序集名称(项目名称, 不是全称)
        public static string AppName { get; set; }

        // 本程序集版本(项目版本, 不是文件版本)
        public static string AppVersion { get; set; }

        // 业务默认权重(当注册业务的时候不带 weight 显式指定权重时使用的默认值)
        public static int RpcServiceDefaultWeight { get; set; }

        // 最大包体长度限制（字节）。默认 4194304 字节（4MB）
        public static uint MaxBodyLengthBytes { get; set; }

        // 反重放攻击用，允许子节点请求的时间戳波动范围
        public static uint RpcAntiReplaySeconds { get; set; }

        // 指定会话上下文与主数据库同步时间间隔，单位秒，指定 0 禁用同步（不要在集群模式下禁用），默认 30 (半分钟)
        public static uint RegistrySyncIntervalSeconds { get; set; }

        // 是否启用统计信息接口 (/statistics) 以便查看网关会话信息
        public static bool EnableSessionContextStat { get; set; }

        // 本中心节点请求子节点业务时所使用的 HttpClient 的 User-Agent
        public static string RpcHttpClientUserAgent { get; set; }

        // 请求子节点业务时所使用的 HttpClient 的生命周期，单位秒
        public static double RpcHttpClientLifeTimeSeconds { get; set; }
    }
}
