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
    private readonly AdminSessionService _sessionService;

    public LoginModel(
        AdminCredentialService credentialService,
        AdminSessionService sessionService)
    {
        _credentialService = credentialService;
        _sessionService = sessionService;
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

        var credentialStamp =
            await _sessionService.GetCredentialStampAsync(
                Input.Username,
                cancellationToken);

        if (credentialStamp is null)
        {
            ErrorMessage =
                "No fue posible crear la sesión.";

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
                    "Administrator"),

                new Claim(
                    AdminSessionService.CredentialStampClaimType,
                    credentialStamp)
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
