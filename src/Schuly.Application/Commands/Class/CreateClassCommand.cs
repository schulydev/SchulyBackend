using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Class
{
    [AuthorizedRoles(Roles.Teacher)]
    public record CreateClassCommand(string Name, string? Description, Guid SchoolId) : ICommand<Result>;

    public class CreateClassCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<CreateClassCommand, Result>
    {
        public async ValueTask<Result> Handle(CreateClassCommand command, CancellationToken cancellationToken)
        {
            if (!await dbContext.Schools.AnyAsync(s => s.Id == command.SchoolId, cancellationToken))
                return Result.Failure($"School with ID '{command.SchoolId}' not found");

            List<Domain.Teacher> myTeachers = [];

            if (!userService.IsCurrentUserAdmin())
            {
                var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);
                myTeachers = await dbContext.Teachers
                    .Where(t => t.ApplicationUserId == currentUserId && t.SchoolId == command.SchoolId)
                    .ToListAsync(cancellationToken);

                if (myTeachers.Count == 0)
                    return Result.Forbidden();
            }

            var newClass = new Domain.Class
            {
                Name = command.Name,
                Description = command.Description,
                SchoolId = command.SchoolId
            };

            foreach (var teacher in myTeachers)
                newClass.Teachers.Add(teacher);

            await dbContext.Classes.AddAsync(newClass, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
