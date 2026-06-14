using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class AbsenceMapper
    {
        public static AbsenceDto ToDto(this Absence absence)
        {
            return new AbsenceDto
            {
                Id = absence.Id,
                Reason = absence.Reason,
                Type = absence.Type,
                From = absence.From,
                Until = absence.Until,
                SchoolUserId = absence.SchoolUserId,
                SchoolId = absence.SchoolUser?.SchoolId
            };
        }

        public static List<AbsenceDto> ToDto(this List<Absence> absences)
        {
            return absences.Select(a => a.ToDto()).ToList();
        }
    }
}
