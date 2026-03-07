using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Authorization;
using Web.ViewModel.AccountVM;

namespace Web.Areas.Admin.Controllers
{

    [HasPermission(Permissions.Roles.View)]
    [Area("Admin")]
    public class RolesController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RolesController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var roles = _roleManager.Roles.ToList();
            var models = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                var claims = await _roleManager.GetClaimsAsync(role);
                var permissions = claims
                    .Where(c => c.Type == Permissions.ClaimType)
                    .Select(c => c.Value)
                    .ToList();

                models.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    SelectedPermissions = permissions
                });
            }

            // Pass grouped permissions for the UI
            ViewBag.PermissionGroups = Permissions.GetAllGrouped();

            return View(models);
        }


        [HasPermission(Permissions.Roles.Create)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax(RoleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return Json(new { success = false, errors = new List<string> { "Role name is required." } });
            }

            // Check if role already exists
            var existingRole = await _roleManager.FindByNameAsync(model.Name);
            if (existingRole != null)
            {
                return Json(new { success = false, errors = new List<string> { $"Role '{model.Name}' already exists." } });
            }

            var role = new IdentityRole { Name = model.Name };
            var result = await _roleManager.CreateAsync(role);

            if (result.Succeeded)
            {
                // Save permissions as claims
                if (model.SelectedPermissions != null && model.SelectedPermissions.Any())
                {
                    foreach (var permission in model.SelectedPermissions)
                    {
                        await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                    }
                }

                return Json(new { success = true, message = $"Role '{model.Name}' created successfully." });
            }

            var errors = result.Errors.Select(e => e.Description).ToList();
            return Json(new { success = false, errors });
        }

        [HttpGet]

        [HasPermission(Permissions.Roles.View)]
        public async Task<IActionResult> GetRolePermissions(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return Json(new { success = false, errors = new List<string> { "Role not found." } });
            }

            var claims = await _roleManager.GetClaimsAsync(role);
            var permissions = claims
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToList();

            return Json(new
            {
                success = true,
                id = role.Id,
                name = role.Name,
                permissions
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission(Permissions.Roles.Edit)]
        public async Task<IActionResult> EditAjax(RoleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return Json(new { success = false, errors = new List<string> { "Role name is required." } });
            }

            var role = await _roleManager.FindByIdAsync(model.Id);
            if (role == null)
            {
                return Json(new { success = false, errors = new List<string> { "Role not found." } });
            }

            // Update name
            role.Name = model.Name;
            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return Json(new { success = false, errors });
            }

            // Remove old permission claims
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var claim in existingClaims.Where(c => c.Type == Permissions.ClaimType))
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            // Add new permission claims
            if (model.SelectedPermissions != null && model.SelectedPermissions.Any())
            {
                foreach (var permission in model.SelectedPermissions)
                {
                    await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                }
            }

            return Json(new { success = true, message = $"Role '{model.Name}' updated successfully." });
        }


        [HasPermission(Permissions.Roles.Delete)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultiple(List<string> selectedRoles)
        {
            if (selectedRoles == null || !selectedRoles.Any())
            {
                TempData["Error"] = "No roles selected for deletion.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var roleId in selectedRoles)
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role != null)
                {
                    // Remove all claims first
                    var claims = await _roleManager.GetClaimsAsync(role);
                    foreach (var claim in claims)
                    {
                        await _roleManager.RemoveClaimAsync(role, claim);
                    }

                    await _roleManager.DeleteAsync(role);
                }
            }

            TempData["Success"] = "Selected roles deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}