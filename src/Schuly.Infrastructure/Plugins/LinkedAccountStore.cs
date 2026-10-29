using Microsoft.EntityFrameworkCore;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;

namespace Schuly.Infrastructure.Plugins
{
    /// <summary>
    /// <see cref="ILinkedAccounts"/> for the host and every plugin: rows in <c>LinkedAccounts</c>,
    /// secrets in the <see cref="VaultNamespace"/> vault under <c>{linkedAccountId}:{name}</c>.
    /// </summary>
    public sealed class LinkedAccountStore(SchulyDbContext db, IPluginVaultFactory vaultFactory) : ILinkedAccounts
    {
        public const string VaultNamespace = "linked-accounts";
        private const string NamesSecret = "__names";

        private IPluginVault Vault => vaultFactory.GetVault(VaultNamespace);

        public async Task<IReadOnlyList<LinkedAccount>> ListAsync(Guid applicationUserId, string provider, CancellationToken cancellationToken = default)
        {
            var accounts = await db.LinkedAccounts
                .AsNoTracking()
                .Where(a => a.ApplicationUserId == applicationUserId && a.Provider == provider)
                .OrderBy(a => a.Email)
                .ToListAsync(cancellationToken);
            return accounts.Select(ToContract).ToList();
        }

        public async Task<LinkedAccount?> GetAsync(Guid applicationUserId, Guid linkedAccountId, CancellationToken cancellationToken = default)
        {
            var account = await FindAsync(applicationUserId, linkedAccountId, cancellationToken);
            return account is null ? null : ToContract(account);
        }

        public async Task<LinkedAccount> SaveAsync(Guid applicationUserId, LinkedAccountRecord account, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(account.Provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(account.ExternalId);
            ArgumentException.ThrowIfNullOrWhiteSpace(account.Email);

            var existing = await db.LinkedAccounts.FirstOrDefaultAsync(a => a.ApplicationUserId == applicationUserId && a.Provider == account.Provider && a.ExternalId == account.ExternalId, cancellationToken);
            if (existing is null)
            {
                existing = new Domain.LinkedAccount
                {
                    ApplicationUserId = applicationUserId,
                    Provider = account.Provider,
                    ExternalId = account.ExternalId,
                    Email = account.Email,
                    DisplayName = account.DisplayName,
                };
                db.LinkedAccounts.Add(existing);
            }
            else
            {
                existing.Email = account.Email;
                existing.DisplayName = account.DisplayName;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (db.Entry(existing).State == EntityState.Added)
            {
                // A concurrent save of the same external account won the unique index: link to its row instead of failing.
                db.Entry(existing).State = EntityState.Detached;
                var winner = await db.LinkedAccounts.FirstOrDefaultAsync(a => a.ApplicationUserId == applicationUserId && a.Provider == account.Provider && a.ExternalId == account.ExternalId, cancellationToken);
                if (winner is null)
                    throw;

                winner.Email = account.Email;
                winner.DisplayName = account.DisplayName;
                winner.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                existing = winner;
            }

            return ToContract(existing);
        }

        public async Task<bool> RemoveAsync(Guid applicationUserId, Guid linkedAccountId, CancellationToken cancellationToken = default)
        {
            var account = await FindAsync(applicationUserId, linkedAccountId, cancellationToken);
            if (account is null)
                return false;

            db.LinkedAccounts.Remove(account);
            await db.SaveChangesAsync(cancellationToken);
            RemoveSecrets(linkedAccountId);
            return true;
        }

        public async Task<string?> GetSecretAsync(Guid applicationUserId, Guid linkedAccountId, string name, CancellationToken cancellationToken = default)
        {
            if (await FindAsync(applicationUserId, linkedAccountId, cancellationToken) is null)
                return null;
            return Vault.Get(Key(linkedAccountId, name));
        }

        public async Task SetSecretAsync(Guid applicationUserId, Guid linkedAccountId, string name, string value, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name == NamesSecret)
                throw new ArgumentException($"'{NamesSecret}' is reserved.", nameof(name));
            if (await FindAsync(applicationUserId, linkedAccountId, cancellationToken) is null)
                throw new InvalidOperationException("Linked account not found for this user.");

            Vault.Set(Key(linkedAccountId, name), value);
            var names = SecretNames(linkedAccountId);
            if (names.Add(name))
                Vault.Set(Key(linkedAccountId, NamesSecret), string.Join('\n', names));
        }

        /// <summary>Deletes every secret stored for the account (used once its row is gone).</summary>
        public void RemoveSecrets(Guid linkedAccountId)
        {
            foreach (var name in SecretNames(linkedAccountId))
                Vault.Remove(Key(linkedAccountId, name));
            Vault.Remove(Key(linkedAccountId, NamesSecret));
        }

        private Task<Domain.LinkedAccount?> FindAsync(Guid applicationUserId, Guid linkedAccountId, CancellationToken cancellationToken) =>
            db.LinkedAccounts.FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.ApplicationUserId == applicationUserId, cancellationToken);

        private HashSet<string> SecretNames(Guid linkedAccountId) =>
            (Vault.Get(Key(linkedAccountId, NamesSecret)) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        private static string Key(Guid linkedAccountId, string name) => $"{linkedAccountId:N}:{name}";

        private static LinkedAccount ToContract(Domain.LinkedAccount a) =>
            new(a.Id, a.Provider, a.ExternalId, a.Email, a.DisplayName, new DateTimeOffset(DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)), new DateTimeOffset(DateTime.SpecifyKind(a.UpdatedAt, DateTimeKind.Utc)));
    }
}
