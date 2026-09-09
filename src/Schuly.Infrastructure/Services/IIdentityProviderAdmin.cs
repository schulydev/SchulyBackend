namespace Schuly.Infrastructure.Services
{
    public interface IIdentityProviderAdmin
    {
        Task<bool> DeleteUserAsync(string externalId, CancellationToken cancellationToken = default);
    }
}
