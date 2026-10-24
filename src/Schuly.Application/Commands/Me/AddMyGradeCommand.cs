using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Me
{
    /// <summary>Adds a hand-entered grade for one of the user's own student profiles; the subject and exam are created as needed.</summary>
    [AllowAuthenticated]
    public record AddMyGradeCommand(Guid SchoolUserId, string Subject, string ExamName, decimal Score, decimal Weight, DateOnly? ExamDate) : ICommand<Result<Guid>>;

    public class AddMyGradeCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<AddMyGradeCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(AddMyGradeCommand command, CancellationToken cancellationToken)
        {
            if (MyGrade.Invalid(command.Score, command.Weight) is { } invalid)
                return Result.Failure<Guid>(invalid);
            var subject = command.Subject?.Trim() ?? "";
            var examName = command.ExamName?.Trim() ?? "";
            if (subject.Length is 0 or > 100)
                return Result.Failure<Guid>("Subject must be 1 to 100 characters");
            if (examName.Length is 0 or > 200)
                return Result.Failure<Guid>("Exam name must be 1 to 200 characters");

            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
            if (!myIds.Contains(command.SchoolUserId))
                return Result.Forbidden<Guid>();

            var student = await dbContext.SchoolUsers
                .Include(su => su.Classes)
                .SingleAsync(su => su.Id == command.SchoolUserId, cancellationToken);

            var cls = await dbContext.Classes.FirstOrDefaultAsync(c => c.SchoolId == student.SchoolId && c.Name == subject, cancellationToken);
            if (cls is null)
            {
                cls = new Domain.Class { Name = subject, SchoolId = student.SchoolId };
                dbContext.Classes.Add(cls);
            }
            if (!student.Classes.Contains(cls))
                student.Classes.Add(cls);

            var exam = await dbContext.Exams.FirstOrDefaultAsync(e => e.ClassId == cls.Id && e.Name == examName, cancellationToken);
            if (exam is null)
            {
                exam = new Domain.Exam { Name = examName, Type = ExamType.Classic, Date = command.ExamDate, Class = cls };
                dbContext.Exams.Add(exam);
            }

            var grade = new Domain.Grade { Exam = exam, SchoolUserId = student.Id, Score = command.Score, Weighting = command.Weight };
            dbContext.Grades.Add(grade);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(grade.Id);
        }
    }
}
