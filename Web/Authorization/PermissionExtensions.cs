using System.Security.Claims;

namespace Web.Authorization
{
    public static class PermissionExtensions
    {
        public static bool HasPermission(this ClaimsPrincipal user, string permission)
        {
            if (user == null) return false;
            if (user.IsInRole("Admin")) return true;
            return user.Claims.Any(c =>
                c.Type == Permissions.ClaimType &&
                c.Value == permission);
        }
    }
}