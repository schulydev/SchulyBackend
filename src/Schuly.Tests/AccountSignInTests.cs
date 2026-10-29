using Microsoft.Extensions.Configuration;
using Schuly.Application.Commands.User;
using Schuly.Domain;
using Schuly.Infrastructure.Services;
using Schuly.Tests.TestHelpers;
using Toamaisutaa.Abstractions;

namespace Schuly.Tests
{
    public class AccountSignInTests
    {
        private static IConfiguration Config(params string[] administrators) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(administrators.Select((a, i) => new KeyValuePair<string, string?>($"Auth:Administrators:{i}", a)))
                .AddInMemoryCollection([new KeyValuePair<string, string?>("Avatar:SigningKey", "test-signing-key")])
                .Build();

        [Test]
        public async Task Configured_email_or_username_gets_the_administrator_role()
        {
            var db = TestDb.NewContext(Guid.NewGuid().ToString());
            var admin = new ToamaisutaaUser { Id = Guid.NewGuid(), Email = "admin@example.com", UserName = "someone" };
            db.Set<ToamaisutaaPasswordCredential>().Add(Credential(admin, confirmed: true));
            await db.SaveChangesAsync();
            var provider = new UserRoleProvider(Config("Admin@Example.com", "opsuser"), db);

            var byEmail = await provider.GetRolesAsync(admin);
            var byName = await provider.GetRolesAsync(new ToamaisutaaUser { Id = Guid.NewGuid(), Email = "ops@example.com", UserName = "OpsUser" });
            var other = await provider.GetRolesAsync(new ToamaisutaaUser { Id = Guid.NewGuid(), Email = "student@example.com", UserName = "student" });

            await Assert.That(byEmail).Contains("Administrator");
            await Assert.That(byName).Contains("Administrator");
            await Assert.That(other).IsEmpty();
        }

        [Test]
        public async Task An_unconfirmed_administrator_email_gets_no_role()
        {
            var db = TestDb.NewContext(Guid.NewGuid().ToString());
            var squatter = new ToamaisutaaUser { Id = Guid.NewGuid(), Email = "admin@example.com", UserName = "squatter" };
            db.Set<ToamaisutaaPasswordCredential>().Add(Credential(squatter, confirmed: false));
            await db.SaveChangesAsync();
            var provider = new UserRoleProvider(Config("admin@example.com"), db);

            await Assert.That(await provider.GetRolesAsync(squatter)).IsEmpty();
        }

        [Test]
        public async Task A_provider_only_account_with_the_administrator_email_gets_the_role()
        {
            var db = TestDb.NewContext(Guid.NewGuid().ToString());
            var provider = new UserRoleProvider(Config("admin@example.com"), db);

            var roles = await provider.GetRolesAsync(new ToamaisutaaUser { Id = Guid.NewGuid(), Email = "admin@example.com" });

            await Assert.That(roles).Contains("Administrator");
        }

        private static ToamaisutaaPasswordCredential Credential(ToamaisutaaUser user, bool confirmed) => new()
        {
            UserId = user.Id,
            UserName = user.UserName!,
            NormalizedUserName = user.UserName!.ToUpperInvariant(),
            Email = user.Email,
            NormalizedEmail = user.Email?.ToUpperInvariant(),
            EmailConfirmedAt = confirmed ? DateTimeOffset.UtcNow : null,
            PasswordHash = "x",
        };

        [Test]
        public async Task Avatar_upload_replaces_the_previous_picture()
        {
            var db = TestDb.NewContext(Guid.NewGuid().ToString());
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = "ext", Email = "u@example.com" };
            db.ApplicationUsers.Add(user);
            await db.SaveChangesAsync();
            var storage = new InMemoryDocumentStorage();
            var handler = new SetMyAvatarCommandHandler(db, new FakeUserService(false) { CurrentUserId = user.Id }, storage);

            await handler.Handle(new SetMyAvatarCommand(new MemoryStream([1, 2, 3]), "image/png", 3), default);
            var first = user.ProfilePictureUrl;
            var result = await handler.Handle(new SetMyAvatarCommand(new MemoryStream([4, 5]), "image/jpeg", 2), default);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(user.ProfilePictureUrl).IsNotEqualTo(first);
            await Assert.That(storage.Objects.Keys).DoesNotContain(first!);
            await Assert.That(storage.Objects[user.ProfilePictureUrl!]).IsEquivalentTo(new byte[] { 4, 5 });
        }

        [Test]
        public async Task Avatar_upload_refuses_large_files_and_other_types()
        {
            var db = TestDb.NewContext(Guid.NewGuid().ToString());
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = "ext", Email = "u@example.com" };
            db.ApplicationUsers.Add(user);
            await db.SaveChangesAsync();
            var storage = new InMemoryDocumentStorage();
            var handler = new SetMyAvatarCommandHandler(db, new FakeUserService(false) { CurrentUserId = user.Id }, storage);

            var tooLarge = await handler.Handle(new SetMyAvatarCommand(new MemoryStream([1]), "image/png", SetMyAvatarCommandHandler.MaxBytes + 1), default);
            var notAnImage = await handler.Handle(new SetMyAvatarCommand(new MemoryStream([1]), "image/svg+xml", 1), default);

            await Assert.That(tooLarge.IsSuccess).IsFalse();
            await Assert.That(notAnImage.IsSuccess).IsFalse();
            await Assert.That(storage.Objects).IsEmpty();
            await Assert.That(user.ProfilePictureUrl).IsNull();
        }

        [Test]
        public async Task Signed_avatar_urls_do_not_cross_between_school_users_and_accounts()
        {
            var signer = new AvatarUrlSigner(Config());
            var id = Guid.NewGuid();

            var userUrl = signer.ToPublicUserUrl(id, "avatars/key")!;
            var query = System.Web.HttpUtility.ParseQueryString(new Uri("http://x" + userUrl).Query);
            var exp = long.Parse(query["exp"]!);
            var sig = query["sig"]!;

            await Assert.That(userUrl).StartsWith($"/api/avatars/users/{id}");
            await Assert.That(signer.VerifyUser(id, exp, sig)).IsTrue();
            await Assert.That(signer.Verify(id, exp, sig)).IsFalse();
            await Assert.That(signer.VerifyUser(Guid.NewGuid(), exp, sig)).IsFalse();
        }
    }
}
