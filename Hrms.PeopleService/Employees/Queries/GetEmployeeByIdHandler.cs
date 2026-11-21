using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace Hrms.PeopleService.Employees.Queries;

public class GetEmployeeByIdHandler
{
    private readonly PeopleDbContext _dbContext;

    public GetEmployeeByIdHandler(PeopleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<EmployeeResponseDto?> HandleAsync(
        GetEmployeeByIdQuery query,
        CancellationToken cancellationToken)
    {
        return _dbContext.Employees
            .Where(employee => employee.Id == query.EmployeeId)
            .Select(employee => new EmployeeResponseDto(
                employee.Id,
                employee.FirstName,
                employee.LastName,
                employee.Email,
                employee.DateOfBirth,
                employee.Position,
                employee.Status))
            .FirstOrDefaultAsync(cancellationToken);
    }
}