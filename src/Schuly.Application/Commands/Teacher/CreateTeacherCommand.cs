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
            if (command.ApplicationUserId is Guid linkId &&
                !await dbContext.ApplicationUsers.AnyAsync(au => au.Id == linkId, cancellationToken))
                return Result<Guid>.Failure($"ApplicationUser with ID '{linkId}' not found");

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
