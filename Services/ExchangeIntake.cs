using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyCustomUmbracoProject.Models;

namespace MyCustomUmbracoProject.Services
{
    public class ExchangeIntake : IExchangeIntake
    {
        public const string DefaultModel = "mistral-small-latest";
        public const string LargeModel   = "mistral-large-latest";

        private const int HistoryLimit = 10;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        private readonly IChatHistoryService _chatHistory;
        private readonly IMessageChannel _channel;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExchangeIntake> _logger;

        public ExchangeIntake(
            IChatHistoryService chatHistory,
            IMessageChannel channel,
            IMemoryCache cache,
            ILogger<ExchangeIntake> logger)
        {
            _chatHistory = chatHistory;
            _channel = channel;
            _cache = cache;
            _logger = logger;
        }

        public static string ResolveModel(string requested) =>
            requested == LargeModel ? LargeModel : DefaultModel;

        public async Task<string> HandleAsync(string visitorId, string sessionId, string prompt, string model, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

            var resolvedModel = ResolveModel(model);
            var cacheKey = BuildCacheKey(resolvedModel, prompt);

            if (_cache.TryGetValue(cacheKey, out string? cachedReply))
            {
                _logger.LogInformation("Cache hit for prompt: {Prompt}", prompt);
                _chatHistory.Save(BuildExchange(sessionId, visitorId, prompt, cachedReply!, resolvedModel));
                return cachedReply!;
            }

            var history = _chatHistory.GetBySession(sessionId, limit: HistoryLimit);
            var messages = BuildMessages(history, prompt);

            var reply = await _channel.SendAsync(messages, resolvedModel, ct);

            _cache.Set(cacheKey, reply, CacheTtl);
            _chatHistory.Save(BuildExchange(sessionId, visitorId, prompt, reply, resolvedModel));

            _logger.LogInformation("Response saved for prompt: {Prompt}", prompt);
            return reply;
        }

        private static string BuildCacheKey(string model, string prompt) =>
            $"{model}:{prompt.Trim().ToLowerInvariant()}";

        private static List<Message> BuildMessages(IReadOnlyList<Exchange> history, string prompt)
        {
            var messages = new List<Message>(history.Count * 2 + 1);
            foreach (var exchange in history)
            {
                messages.Add(Message.User(exchange.UserPrompt));
                messages.Add(Message.Assistant(exchange.ResponseMarkdown));
            }
            messages.Add(Message.User(prompt));
            return messages;
        }

        private static Exchange BuildExchange(string sessionId, string visitorId, string prompt, string reply, string model) => new()
        {
            SessionId = sessionId,
            UserId = visitorId,
            UserPrompt = prompt,
            ResponseMarkdown = reply,
            AiModel = model
        };
    }
}
