using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Behaviors;
using GetMySchoolsQuery = Schuly.Application.Queries.School.GetMySchoolsQuery;

namespace Schuly.Tests
{
    public class AuthorizationBehaviorTests
    {
        [AllowAuthenticated]
        private sealed record AnyAuthenticatedMessage : IMessage;

        private sealed record NoAttributeMessage : IMessage;

        private static ValueTask<string> Next<T>(T _, CancellationToken __) where T : notnull, IMessage
            => ValueTask.FromResult("ok");

        [Test]
        public async Task AllowAuthenticated_message_passes()
        {
            var behavior = new AuthorizationBehavior<AnyAuthenticatedMessage, string>();

            var result = await behavior.Handle(new AnyAuthenticatedMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }

        [Test]
        public async Task Message_without_an_authorization_attribute_throws()
        {
            var behavior = new AuthorizationBehavior<NoAttributeMessage, string>();

            await Assert.That(async () =>
                    await behavior.Handle(new NoAttributeMessage(), Next, CancellationToken.None))
                .Throws<InvalidOperationException>();
        }

        [Test]
        public async Task GetMySchoolsQuery_is_open_to_any_authenticated_user()
        {
            var behavior = new AuthorizationBehavior<GetMySchoolsQuery, string>();

            var result = await behavior.Handle(new GetMySchoolsQuery(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }
    }
}
