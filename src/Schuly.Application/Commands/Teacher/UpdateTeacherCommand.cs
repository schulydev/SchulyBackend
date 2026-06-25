using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.Teacher
{
    [AuthorizedRoles(Roles.Administrator)]
    public record UpdateTeacherCommand(Guid TeacherId, string? FirstName, string? LastName, string? Code, string? Email) : ICommand<Result>;

    public class UpdateTeacherCommandHandler(SchulyDbContext dbContext) : ICommandHandler<UpdateTeacherCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateTeacherCommand command, CancellationToken cancellationToken)
        {
            var teacher = await dbContext.Teachers
                .SingleOrDefaultAsync(t => t.Id == command.TeacherId, cancellationToken);

            if (teacher == null)
                return Result.Failure($"Teacher with ID '{command.TeacherId}' not found");

            if (!string.IsNullOrEmpty(command.FirstName))
                teacher.FirstName = command.FirstName;

            if (!string.IsNullOrEmpty(command.LastName))
                teacher.LastName = command.LastName;

            if (!string.IsNullOrEmpty(command.Code))
                teacher.Code = command.Code;

            if (!string.IsNullOrEmpty(command.Email))
                teacher.Email = command.Email;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
