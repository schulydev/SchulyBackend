using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Class
{
    [AllowAuthenticated]
    public record GetClassesQuery() : IQuery<Result<List<ClassDto>>>;

    public class GetClassesQueryHandler(SchulyDbContext dbContext, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetClassesQuery, Result<List<ClassDto>>>
    {
        public async ValueTask<Result<List<ClassDto>>> Handle(GetClassesQuery query, CancellationToken cancellationToken)
        {
            var classes = await dbContext.Classes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.Students)
                    .ThenInclude(s => s.Absences)
                .Include(c => c.Students)
                    .ThenInclude(s => s.Grades)
                .Include(c => c.Agenda)
                .Include(c => c.Exams)
                    .ThenInclude(e => e.Grades)
                .ToListAsync(cancellationToken);

            var dtos = classes.ToDto();
            foreach (var student in dtos.SelectMany(c => c.Students))
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<List<ClassDto>>.Success(dtos);
        }
    }
}
