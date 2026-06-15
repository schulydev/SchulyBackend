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
    public record UpdateSchoolSystemCommand(
        Guid Id,
        string Key,
        string DisplayName,
        string LoginMethod,
        string? LogoUrl = null,
        string? SchulwareApiBaseUrl = null,
        string? StatelessBasePath = null,
        bool Enabled = true,
        int SortOrder = 0,
        List<SchoolSystemLoginFieldDto>? LoginFields = null) : ICommand<Result>;

    public class UpdateSchoolSystemCommandHandler(SchulyDbContext dbContext) : ICommandHandler<UpdateSchoolSystemCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateSchoolSystemCommand command, CancellationToken cancellationToken)
        {
            var system = await dbContext.SchoolSystems
                .SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

            if (system == null)
                return Result.Failure($"School system with ID {command.Id} not found");

            var keyTaken = await dbContext.SchoolSystems
                .AnyAsync(s => s.Key == command.Key && s.Id != command.Id, cancellationToken);
            if (keyTaken)
                return Result.Conflict($"A school system with key '{command.Key}' already exists");

            system.Key = command.Key;
            system.DisplayName = command.DisplayName;
            system.LoginMethod = command.LoginMethod;
            system.LogoUrl = command.LogoUrl;
            system.SchulwareApiBaseUrl = command.SchulwareApiBaseUrl;
            system.StatelessBasePath = command.StatelessBasePath;
            system.Enabled = command.Enabled;
            system.SortOrder = command.SortOrder;
            system.LoginFields = (command.LoginFields ?? []).Select(f => f.ToEntity()).ToList();

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
