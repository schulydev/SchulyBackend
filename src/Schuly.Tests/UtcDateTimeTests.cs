using Microsoft.EntityFrameworkCore;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class UtcDateTimeTests
    {
        [Test]
        public async Task Normalize_on_unspecified_keeps_ticks_and_becomes_utc()
        {
            var value = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Unspecified);
            var normalized = UtcDateTime.Normalize(value);

            await Assert.That(normalized.Ticks).IsEqualTo(value.Ticks);
            await Assert.That(normalized.Kind).IsEqualTo(DateTimeKind.Utc);
        }

        [Test]
        public async Task Normalize_on_local_converts_to_universal_time()
        {
            var value = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Local);
            var normalized = UtcDateTime.Normalize(value);

            await Assert.That(normalized).IsEqualTo(value.ToUniversalTime());
            await Assert.That(normalized.Kind).IsEqualTo(DateTimeKind.Utc);
        }

        [Test]
        public async Task Model_carries_the_converter_for_a_non_nullable_DateTime_property()
        {
            using var ctx = TestDb.NewContext(nameof(Model_carries_the_converter_for_a_non_nullable_DateTime_property));

            var converter = ctx.Model.FindEntityType(typeof(Absence))!.FindProperty(nameof(Absence.From))!.GetValueConverter();

            await Assert.That(converter).IsNotNull();
            var converted = converter!.ConvertToProvider(new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Unspecified));
            await Assert.That(converted).IsTypeOf<DateTime>();
            await Assert.That(((DateTime)converted!).Kind).IsEqualTo(DateTimeKind.Utc);
        }

        [Test]
        public async Task Model_carries_the_converter_for_a_nullable_DateTime_property()
        {
            using var ctx = TestDb.NewContext(nameof(Model_carries_the_converter_for_a_nullable_DateTime_property));

            var converter = ctx.Model.FindEntityType(typeof(AgendaEntry))!.FindProperty(nameof(AgendaEntry.EndDate))!.GetValueConverter();

            await Assert.That(converter).IsNotNull();
            var converted = converter!.ConvertToProvider(new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Unspecified));
            await Assert.That(converted).IsTypeOf<DateTime>();
            await Assert.That(((DateTime)converted!).Kind).IsEqualTo(DateTimeKind.Utc);
        }
    }
}
