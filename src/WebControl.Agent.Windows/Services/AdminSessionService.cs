using System.Security.Cryptography;
using System.Text;
using WebControl.Agent.Windows.Persistence;

namespace WebControl.Agent.Windows.Services;

public sealed class AdminSessionService
{
    public const string CredentialStampClaimType =
        "webcontrol:credential-stamp";

    private readonly AdminUserRepository _repository;

    public AdminSessionService(
        AdminUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<string?> GetCredentialStampAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var user =
            await _repository.GetByUsernameAsync(
                username,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        return CreateCredentialStamp(
            user.PasswordHash);
    }

    public async Task<bool> IsCredentialStampValidAsync(
        string username,
        string? credentialStamp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialStamp))
        {
            return false;
        }

        var expectedStamp =
            await GetCredentialStampAsync(
                username,
                cancellationToken);

        if (expectedStamp is null ||
            expectedStamp.Length != credentialStamp.Length)
        {
            return false;
        }

        var expectedBytes =
            Encoding.ASCII.GetBytes(
                expectedStamp);

        var actualBytes =
            Encoding.ASCII.GetBytes(
                credentialStamp);

        return CryptographicOperations.FixedTimeEquals(
            expectedBytes,
            actualBytes);
    }

    private static string CreateCredentialStamp(
        string passwordHash)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                passwordHash);

        var hash =
            SHA256.HashData(
                bytes);

        return Convert.ToHexString(
            hash);
    }
}
