using Mediator;
using Schuly.Application.Contracts.Commands.SchoolUser;
using Schuly.Application.Contracts.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.SchoolUser
{
    public class CreateSchoolUserCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateSchoolUserCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(CreateSchoolUserCommand command, CancellationToken cancellationToken)
        {
            var schoolUser = new Domain.SchoolUser
            {
                ApplicationUserId = command.ApplicationUserId,
                SchoolId = command.SchoolId,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Email = command.Email,
                PrivateEmail = command.PrivateEmail,
                PhoneNumber = command.PhoneNumber,
                Street = command.Street,
                City = command.City,
                Zip = command.Zip,
                Birthday = command.Birthday,
                EntryDate = command.EntryDate,
                Role = command.Role,
                State = UserState.Active,
                StudentNumber = command.StudentNumber,
                TeacherCode = command.TeacherCode
            };

            await dbContext.SchoolUsers.AddAsync(schoolUser, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(schoolUser.Id);
        }
    }
}
