using System.Security.Claims;

namespace Nomelo.Server.Auth;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId
    {
        get
        {
            var user = accessor.HttpContext?.User
                       ?? throw new InvalidOperationException("No HttpContext");
            var sub = user.FindFirst("sub")?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(sub)
                ? throw new InvalidOperationException("user has no sub claim")
                : sub;
        }
    }
}
