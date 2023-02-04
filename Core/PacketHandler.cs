using System;
using System.Collections.Generic;
using Google.Protobuf;
using Acorle.Models;
using static Acorle.Models.RequestPacket.Types;
using static Acorle.Models.ResponsePacket.Types;


namespace Acorle.Core
{
    public static class PacketHandler
    {

        public static string GetResponseCodeDescription(ResponseCodeType code) => code switch
        {
            ResponseCodeType.ServerException => "server exception",
            ResponseCodeType.NotFound => "not found",
            ResponseCodeType.Forbidden => "forbidden",
            ResponseCodeType.BadGateway => "bad gateway",
            ResponseCodeType.BadRequest => "bad request",
            ResponseCodeType.ServiceUnavailable => "service unavailable",
            ResponseCodeType.MethodNotAllowed => "method not allowed",
            ResponseCodeType.InvalidBody => "invalid body",
            ResponseCodeType.RpcInvalidZone => "rpc: invalid zone",
            ResponseCodeType.RpcOperationFailed => "rpc: operation failed",
            ResponseCodeType.RpcRegLimit => "rpc: could not register more services",
            ResponseCodeType.RpcResponseError => "rpc: response error",
            ResponseCodeType.RpcResponseTimedout => "rpc: response timed out",
            ResponseCodeType.RpcNetworkException => "rpc: network exception",
            ResponseCodeType.RpcConfigNotFound => "rpc: configuration not found",
            ResponseCodeType.SvcInvalidZone => "service: invalid zone",
            ResponseCodeType.SvcNotFoundOrUnavailable => "service: not found or unavailable",
            ResponseCodeType.Ok => "ok",
            _ => "unknown",
        };

        public static byte[] MakeResponse(ResponseCodeType code, ByteString data = null)
        {
            var responsePacket = new ResponsePacket
            {
                Magic = Constants.PacketMagic,
                Code = code,
                Data = data ?? ByteString.Empty,
            };
            return responsePacket.ToByteArray();
        }

        public static object MakePlainErrorObject(ResponseCodeType code) => new {
            Code = code,
            Message = GetResponseCodeDescription(code)
        };


        public static ResponsePacket MakeEmptyResponsePacket() => new() {
            Magic = Constants.PacketMagic,
            Code = ResponseCodeType.Ok
        };


        public static RpcRequestOut MakeRpcRequestOut(ByteString data, string zone, string secret, string key, string ip, int port, IEnumerable<HeaderKVPair> headers)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var packetOutbound = new RpcRequestOut
            {
                Magic = Constants.PacketMagic,
                Signature = MakeRpcSignature(zone, secret, timestamp),
                Timestamp = timestamp,
                Zone = zone,
                Key = key,
                Ip = ip,
                Port = port,
                Data = data,
            };
            packetOutbound.Headers.Add(headers);
            return packetOutbound;
        }


        public static string MakeRpcSignature(string zone, string secret, long timestamp)
            => Utils.HmacSha1Hash($"{timestamp}{zone}{secret}", $"{timestamp}{zone}{secret}");


        public static bool ValidateRequestPacket(RequestPacket packet)
        {
            if (packet == null || packet.Magic != Constants.PacketMagic || string.IsNullOrEmpty(packet.Zone) || packet.Data.IsEmpty)
                return false;
            return true;
        }


        public static bool ValidateServiceRequest(ServiceRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Key))
                return false;
            return true;
        }


        public static bool ValidateRpcRequest(RpcRequest request)
        {
            if (request == null
                || string.IsNullOrEmpty(request.Signature)
                || request.Timestamp <= 0
                )
                return false;

            // 防止重放攻击，如果用户 Timestamp 造假，则会在后面 GetRpcPayload() 签名验证环节被发现和拦截。
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (request.Timestamp < currentTimestamp - Constants.RpcAntiReplaySeconds * 1000 || request.Timestamp > currentTimestamp + Constants.RpcAntiReplaySeconds * 1000)
            {
                return false;
            }
            return true;
        }


        public static byte[] GetRpcPayload(RpcRequest request, string zone, string secret)
        {
            string signature = MakeRpcSignature(zone, secret, request.Timestamp);
            if (signature != request.Signature) return null;
            return request.Data.ToByteArray();
        }

    }
}
