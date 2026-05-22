using System.Security.Claims;

namespace BeastVault.Api.Helpers;

public static class HttpContextHelper
{
    public static int? GetUserId(this HttpContext context)
    {
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    public static bool IsAdmin(this HttpContext context) =>
        context.User.IsInRole("Admin");
}
