using Mediator;
using Schuly.Application.Contracts.Authorization;
using Schuly.Application.Contracts.Models;
using Schuly.Domain.Enums;

namespace Schuly.Application.Contracts.Commands.SchoolUser
{
    public record CreateSchoolUserCommand(
        Guid ApplicationUserId,
        Guid SchoolId,
        string FirstName,
        string LastName,
        string Email,
        string? PrivateEmail,
        string? PhoneNumber,
        string? Street,
        string? City,
        string? Zip,
        DateOnly Birthday,
        DateOnly EntryDate,
        Roles Role,
        string? StudentNumber,
        string? TeacherCode) : ICommand<Result<Guid>>, IHasAuthorization
    {
        public Roles GetRequiredRole() => Roles.Administrator;
    }
}
