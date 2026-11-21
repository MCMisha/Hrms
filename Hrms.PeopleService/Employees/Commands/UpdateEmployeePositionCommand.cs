namespace Hrms.PeopleService.Employees.Commands;

public sealed record UpdateEmployeePositionCommand(int EmployeeId, string Position);