using System.Globalization;
using System.Text.RegularExpressions;
using DataPipeline.Core.Interfaces;
using DataPipeline.Core.Models;
using Hrms.Models;
using Hrms.Models.Enums;

namespace Hrms.Mappers;

public class EmployeeMapper : IRecordMapper<Employee>
{
    private const string EmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";

    public MappingResult<Employee> Map(RawRecord record)
    {
        var errors = new List<MappingError>();

        var firstNameRaw = record.GetValueOrDefault("FirstName");
        string firstName = string.Empty;


        if (string.IsNullOrWhiteSpace(firstNameRaw))
        {
            errors.Add(new MappingError(
                "FirstName",
                "FirstName is required",
                firstNameRaw));
        }
        else if (firstNameRaw.Length > 100)
        {
            errors.Add(new MappingError(
                "FirstName",
                "FirstName must be less than 100 characters",
                firstNameRaw));
        }
        else
        {
            firstName = firstNameRaw;
        }

        var lastNameRaw = record.GetValueOrDefault("LastName");
        string lastName = string.Empty;
        if (string.IsNullOrWhiteSpace(lastNameRaw))
        {
            errors.Add(new MappingError(
                "LastName",
                "LastName is required",
                lastNameRaw));
        }
        else if (lastNameRaw.Length > 100)
        {
            errors.Add(new MappingError(
                "LastName",
                "LastName must be less than 100 characters",
                lastNameRaw));
        }
        else
        {
            lastName = lastNameRaw;
        }

        var dateBirthRaw = record.GetValueOrDefault("DateOfBirth");
        DateTime dateOfBirth = default;
        if (string.IsNullOrWhiteSpace(dateBirthRaw))
        {
            errors.Add(new MappingError("DateOfBirth",
                "Date of birth is required",
                dateBirthRaw));
        }
        else if (!DateTime.TryParseExact(
                     dateBirthRaw,
                     "yyyy-MM-dd",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None,
                     out dateOfBirth))
        {
            errors.Add(new MappingError(
                "DateOfBirth",
                "DateOfBirth must have format yyyy-MM-dd",
                dateBirthRaw));
        }
        else if (dateOfBirth.Date > DateTime.Today)
        {
            errors.Add(new MappingError(
                "DateOfBirth",
                "DateOfBirth cannot be in the future",
                dateBirthRaw));
        }
        var emailRaw = record.GetValueOrDefault("Email");
        string email = string.Empty;
        if (string.IsNullOrWhiteSpace(emailRaw))
        {
            errors.Add(new MappingError(
                "Email",
                "Email is required",
                emailRaw));
        }
        else if (emailRaw.Length > 255)
        {
            errors.Add(new MappingError(
                "Email",
                "Email must be less than 256 characters",
                emailRaw));
        }
        else if (!Regex.IsMatch(emailRaw, EmailPattern))
        {
            errors.Add(new MappingError(
                "Email",
                "Email is invalid",
                emailRaw));
        }
        else
        {
            email = emailRaw;
        }
        
        var position = record.GetValueOrDefault("Position");
        if (string.IsNullOrWhiteSpace(position))
        {
            errors.Add(new MappingError(
                "Age",
                "Age is required",
                position));
        }
        
        decimal salary = default;
        var salaryText = record.GetValueOrDefault("Salary");
        if (string.IsNullOrEmpty(salaryText))
        {
            errors.Add(new MappingError(
                "Salary",
                "Salary is required",
                salaryText));
        }
        else if (!decimal.TryParse(
                     salaryText,
                     NumberStyles.Number,
                     CultureInfo.InvariantCulture,
                     out salary))
        {
            errors.Add(new MappingError(
                "Salary",
                "Salary must be a decimal",
                salaryText));
        }
        else if (salary < 0)
        {
            errors.Add(new MappingError(
                "Salary",
                "Salary cannot be less than zero",
                salaryText));
        }

        var statusText = record.GetValueOrDefault("Status");
        EmployeeStatus status = EmployeeStatus.Active;
        if (string.IsNullOrEmpty(statusText))
        {
            errors.Add(new MappingError(
                "Status",
                "Status is required",
                statusText));
        }
        else if (!Enum.TryParse(statusText, out status))
        {
            errors.Add(new MappingError(
                "Status",
                "Status is invalid",
                statusText));
        }

        if (errors.Any())
        {
            return MappingResult<Employee>.Failure(record.RowNumber, errors.ToArray());
        }

        return MappingResult<Employee>.Success(new Employee(0, firstName, lastName,dateOfBirth, email, position ?? "Intern", salary, status));
    }
}