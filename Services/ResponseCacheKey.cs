using System.Security.Cryptography;
using System.Text;
using MyCustomUmbracoProject.Models;

namespace MyCustomUmbracoProject.Services
{
    public static class ResponseCacheKey
    {
        public static string Build(string model, string prompt, IReadOnlyList<Exchange> history)
        {
            var normalized = prompt.Trim().ToLowerInvariant();
            var historyDigest = ComputeHistoryDigest(history);
            return $"{model}:{historyDigest}:{normalized}";
        }

        private static string ComputeHistoryDigest(IReadOnlyList<Exchange> history)
        {
            var sb = new StringBuilder();
            foreach (var exchange in history)
            {
                sb.Append(exchange.UserPrompt).Append('')
                  .Append(exchange.ResponseMarkdown).Append('');
            }
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash)[..16];
        }
    }
}
