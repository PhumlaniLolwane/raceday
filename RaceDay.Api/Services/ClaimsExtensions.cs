using System.Security.Claims;

namespace RaceDay.Api.Services;

public static class ClaimsExtensions
{
    /// <summary>The logged-in user's UserID, read from the session cookie.</summary>
    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
