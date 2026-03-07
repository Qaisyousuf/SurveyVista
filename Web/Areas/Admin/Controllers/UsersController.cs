using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Model;
using System.Security.Claims;
using Web.Authorization;
using Web.ViewModel.AccountVM;

namespace Web.Areas.Admin.Controllers
{
    [HasPermission(Permissions.Users.View)]
    [Area("Admin")]
   
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();
            var models = new List<RegisterViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var model = new RegisterViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    SelectedRoles = roles.ToList()
                };
                models.Add(model);
            }

            // Pass roles for the modals
            ViewBag.Roles = _roleManager.Roles
                .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                .ToList();

            return View(models);
        }

        [HasPermission(Permissions.Users.Create)]
        public IActionResult Register()
        {
            var model = new RegisterViewModel
            {
                Roles = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList()
            };
            return View(model);
        }


        [HasPermission(Permissions.Users.Create)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if the email already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "A user with this email already exists.");
                    model.Roles = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email, // Consider using a different username convention if emails and usernames are distinct
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Assign roles to the user
                    foreach (var role in model.SelectedRoles)
                    {
                        await _userManager.AddToRoleAsync(user, role);
                    }

                    return RedirectToAction(nameof(Index)); // Redirect after successful registration
                }

                // If there are errors during the creation, add them to the ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            // Refresh the roles list for the form in case of an error
            model.Roles = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
            return View(model);
        }



        [HasPermission(Permissions.Users.Delete)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected(List<string> selectedUserIds)
        {
            if (selectedUserIds != null && selectedUserIds.Count > 0)
            {
                foreach (var userId in selectedUserIds)
                {
                    var user = await _userManager.FindByEmailAsync(userId);
                    if (user != null)
                    {
                        var result = await _userManager.DeleteAsync(user);
                        // Optionally handle each result
                    }
                }
                // Consider adding a TempData or ViewBag message for success or failure
            }
            return RedirectToAction(nameof(Index));
        }

        [HasPermission(Permissions.Users.Edit)]
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{id}'.");
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.ToList();

            var viewModel = new EditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                SelectedRoles = userRoles.ToList(),
                Roles = allRoles.Select(role => new SelectListItem
                {
                    Value = role.Name,
                    Text = role.Name,
                    Selected = userRoles.Contains(role.Name)
                }).ToList()
            };

            return View(viewModel);
        }


        [HasPermission(Permissions.Users.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"User with Id = {model.Id} cannot be found";
                return View("NotFound");
            }

            // Update user properties except for email
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                // Handling role updates
                var roles = await _userManager.GetRolesAsync(user);
                var resultRemove = await _userManager.RemoveFromRolesAsync(user, roles);
                if (!resultRemove.Succeeded)
                {
                    ModelState.AddModelError("", "Cannot remove user existing roles");
                    return View(model);
                }

                var resultAdd = await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                if (!resultAdd.Succeeded)
                {
                    ModelState.AddModelError("", "Cannot add selected roles to user");
                    return View(model);
                }

                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            model.Roles = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
            return View(model);
        }

        [HasPermission(Permissions.Users.Create)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAjax(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, errors });
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Json(new { success = false, errors = new List<string> { "A user with this email already exists." } });
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (model.SelectedRoles != null && model.SelectedRoles.Any())
                {
                    foreach (var role in model.SelectedRoles)
                    {
                        await _userManager.AddToRoleAsync(user, role);
                    }
                }

                return Json(new { success = true, message = "User created successfully." });
            }

            var createErrors = result.Errors.Select(e => e.Description).ToList();
            return Json(new { success = false, errors = createErrors });
        }


        [HasPermission(Permissions.Users.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAjax(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, errors });
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return Json(new { success = false, errors = new List<string> { $"User with Id = {model.Id} cannot be found." } });
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

                if (model.SelectedRoles != null && model.SelectedRoles.Any())
                {
                    await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                }

                return Json(new { success = true, message = "User updated successfully." });
            }

            var updateErrors = result.Errors.Select(e => e.Description).ToList();
            return Json(new { success = false, errors = updateErrors });
        }

    }
}
