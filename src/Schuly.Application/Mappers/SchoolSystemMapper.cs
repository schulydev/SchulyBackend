using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class SchoolSystemMapper
    {
        public static SchoolSystemDto ToDto(this SchoolSystem system)
        {
            return new SchoolSystemDto
            {
                Id = system.Id,
                Key = system.Key,
                DisplayName = system.DisplayName,
                LogoUrl = system.LogoUrl,
                SchulwareApiBaseUrl = system.SchulwareApiBaseUrl,
                LoginMethod = system.LoginMethod,
                Enabled = system.Enabled,
                SortOrder = system.SortOrder,
                LoginFields = system.LoginFields.Select(f => f.ToDto()).ToList(),
                CreatedAt = system.CreatedAt,
                UpdatedAt = system.UpdatedAt
            };
        }

        public static List<SchoolSystemDto> ToDto(this List<SchoolSystem> systems)
        {
            return systems.Select(s => s.ToDto()).ToList();
        }

        public static SchoolSystemLoginFieldDto ToDto(this SchoolSystemLoginField field)
        {
            return new SchoolSystemLoginFieldDto
            {
                Key = field.Key,
                Label = field.Label,
                Type = field.Type,
                Placeholder = field.Placeholder,
                DefaultValue = field.DefaultValue,
                Required = field.Required
            };
        }

        public static SchoolSystemLoginField ToEntity(this SchoolSystemLoginFieldDto field)
        {
            return new SchoolSystemLoginField
            {
                Key = field.Key,
                Label = field.Label,
                Type = field.Type,
                Placeholder = field.Placeholder,
                DefaultValue = field.DefaultValue,
                Required = field.Required
            };
        }
    }
}
