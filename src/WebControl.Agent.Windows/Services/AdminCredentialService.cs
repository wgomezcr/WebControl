using Microsoft.AspNetCore.Identity;
using WebControl.Agent.Windows.Persistence;

namespace WebControl.Agent.Windows.Services;

public sealed class AdminCredentialService
{
    private readonly AdminUserRepository _repository;

    private readonly PasswordHasher<AdminPasswordSubject>
        _passwordHasher = new();

    public AdminCredentialService(
        AdminUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> CreateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(password);

        var existing =
            await _repository.GetByUsernameAsync(
                username,
                cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                "Administrator user already exists.");
        }

        var subject =
            new AdminPasswordSubject(
                username.Trim());

        var passwordHash =
            _passwordHasher.HashPassword(
                subject,
                password);

        return await _repository.CreateAsync(
            subject.Username,
            passwordHash,
            cancellationToken);
    }

    public async Task<bool> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user =
            await _repository.GetByUsernameAsync(
                username,
                cancellationToken);

        if (user is null)
        {
            return false;
        }

        var subject =
            new AdminPasswordSubject(
                user.Username);

        var result =
            _passwordHasher.VerifyHashedPassword(
                subject,
                user.PasswordHash,
                password);

        if (result ==
            PasswordVerificationResult.Failed)
        {
            return false;
        }

        if (result ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash =
                _passwordHasher.HashPassword(
                    subject,
                    password);

            await _repository.UpdatePasswordHashAsync(
                user.Id,
                newHash,
                cancellationToken);
        }

        return true;
    }

    public async Task ResetPasswordAsync(
        long userId,
        string username,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(
            newPassword);

        var subject =
            new AdminPasswordSubject(
                username.Trim());

        var passwordHash =
            _passwordHasher.HashPassword(
                subject,
                newPassword);

        await _repository.UpdatePasswordHashAsync(
            userId,
            passwordHash,
            cancellationToken);
    }

    private static void ValidatePassword(
        string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 10)
        {
            throw new ArgumentException(
                "Administrator password must contain at least 10 characters.",
                nameof(password));
        }
    }

    private sealed record AdminPasswordSubject(
        string Username);
}
