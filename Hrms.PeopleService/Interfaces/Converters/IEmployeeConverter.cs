using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;

namespace Hrms.PeopleService.Interfaces.Converters;

public interface IEmployeeConverter
{
    EmployeeResponseDto FromEmployeeToResponseDto(Employee employee);
}