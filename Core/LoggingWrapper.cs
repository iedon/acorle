using System;
using Microsoft.Extensions.Logging;
using static Acorle.Models.ResponsePacket.Types;


namespace Acorle.Core
{
    public static class LoggingWrapper
    {
        private static readonly Action<ILogger, string, Exception> _logError = LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "error"), "Error {ActionDescription}");
        private static readonly Action<ILogger, long, string, string, ResponseCodeType, string, string, Exception> _logAccess = LoggerMessage.Define<long, string, string, ResponseCodeType, string, string>(LogLevel.Information, new EventId(2, "access"), "{ElapsedMs}ms, {Zone}, {Service}, {ResponseCode}, {RemoteAddress}, {UA}");

        public static void LogError(ILogger logger, string actionDescription, Exception exception)
        {
            _logError(logger, actionDescription, exception);
        }

        public static void LogAccess(ILogger logger, long elapsedMs, string zone, string service, ResponseCodeType responseCodeType, string remoteAddress, string userAgent)
        {
            _logAccess(logger, elapsedMs, zone, service, responseCodeType, remoteAddress, userAgent, null);
        }
    }
}
