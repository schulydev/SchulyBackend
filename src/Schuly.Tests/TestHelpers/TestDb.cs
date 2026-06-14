using Microsoft.EntityFrameworkCore;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Tests.TestHelpers
{
    public static class TestDb
    {
        public static SchulyDbContext NewContext(string name) =>
            new(new DbContextOptionsBuilder<SchulyDbContext>()
                .UseInMemoryDatabase(name)
                .Options);

        public static SchoolUser NewSchoolUser(Guid schoolId, string last = "Doe") => new()
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = Guid.NewGuid(),
            SchoolId = schoolId,
            FirstName = "Test",
            LastName = last,
            Email = $"{last}@example.com",
            Birthday = new DateOnly(2000, 1, 1),
            EntryDate = new DateOnly(2020, 1, 1),
            Role = Roles.Student,
        };
    }
}
