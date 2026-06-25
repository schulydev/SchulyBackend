using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Exam
{
    [AuthorizedRoles(Roles.Teacher)]
    public record CreateExamCommand(string Name, string? Description, ExamType Type, Guid ClassId) : ICommand<Result>;

    public class CreateExamCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<CreateExamCommand, Result>
    {
        public async ValueTask<Result> Handle(CreateExamCommand command, CancellationToken cancellationToken)
        {
            if (!await userService.CanManageClassAsync(command.ClassId, cancellationToken))
                return Result.Forbidden();

            await dbContext.Exams.AddAsync(new Domain.Exam
            {
                Name = command.Name,
                Description = command.Description,
                Type = command.Type,
                ClassId = command.ClassId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
