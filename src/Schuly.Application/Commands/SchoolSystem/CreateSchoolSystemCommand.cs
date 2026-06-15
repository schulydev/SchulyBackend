using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.SchoolSystem
{
    [AuthorizedRoles(Roles.Administrator)]
    public record CreateSchoolSystemCommand(
        string Key,
        string DisplayName,
        string LoginMethod,
        string? LogoUrl = null,
        string? SchulwareApiBaseUrl = null,
        string? StatelessBasePath = null,
        bool Enabled = true,
        int SortOrder = 0,
        List<SchoolSystemLoginFieldDto>? LoginFields = null) : ICommand<Result<Guid>>;

    public class CreateSchoolSystemCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateSchoolSystemCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(CreateSchoolSystemCommand command, CancellationToken cancellationToken)
        {
            var keyExists = await dbContext.SchoolSystems.AnyAsync(s => s.Key == command.Key, cancellationToken);
            if (keyExists)
                return Result<Guid>.Conflict($"A school system with key '{command.Key}' already exists");

            var system = new Domain.SchoolSystem
            {
                Key = command.Key,
                DisplayName = command.DisplayName,
                LoginMethod = command.LoginMethod,
                LogoUrl = command.LogoUrl,
                SchulwareApiBaseUrl = command.SchulwareApiBaseUrl,
                StatelessBasePath = command.StatelessBasePath,
                Enabled = command.Enabled,
                SortOrder = command.SortOrder,
                LoginFields = (command.LoginFields ?? []).Select(f => f.ToEntity()).ToList()
            };

            await dbContext.SchoolSystems.AddAsync(system, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(system.Id);
        }
    }
}
