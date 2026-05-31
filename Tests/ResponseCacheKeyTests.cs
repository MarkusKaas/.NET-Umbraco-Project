using MyCustomUmbracoProject.Models;
using MyCustomUmbracoProject.Services;
using Xunit;

namespace MyCustomUmbracoProject.Tests;

public class ResponseCacheKeyTests
{
    [Fact]
    public void Identical_inputs_produce_identical_keys()
    {
        var history = new List<ChatMessage>
        {
            new() { UserPrompt = "hello", ResponseMarkdown = "hi there" }
        };

        var first  = ResponseCacheKey.Build("mistral-small-latest", "what is react?", history);
        var second = ResponseCacheKey.Build("mistral-small-latest", "what is react?", history);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_history_produces_different_key()
    {
        var pythonContext = new List<ChatMessage>
        {
            new() { UserPrompt = "tell me about python", ResponseMarkdown = "python is..." }
        };
        var reactContext = new List<ChatMessage>
        {
            new() { UserPrompt = "tell me about react", ResponseMarkdown = "react is..." }
        };

        var pythonKey = ResponseCacheKey.Build("mistral-small-latest", "what about hooks?", pythonContext);
        var reactKey  = ResponseCacheKey.Build("mistral-small-latest", "what about hooks?", reactContext);

        Assert.NotEqual(pythonKey, reactKey);
    }

    [Fact]
    public void Different_model_produces_different_key()
    {
        var history = new List<ChatMessage>();

        var smallKey = ResponseCacheKey.Build("mistral-small-latest", "hello", history);
        var largeKey = ResponseCacheKey.Build("mistral-large-latest", "hello", history);

        Assert.NotEqual(smallKey, largeKey);
    }

    [Fact]
    public void Prompt_normalization_is_preserved()
    {
        var history = new List<ChatMessage>();

        var padded = ResponseCacheKey.Build("mistral-small-latest", "  Hello  ", history);
        var clean  = ResponseCacheKey.Build("mistral-small-latest", "hello",      history);

        Assert.Equal(padded, clean);
    }

    [Fact]
    public void Empty_history_is_stable()
    {
        var first  = ResponseCacheKey.Build("mistral-small-latest", "hello", new List<ChatMessage>());
        var second = ResponseCacheKey.Build("mistral-small-latest", "hello", new List<ChatMessage>());

        Assert.Equal(first, second);
    }
}
