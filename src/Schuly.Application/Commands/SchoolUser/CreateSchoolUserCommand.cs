using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.SchoolUser
{
    [AuthorizedRoles(Roles.Administrator)]
    public record CreateSchoolUserCommand(Guid ApplicationUserId, Guid SchoolId, string FirstName, string LastName, string Email, string? PrivateEmail, string? PhoneNumber, string? Street, string? City, string? Zip, DateOnly Birthday, DateOnly EntryDate, Roles Role, string? ProfilePictureUrl = null) : ICommand<Result<Guid>>;

    public class CreateSchoolUserCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateSchoolUserCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(CreateSchoolUserCommand command, CancellationToken cancellationToken)
        {
            if (!await dbContext.ApplicationUsers.AnyAsync(au => au.Id == command.ApplicationUserId, cancellationToken))
                return Result<Guid>.Failure($"ApplicationUser with ID '{command.ApplicationUserId}' not found");

            if (!await dbContext.Schools.AnyAsync(s => s.Id == command.SchoolId, cancellationToken))
                return Result<Guid>.Failure($"School with ID '{command.SchoolId}' not found");

            if (await dbContext.SchoolUsers.AnyAsync(su => su.ApplicationUserId == command.ApplicationUserId && su.SchoolId == command.SchoolId && su.Email == command.Email, cancellationToken))
                return Result<Guid>.Conflict("A school user with this email already exists for this account and school");

            var schoolUser = new Domain.SchoolUser
            {
                ApplicationUserId = command.ApplicationUserId,
                SchoolId = command.SchoolId,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Email = command.Email,
                PrivateEmail = command.PrivateEmail,
                PhoneNumber = command.PhoneNumber,
                ProfilePictureUrl = command.ProfilePictureUrl,
                Street = command.Street,
                City = command.City,
                Zip = command.Zip,
                Birthday = command.Birthday,
                EntryDate = command.EntryDate,
                Role = command.Role,
                State = UserState.Active,
            };

            await dbContext.SchoolUsers.AddAsync(schoolUser, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(schoolUser.Id);
        }
    }
}
