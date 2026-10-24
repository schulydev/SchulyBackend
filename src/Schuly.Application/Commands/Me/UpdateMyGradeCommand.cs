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
    public record UpdateMyGradeCommand(Guid GradeId, decimal Score, decimal Weight) : ICommand<Result>;

    public class UpdateMyGradeCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<UpdateMyGradeCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateMyGradeCommand command, CancellationToken cancellationToken)
        {
            if (MyGrade.Invalid(command.Score, command.Weight) is { } invalid)
                return Result.Failure(invalid);

            var grade = await dbContext.Grades.SingleOrDefaultAsync(g => g.Id == command.GradeId, cancellationToken);
            if (grade is null)
                return Result.Failure($"Grade with ID '{command.GradeId}' not found");

            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
            if (!myIds.Contains(grade.SchoolUserId))
                return Result.Forbidden();
            if (ManualEdit.RefuseSynced(grade) is { } synced)
                return synced;

            grade.Score = command.Score;
            grade.Weighting = command.Weight;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
