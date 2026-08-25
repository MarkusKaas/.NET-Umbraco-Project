using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MyCustomUmbracoProject.Models;
using MyCustomUmbracoProject.Services;
using Xunit;

namespace MyCustomUmbracoProject.Tests;

public class ExchangeIntakeTests
{
    [Fact]
    public async Task Channel_receives_prior_history_followed_by_new_prompt()
    {
        var history = new FakeChatHistory();
        history.Save(new Exchange { SessionId = "s1", UserPrompt = "hi",   ResponseMarkdown = "hello back" });
        history.Save(new Exchange { SessionId = "s1", UserPrompt = "ok?", ResponseMarkdown = "yes" });
        var channel = new FakeMessageChannel("the reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "what now?", ExchangeIntake.DefaultModel);

        Assert.Equal(
            new[] { ("user", "hi"), ("assistant", "hello back"), ("user", "ok?"), ("assistant", "yes"), ("user", "what now?") },
            channel.LastMessages!.Select(m => (m.Role, m.Content)).ToArray()
        );
    }

    [Fact]
    public async Task Reply_is_persisted_as_exchange()
    {
        var history = new FakeChatHistory();
        var channel = new FakeMessageChannel("the reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "ask", ExchangeIntake.DefaultModel);

        var saved = Assert.Single(history.Saved);
        Assert.Equal("v1", saved.UserId);
        Assert.Equal("s1", saved.SessionId);
        Assert.Equal("ask", saved.UserPrompt);
        Assert.Equal("the reply", saved.ResponseMarkdown);
        Assert.Equal(ExchangeIntake.DefaultModel, saved.AiModel);
    }

    [Fact]
    public async Task Repeat_prompt_hits_cache_and_skips_channel()
    {
        var history = new FakeChatHistory();
        var channel = new FakeMessageChannel("cached reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "same",  ExchangeIntake.DefaultModel);
        await intake.HandleAsync("v1", "s2", "SAME ", ExchangeIntake.DefaultModel);

        Assert.Equal(1, channel.CallCount);
    }

    [Fact]
    public async Task Cache_hit_still_persists_a_new_exchange()
    {
        var history = new FakeChatHistory();
        var channel = new FakeMessageChannel("cached reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "same", ExchangeIntake.DefaultModel);
        await intake.HandleAsync("v1", "s2", "same", ExchangeIntake.DefaultModel);

        Assert.Equal(2, history.Saved.Count);
        Assert.Equal("s2", history.Saved[1].SessionId);
    }

    [Fact]
    public async Task Different_models_get_separate_cache_entries()
    {
        var history = new FakeChatHistory();
        var channel = new FakeMessageChannel("reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "hello", ExchangeIntake.DefaultModel);
        await intake.HandleAsync("v1", "s1", "hello", ExchangeIntake.LargeModel);

        Assert.Equal(2, channel.CallCount);
    }

    [Fact]
    public async Task Empty_prompt_throws()
    {
        var intake = BuildIntake(new FakeChatHistory(), new FakeMessageChannel("x"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            intake.HandleAsync("v1", "s1", "   ", ExchangeIntake.DefaultModel));
    }

    [Fact]
    public async Task Unknown_model_is_resolved_to_default()
    {
        var history = new FakeChatHistory();
        var channel = new FakeMessageChannel("reply");
        var intake  = BuildIntake(history, channel);

        await intake.HandleAsync("v1", "s1", "ask", "some-unknown-model");

        Assert.Equal(ExchangeIntake.DefaultModel, channel.LastModel);
        Assert.Equal(ExchangeIntake.DefaultModel, history.Saved.Single().AiModel);
    }

    private static ExchangeIntake BuildIntake(IChatHistoryService history, IMessageChannel channel) =>
        new(history, channel, new MemoryCache(new MemoryCacheOptions()), NullLogger<ExchangeIntake>.Instance);

    private class FakeMessageChannel : IMessageChannel
    {
        private readonly string _reply;
        public int CallCount { get; private set; }
        public IReadOnlyList<Message>? LastMessages { get; private set; }
        public string? LastModel { get; private set; }

        public FakeMessageChannel(string reply) => _reply = reply;

        public Task<string> SendAsync(IReadOnlyList<Message> messages, string model, CancellationToken ct = default)
        {
            CallCount++;
            LastMessages = messages;
            LastModel = model;
            return Task.FromResult(_reply);
        }
    }

    private class FakeChatHistory : IChatHistoryService
    {
        public List<Exchange> Saved { get; } = new();

        public void Save(Exchange exchange) => Saved.Add(exchange);

        public List<Exchange> GetBySession(string sessionId, int limit = 20) =>
            Saved.Where(e => e.SessionId == sessionId).Take(limit).ToList();

        public List<ChatSessionSummary> GetAllSessions(string userId) => new();

        public void ClearSession(string sessionId) => Saved.RemoveAll(e => e.SessionId == sessionId);
    }
}
