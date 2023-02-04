using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Acorle.Core
{
    public static class Utils
    {
        public static string HmacSha1Hash(string message, string secret)
        {
            secret ??= string.Empty;
            var encoding = new UTF8Encoding();
            byte[] keyInBytes = encoding.GetBytes(secret);
            byte[] messageInBytes = encoding.GetBytes(message);
            using var hmacSha1 = new HMACSHA1(keyInBytes);
            byte[] hashedMessage = hmacSha1.ComputeHash(messageInBytes);
            return Convert.ToBase64String(hashedMessage);
        }


        public static string Sha1Hash(string message)
        {
            byte[] messageInBytes = new UTF8Encoding().GetBytes(message);
            using var sha1 = SHA1.Create();
            byte[] hashedMessage = sha1.ComputeHash(messageInBytes);
            return Convert.ToBase64String(hashedMessage);
        }


        public static long ToUnixTimeMilliseconds(DateTime input) => new DateTimeOffset(input.ToLocalTime()).ToUnixTimeMilliseconds();


        public static string JsonSerialize<T>(T rawObject)
            => JsonSerializer.Serialize(rawObject, Constants.JsonSerializerOptionsGlobal);


        public static T JsonDeserialize<T>(string jsonText)
            => JsonSerializer.Deserialize<T>(jsonText, Constants.JsonSerializerOptionsGlobal);


        public async static Task<T> JsonDeserializeAsync<T>(Stream utf8JsonStream)
            => await JsonSerializer.DeserializeAsync<T>(utf8JsonStream, Constants.JsonSerializerOptionsGlobal).ConfigureAwait(false);
    }
}
