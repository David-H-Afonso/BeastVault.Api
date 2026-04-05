namespace BeastVault.Api.Infrastructure.Helpers
{
    public static class HttpContextHelper
    {
        public static int? GetUserId(this HttpContext context)
        {
            if (context.Items.TryGetValue("UserId", out var value) && value is int id)
                return id;
            return null;
        }

        public static int GetUserIdOrDefault(this HttpContext context, int defaultUserId = 1)
            => context.GetUserId() ?? defaultUserId;
    }
}
