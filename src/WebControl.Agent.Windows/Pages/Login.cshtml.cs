using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebControl.Agent.Windows.Services;

namespace WebControl.Agent.Windows.Pages;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly AdminCredentialService _credentialService;

    public LoginModel(
        AdminCredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; } =
        new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var valid =
            await _credentialService.ValidateAsync(
                Input.Username,
                Input.Password,
                cancellationToken);

        if (!valid)
        {
            ErrorMessage =
                "Usuario o contraseña incorrectos.";

            return Page();
        }

        var claims =
            new[]
            {
                new Claim(
                    ClaimTypes.Name,
                    Input.Username.Trim()),

                new Claim(
                    ClaimTypes.Role,
                    "Administrator")
            };

        var identity =
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

        var principal =
            new ClaimsPrincipal(
                identity);

        var properties =
            new AuthenticationProperties
            {
                IsPersistent =
                    Input.RememberMe
            };

        if (Input.RememberMe)
        {
            properties.ExpiresUtc =
                DateTimeOffset.UtcNow.AddDays(30);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        return RedirectToPage("/Index");
    }

    public sealed class LoginInputModel
    {
        [Required(
            ErrorMessage = "Ingrese el usuario.")]
        public string Username { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Ingrese la contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } =
            string.Empty;

        public bool RememberMe { get; set; }
    }
}
