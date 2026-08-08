using Microsoft.AspNetCore.Mvc;
using MyCustomUmbracoProject.Services;

namespace MyCustomUmbracoProject.Controllers
{
    [Route("umbraco/surface/AiSearch")]
    public class AiSearchSurfaceController : Controller
    {
        private readonly IExchangeIntake _intake;
        private readonly ChatHistoryService _chatHistory;
        private readonly ILogger<AiSearchSurfaceController> _logger;

        private const string SessionCookie = "chatSessionId";
        private const string UserCookie    = "chatUserId";
        private static readonly TimeSpan VisitorCookieLifetime = TimeSpan.FromDays(365 * 2);
        private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(30);

        public AiSearchSurfaceController(
            IExchangeIntake intake,
            ChatHistoryService chatHistory,
            ILogger<AiSearchSurfaceController> logger)
        {
            _intake = intake;
            _chatHistory = chatHistory;
            _logger = logger;
        }

        [HttpPost("Ask")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask(string prompt, string model)
        {
            var visitorId = GetOrCreateUserId();
            var sessionId = GetOrCreateSessionId();

            var resolvedModel = ExchangeIntake.ResolveModel(model);
            TempData["SelectedModel"] = resolvedModel;

            if (string.IsNullOrWhiteSpace(prompt))
                return RedirectToReferer();

            try
            {
                await _intake.HandleAsync(visitorId, sessionId, prompt, resolvedModel);
            }
            catch (MessageChannelException ex)
            {
                _logger.LogError(ex, "Channel failure handling prompt: {Prompt}", prompt);
                TempData["AiError"] = ex.Message;
            }

            return RedirectToReferer();
        }

        [HttpPost("NewChat")]
        [ValidateAntiForgeryToken]
        public IActionResult NewChat()
        {
            SetCookie(SessionCookie, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.Add(SessionCookieLifetime));
            return RedirectToReferer();
        }

        [HttpPost("LoadSession")]
        [ValidateAntiForgeryToken]
        public IActionResult LoadSession(string sessionId)
        {
            if (!UserOwnsSession(sessionId))
                return RedirectToReferer();

            SetCookie(SessionCookie, sessionId, DateTimeOffset.UtcNow.Add(SessionCookieLifetime));
            return RedirectToReferer();
        }

        [HttpPost("DeleteSession")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSession(string sessionId)
        {
            if (!UserOwnsSession(sessionId))
                return RedirectToReferer();

            _chatHistory.ClearSession(sessionId);

            if (Request.Cookies[SessionCookie] == sessionId)
                SetCookie(SessionCookie, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.Add(SessionCookieLifetime));

            return RedirectToReferer();
        }

        private string GetOrCreateUserId() => GetOrCreateCookie(UserCookie, DateTimeOffset.UtcNow.Add(VisitorCookieLifetime));
        private string GetOrCreateSessionId() => GetOrCreateCookie(SessionCookie, DateTimeOffset.UtcNow.Add(SessionCookieLifetime));

        private string GetOrCreateCookie(string name, DateTimeOffset expires)
        {
            if (Request.Cookies.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing))
                return existing;

            var newId = Guid.NewGuid().ToString("N");
            SetCookie(name, newId, expires);
            return newId;
        }

        private void SetCookie(string name, string value, DateTimeOffset expires)
        {
            Response.Cookies.Append(name, value, new CookieOptions
            {
                Expires = expires,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
        }

        private IActionResult RedirectToReferer() => Redirect(Request.Headers.Referer.ToString());

        private bool UserOwnsSession(string sessionId) =>
            _chatHistory.GetAllSessions(GetOrCreateUserId()).Any(s => s.SessionId == sessionId);
    }
}
