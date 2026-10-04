using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using WebControl.Agent.Windows.Persistence;

namespace WebControl.Agent.Windows.Services;

public sealed class RecoveryCredentialService
{
    private const string Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly RecoveryCredentialRepository _repository;

    private readonly PasswordHasher<RecoveryCodeSubject>
        _hasher = new();

    public RecoveryCredentialService(
        RecoveryCredentialRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> GenerateAsync(
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        var recoveryCode =
            GenerateRecoveryCode();

        var subject =
            new RecoveryCodeSubject(
                adminUserId);

        var hash =
            _hasher.HashPassword(
                subject,
                recoveryCode);

        await _repository.UpsertAsync(
            adminUserId,
            hash,
            cancellationToken);

        return recoveryCode;
    }

    public async Task<bool> ValidateAsync(
        long adminUserId,
        string recoveryCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                recoveryCode))
        {
            return false;
        }

        var credential =
            await _repository.GetAsync(
                adminUserId,
                cancellationToken);

        if (credential is null ||
            credential.UsedAtUtc is not null)
        {
            return false;
        }

        var subject =
            new RecoveryCodeSubject(
                adminUserId);

        var result =
            _hasher.VerifyHashedPassword(
                subject,
                credential.RecoveryCodeHash,
                NormalizeRecoveryCode(
                    recoveryCode));

        return result !=
            PasswordVerificationResult.Failed;
    }

    public Task MarkUsedAsync(
        long adminUserId,
        CancellationToken cancellationToken = default)
    {
        return _repository.MarkUsedAsync(
            adminUserId,
            cancellationToken);
    }

    private static string GenerateRecoveryCode()
    {
        Span<char> characters =
            stackalloc char[20];

        for (var index = 0;
             index < characters.Length;
             index++)
        {
            characters[index] =
                Alphabet[
                    RandomNumberGenerator.GetInt32(
                        Alphabet.Length)];
        }

        var raw =
            new string(
                characters);

        return string.Join(
            "-",
            Enumerable.Range(0, 5)
                .Select(
                    index =>
                        raw.Substring(
                            index * 4,
                            4)));
    }

    private static string NormalizeRecoveryCode(
        string recoveryCode)
    {
        var characters =
            recoveryCode
                .Where(
                    character =>
                        character != '-' &&
                        !char.IsWhiteSpace(
                            character))
                .Select(
                    char.ToUpperInvariant)
                .ToArray();

        if (characters.Length != 20)
        {
            return recoveryCode.Trim();
        }

        var raw =
            new string(
                characters);

        return string.Join(
            "-",
            Enumerable.Range(0, 5)
                .Select(
                    index =>
                        raw.Substring(
                            index * 4,
                            4)));
    }

    private sealed record RecoveryCodeSubject(
        long AdminUserId);
}
