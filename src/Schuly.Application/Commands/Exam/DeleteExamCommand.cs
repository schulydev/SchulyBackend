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
    public record DeleteExamCommand(Guid ExamId) : ICommand<Result>;

    public class DeleteExamCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<DeleteExamCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteExamCommand command, CancellationToken cancellationToken)
        {
            var exam = await dbContext.Exams
                .SingleOrDefaultAsync(e => e.Id == command.ExamId, cancellationToken);

            if (exam == null)
                return Result.Failure($"Exam with ID '{command.ExamId}' not found");

            if (!await userService.CanManageClassAsync(exam.ClassId, cancellationToken))
                return Result.Forbidden();

            dbContext.Exams.Remove(exam);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
