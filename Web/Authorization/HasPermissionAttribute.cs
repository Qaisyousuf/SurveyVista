// Authorization/HasPermissionAttribute.cs
using Microsoft.AspNetCore.Authorization;

namespace Web.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
            : base(policy: permission)
        {
        }
    }
}