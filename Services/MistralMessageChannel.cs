using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyCustomUmbracoProject.Models;

namespace MyCustomUmbracoProject.Services
{
    public class MistralMessageChannel : IMessageChannel
    {
        private const string Endpoint = "https://api.mistral.ai/v1/chat/completions";
        private static readonly TimeSpan RateLimitRetryDelay = TimeSpan.FromSeconds(2);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MistralMessageChannel> _logger;

        public MistralMessageChannel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<MistralMessageChannel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SendAsync(IReadOnlyList<Message> messages, string model, CancellationToken ct = default)
        {
            var apiKey = _configuration["MistralApiKey"]
                ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
                ?? throw new InvalidOperationException("No Mistral API key configured.");

            var requestBody = JsonSerializer.Serialize(new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content })
            });

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await PostJson(client, requestBody, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Mistral rate limit hit, retrying after {Delay}s", RateLimitRetryDelay.TotalSeconds);
                response.Dispose();
                await Task.Delay(RateLimitRetryDelay, ct);
                response = await PostJson(client, requestBody, ct);
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mistral API error: {StatusCode} - {Body}", response.StatusCode, json);
                throw new MessageChannelException($"Mistral API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        private static Task<HttpResponseMessage> PostJson(HttpClient client, string body, CancellationToken ct) =>
            client.PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "application/json"), ct);
    }

    public class MessageChannelException : Exception
    {
        public MessageChannelException(string message) : base(message) { }
    }
}
