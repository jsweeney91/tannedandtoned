using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin;

public class LoginModel(IOptions<AdminSettings> adminOptions, ILogger<LoginModel> logger) : PageModel
{
    public class InputModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Admin/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var settings = adminOptions.Value;
        var valid = string.Equals(Input.Username, settings.Username, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(settings.PasswordHash)
            && PasswordHasher.Verify(Input.Password, settings.PasswordHash);

        if (!valid)
        {
            logger.LogWarning("Failed admin login attempt for user {Username}.", Input.Username);
            await Task.Delay(Random.Shared.Next(300, 800));
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, settings.Username),
            new(ClaimTypes.Role, "Owner")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Admin/Index");
    }
}
