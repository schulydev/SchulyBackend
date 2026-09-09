using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.Teacher
{
    [AuthorizedRoles(Roles.Administrator)]
    public record CreateTeacherCommand(Guid SchoolId, string FirstName, string LastName, string Code, string? Email, Guid? ApplicationUserId = null) : ICommand<Result<Guid>>;

    public class CreateTeacherCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateTeacherCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(CreateTeacherCommand command, CancellationToken cancellationToken)
        {
            if (!await dbContext.Schools.AnyAsync(s => s.Id == command.SchoolId, cancellationToken))
                return Result<Guid>.Failure($"School with ID '{command.SchoolId}' not found");

            if (command.ApplicationUserId is Guid linkId &&
                !await dbContext.ApplicationUsers.AnyAsync(au => au.Id == linkId, cancellationToken))
                return Result<Guid>.Failure($"ApplicationUser with ID '{linkId}' not found");

            if (await dbContext.Teachers.AnyAsync(t => t.SchoolId == command.SchoolId && t.Code == command.Code, cancellationToken))
                return Result<Guid>.Conflict($"A teacher with code '{command.Code}' already exists in this school");

            var teacher = new Domain.Teacher
            {
                SchoolId = command.SchoolId,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Code = command.Code,
                Email = command.Email,
                ApplicationUserId = command.ApplicationUserId
            };

            await dbContext.Teachers.AddAsync(teacher, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(teacher.Id);
        }
    }
}
