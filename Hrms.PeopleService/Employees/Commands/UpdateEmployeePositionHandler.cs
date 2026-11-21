using Hrms.PeopleService.Interfaces.Converters;
using Hrms.PeopleService.Models.DTO;
using Hrms.PeopleService.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hrms.PeopleService.Employees.Commands;

public class UpdateEmployeePositionHandler
{
    private readonly PeopleDbContext _dbContext;
    private readonly IEmployeeConverter _employeeConverter;

    public UpdateEmployeePositionHandler(PeopleDbContext dbContext, IEmployeeConverter employeeConverter)
    {
        _dbContext = dbContext;
        _employeeConverter = employeeConverter;
    }

    public async Task<UpdateEmployeePositionResult> HandleAsync(UpdateEmployeePositionCommand command,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees.FirstOrDefaultAsync(emp =>
            emp.Id == command.EmployeeId, cancellationToken);

        if (employee == null)
        {
            return new UpdateEmployeePositionResult(UpdateEmployeePositionStatus.NotFound);
        }

        if (employee.Status != EmployeeStatus.Active)
        {
            return new UpdateEmployeePositionResult(UpdateEmployeePositionStatus.Inactive);
        }

        employee.Position = command.Position;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new UpdateEmployeePositionResult(UpdateEmployeePositionStatus.Updated,
            _employeeConverter.FromEmployeeToResponseDto(employee));
    }
}