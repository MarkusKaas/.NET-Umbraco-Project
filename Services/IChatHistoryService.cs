using MyCustomUmbracoProject.Models;

namespace MyCustomUmbracoProject.Services
{
    public interface IChatHistoryService
    {
        void Save(Exchange exchange);
        List<Exchange> GetBySession(string sessionId, int limit = 20);
        List<ChatSessionSummary> GetAllSessions(string userId);
        void ClearSession(string sessionId);
    }
}
