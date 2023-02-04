using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using static Acorle.Models.ResponsePacket.Types;
using System.Text;


namespace Acorle.Core
{
    // 因为程序有自己的协议格式，因此忽视 HTTP 的响应格式，将所有错误的 HTTP 代码重写为 200-OK，并发送自己格式的错误数据包
    public static class ErrorHandler
    {
        public static Action<IApplicationBuilder> commonErrorHandler = new(handler => {
            handler.Run(async context =>
            {
                var originalStatusCode = context.Response.StatusCode;
                context.Response.Headers.Add("Cache-Control", "no-store,no-cache");
                context.Response.Headers.Add("Pragma", "no-cache");

                // 针对直接 HTTP 访问的用户输出 JSON 格式的可视错误信息，针对自由协议的访问输出自有协议
                bool usePlainErrorEncoding = !(context.Request.Path.HasValue && (context.Request.Path.Value == "/" || context.Request.Path.Value == "/rpc"));
                context.Response.ContentType = usePlainErrorEncoding ? Constants.JsonContentType + "; charset=utf-8" : Constants.ProtobufContentType;

                // 使用自有协议的时候，就不需要覆写 HTTP 原生状态了
                if (!usePlainErrorEncoding) context.Response.StatusCode = StatusCodes.Status200OK;

                await context.Response.Body.WriteAsync(MakeCommonErrorResponse(originalStatusCode, usePlainErrorEncoding)).ConfigureAwait(false);
            });
        });

        // 将 404, 403, 500, 502 等框架抛出的标准 HTTP 错误统一转化为程序自己的格式
        private static byte[] MakeCommonErrorResponse(int? statusCode = StatusCodes.Status500InternalServerError, bool? plain = false)
        {
            if (plain == false)
            {
                return statusCode switch
                {
                    StatusCodes.Status415UnsupportedMediaType or StatusCodes.Status400BadRequest => PacketHandler.MakeResponse(ResponseCodeType.BadRequest),
                    StatusCodes.Status403Forbidden => PacketHandler.MakeResponse(ResponseCodeType.Forbidden),
                    StatusCodes.Status404NotFound => PacketHandler.MakeResponse(ResponseCodeType.NotFound),
                    StatusCodes.Status405MethodNotAllowed => PacketHandler.MakeResponse(ResponseCodeType.MethodNotAllowed),
                    StatusCodes.Status502BadGateway => PacketHandler.MakeResponse(ResponseCodeType.BadGateway),
                    StatusCodes.Status503ServiceUnavailable => PacketHandler.MakeResponse(ResponseCodeType.ServiceUnavailable),
                    _ => PacketHandler.MakeResponse(ResponseCodeType.ServerException),
                };
            }
            else
            {
                return statusCode switch
                {
                    StatusCodes.Status415UnsupportedMediaType or StatusCodes.Status400BadRequest => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.BadRequest))),
                    StatusCodes.Status403Forbidden => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.Forbidden))),
                    StatusCodes.Status404NotFound => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.NotFound))),
                    StatusCodes.Status405MethodNotAllowed => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.MethodNotAllowed))),
                    StatusCodes.Status502BadGateway => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.BadGateway))),
                    StatusCodes.Status503ServiceUnavailable => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.ServiceUnavailable))),
                    _ => Encoding.UTF8.GetBytes(Utils.JsonSerialize(PacketHandler.MakePlainErrorObject(ResponseCodeType.ServerException))),
                };
            }

        }
    }
}
