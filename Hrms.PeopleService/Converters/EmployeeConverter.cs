using Hrms.PeopleService.Interfaces.Converters;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;

namespace Hrms.PeopleService.Converters;

public class EmployeeConverter : IEmployeeConverter
{
    public EmployeeResponseDto FromEmployeeToResponseDto(Employee employee)
    {
        return new EmployeeResponseDto(employee.Id, employee.FirstName, employee.LastName, employee.Email,
            employee.DateOfBirth, employee.Position, employee.Status);
    }
}