using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.User
{
    [AllowAuthenticated]
    public record ExportCurrentUserQuery() : IQuery<Result<AccountExportDto>>;

    public class ExportCurrentUserQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<ExportCurrentUserQuery, Result<AccountExportDto>>
    {
        public async ValueTask<Result<AccountExportDto>> Handle(ExportCurrentUserQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var user = await dbContext.ApplicationUsers
                .AsNoTracking()
                .Include(u => u.SchoolUsers)
                .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                return Result<AccountExportDto>.Failure("User not found");

            var schoolUsers = await dbContext.SchoolUsers
                .AsNoTracking()
                .Include(su => su.School)
                .Include(su => su.Absences)
                .Include(su => su.Grades)
                .Include(su => su.Classes)
                .Where(su => su.ApplicationUserId == userId)
                .ToListAsync(cancellationToken);

            var schoolUserIds = schoolUsers.Select(su => su.Id).ToList();

            var agendaEntries = await dbContext.AgendaEntries
                .AsNoTracking()
                .Where(ae => ae.SchoolUserId != null && schoolUserIds.Contains(ae.SchoolUserId.Value))
                .ToListAsync(cancellationToken);

            var semesterReports = await dbContext.SemesterReports
                .AsNoTracking()
                .Include(r => r.Subjects)
                .Where(r => schoolUserIds.Contains(r.SchoolUserId))
                .ToListAsync(cancellationToken);

            var documents = await dbContext.StudentDocuments
                .AsNoTracking()
                .Where(d => schoolUserIds.Contains(d.SchoolUserId))
                .ToListAsync(cancellationToken);

            var export = new AccountExportDto
            {
                ExportedAt = DateTime.UtcNow,
                Profile = user.ToDto(),
                SchoolUsers = schoolUsers.ToDto(),
                AgendaEntries = agendaEntries.ToDto(),
                SemesterReports = semesterReports.ToDto(),
                Documents = documents.ToDto(),
            };

            return Result<AccountExportDto>.Success(export);
        }
    }
}
