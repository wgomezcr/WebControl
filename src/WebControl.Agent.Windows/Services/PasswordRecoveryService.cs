using WebControl.Agent.Windows.Persistence;

namespace WebControl.Agent.Windows.Services;

public sealed class PasswordRecoveryService
{
    private readonly AdminUserRepository _adminRepository;
    private readonly AdminCredentialService _credentialService;
    private readonly RecoveryCredentialService _recoveryService;

    public PasswordRecoveryService(
        AdminUserRepository adminRepository,
        AdminCredentialService credentialService,
        RecoveryCredentialService recoveryService)
    {
        _adminRepository = adminRepository;
        _credentialService = credentialService;
        _recoveryService = recoveryService;
    }

    public async Task<PasswordRecoveryResult> ResetAsync(
        string username,
        string recoveryCode,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(recoveryCode))
        {
            return PasswordRecoveryResult.Failed();
        }

        var user =
            await _adminRepository.GetByUsernameAsync(
                username,
                cancellationToken);

        if (user is null)
        {
            return PasswordRecoveryResult.Failed();
        }

        var valid =
            await _recoveryService.ValidateAsync(
                user.Id,
                recoveryCode,
                cancellationToken);

        if (!valid)
        {
            return PasswordRecoveryResult.Failed();
        }

        await _credentialService.ResetPasswordAsync(
            user.Id,
            user.Username,
            newPassword,
            cancellationToken);

        await _recoveryService.MarkUsedAsync(
            user.Id,
            cancellationToken);

        //
        // El código utilizado deja de ser válido.
        // Se genera inmediatamente uno nuevo.
        //
        var newRecoveryCode =
            await _recoveryService.GenerateAsync(
                user.Id,
                cancellationToken);

        return PasswordRecoveryResult.Succeeded(
            newRecoveryCode);
    }
}

public sealed record PasswordRecoveryResult(
    bool Success,
    string? NewRecoveryCode)
{
    public static PasswordRecoveryResult Failed()
    {
        return new PasswordRecoveryResult(
            false,
            null);
    }

    public static PasswordRecoveryResult Succeeded(
        string newRecoveryCode)
    {
        return new PasswordRecoveryResult(
            true,
            newRecoveryCode);
    }
}
