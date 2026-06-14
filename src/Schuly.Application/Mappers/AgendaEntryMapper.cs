using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class AgendaEntryMapper
    {
        public static AgendaEntryDto ToDto(this AgendaEntry agendaEntry)
        {
            return new AgendaEntryDto
            {
                Id = agendaEntry.Id,
                EntryType = agendaEntry.EntryType,
                Title = agendaEntry.Title,
                Description = agendaEntry.Description,
                Place = agendaEntry.Place,
                Date = agendaEntry.Date,
                EndDate = agendaEntry.EndDate,
                ClassId = agendaEntry.ClassId,
                SchoolId = agendaEntry.SchoolId,
                SchoolUserId = agendaEntry.SchoolUserId,
            };
        }

        public static List<AgendaEntryDto> ToDto(this List<AgendaEntry> agendaEntries)
        {
            return agendaEntries.Select(a => a.ToDto()).ToList();
        }
    }
}
