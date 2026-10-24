using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Me
{
    [AllowAuthenticated]
    public record DeleteMyGradeCommand(Guid GradeId) : ICommand<Result>;

    public class DeleteMyGradeCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<DeleteMyGradeCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteMyGradeCommand command, CancellationToken cancellationToken)
        {
            var grade = await dbContext.Grades
                .Include(g => g.Exam)
                .SingleOrDefaultAsync(g => g.Id == command.GradeId, cancellationToken);
            if (grade is null)
                return Result.Failure($"Grade with ID '{command.GradeId}' not found");

            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
            if (!myIds.Contains(grade.SchoolUserId))
                return Result.Forbidden();
            if (ManualEdit.RefuseSynced(grade) is { } synced)
                return synced;

            dbContext.Grades.Remove(grade);
            var examIsEmpty = grade.Exam is { Source: null } && !await dbContext.Grades.AnyAsync(g => g.ExamId == grade.ExamId && g.Id != grade.Id, cancellationToken);
            if (examIsEmpty)
                dbContext.Exams.Remove(grade.Exam!);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
