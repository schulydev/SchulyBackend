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
    public record AddGradeToExamCommand(Guid ExamId, Guid StudentId, decimal Grade, decimal Weight = 1) : ICommand<Result>;

    public class AddGradeToExamCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<AddGradeToExamCommand, Result>
    {
        public async ValueTask<Result> Handle(AddGradeToExamCommand command, CancellationToken cancellationToken)
        {
            var exam = await dbContext.Exams.AsTracking()
                .SingleOrDefaultAsync(e => e.Id == command.ExamId, cancellationToken);

            if (exam == null)
                return Result.Failure($"Exam with ID '{command.ExamId}' not found");

            if (!await userService.CanManageClassAsync(exam.ClassId, cancellationToken))
                return Result.Forbidden();

            var enrolled = await dbContext.Classes
                .AnyAsync(c => c.Id == exam.ClassId && c.Students.Any(s => s.Id == command.StudentId), cancellationToken);
            if (!enrolled)
                return Result.Failure($"Student '{command.StudentId}' is not enrolled in this exam's class");

            exam.Grades.Add(new Domain.Grade
            {
                ExamId = command.ExamId,
                SchoolUserId = command.StudentId,
                Score = command.Grade,
                Weighting = command.Weight,
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
