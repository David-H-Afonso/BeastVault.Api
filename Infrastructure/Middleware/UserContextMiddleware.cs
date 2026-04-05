using System.Security.Claims;

namespace BeastVault.Api.Infrastructure.Middleware
{
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            int? userId = null;

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsed))
                    userId = parsed;
            }

            if (!userId.HasValue
                && context.Request.Headers.TryGetValue("X-User-Id", out var headerValue)
                && int.TryParse(headerValue.FirstOrDefault(), out var headerId))
            {
                userId = headerId;
            }

            if (userId.HasValue)
                context.Items["UserId"] = userId.Value;

            await _next(context);
        }
    }

    public static class UserContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserContext(this IApplicationBuilder builder)
            => builder.UseMiddleware<UserContextMiddleware>();
    }
}
