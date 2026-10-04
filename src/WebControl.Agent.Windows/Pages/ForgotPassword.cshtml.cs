using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebControl.Agent.Windows.Services;

namespace WebControl.Agent.Windows.Pages;

[AllowAnonymous]
public sealed class ForgotPasswordModel : PageModel
{
    private readonly PasswordRecoveryService _recoveryService;

    public ForgotPasswordModel(
        PasswordRecoveryService recoveryService)
    {
        _recoveryService = recoveryService;
    }

    [BindProperty]
    public ForgotPasswordInputModel Input { get; set; } =
        new();

    public bool Success { get; private set; }

    public string? NewRecoveryCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result =
                await _recoveryService.ResetAsync(
                    Input.Username,
                    Input.RecoveryCode,
                    Input.NewPassword,
                    cancellationToken);

            if (!result.Success)
            {
                ErrorMessage =
                    "Los datos de recuperación no son válidos.";

                return Page();
            }

            Success = true;

            NewRecoveryCode =
                result.NewRecoveryCode;

            Input =
                new ForgotPasswordInputModel();

            return Page();
        }
        catch (ArgumentException exception)
        {
            ErrorMessage =
                exception.Message;

            return Page();
        }
    }

    public sealed class ForgotPasswordInputModel
    {
        [Required(
            ErrorMessage = "Ingrese el usuario.")]
        public string Username { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Ingrese el código de recuperación.")]
        public string RecoveryCode { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Ingrese la nueva contraseña.")]
        [MinLength(
            10,
            ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } =
            string.Empty;

        [Required(
            ErrorMessage = "Confirme la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare(
            nameof(NewPassword),
            ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } =
            string.Empty;
    }
}
