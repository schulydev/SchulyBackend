using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.Teacher
{
    [AuthorizedRoles(Roles.Administrator)]
    public record DeleteTeacherCommand(Guid TeacherId) : ICommand<Result>;

    public class DeleteTeacherCommandHandler(SchulyDbContext dbContext) : ICommandHandler<DeleteTeacherCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteTeacherCommand command, CancellationToken cancellationToken)
        {
            var teacher = await dbContext.Teachers
                .SingleOrDefaultAsync(t => t.Id == command.TeacherId, cancellationToken);

            if (teacher == null)
                return Result.Failure($"Teacher with ID '{command.TeacherId}' not found");

            dbContext.Teachers.Remove(teacher);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
