using MyCustomUmbracoProject.Models;

namespace MyCustomUmbracoProject.Services
{
    public interface IMessageChannel
    {
        Task<string> SendAsync(IReadOnlyList<Message> messages, string model, CancellationToken ct = default);
    }
}
