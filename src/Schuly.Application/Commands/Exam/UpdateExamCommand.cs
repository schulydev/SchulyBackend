using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Exam
{
    [AuthorizedRoles(Roles.Teacher)]
    public record UpdateExamCommand(Guid ExamId, string Name, string? Description, ExamType Type, Guid ClassId) : ICommand<Result>;

    public class UpdateExamCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<UpdateExamCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateExamCommand command, CancellationToken cancellationToken)
        {
            var exam = await dbContext.Exams
                .SingleOrDefaultAsync(e => e.Id == command.ExamId, cancellationToken);

            if (exam == null)
                return Result.Failure($"Exam with ID '{command.ExamId}' not found");

            if (!await userService.CanManageClassAsync(exam.ClassId, cancellationToken) ||
                !await userService.CanManageClassAsync(command.ClassId, cancellationToken))
                return Result.Forbidden();

            exam.Name = command.Name;
            exam.Description = command.Description;
            exam.Type = command.Type;
            exam.ClassId = command.ClassId;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
