namespace MyCustomUmbracoProject.Services
{
    public interface IExchangeIntake
    {
        Task<string> HandleAsync(string visitorId, string sessionId, string prompt, string model, CancellationToken ct = default);
    }
}
