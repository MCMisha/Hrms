using System.Threading.Channels;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Services.Background;
using Hrms.PeopleService.Stores;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services
    .AddGrpcClient<CompensationGrpc.CompensationGrpcClient>(options =>
    {
        options.Address = new Uri(
            builder.Configuration["GrpcServices:Compensation"]
            ?? throw new InvalidOperationException(
                "Compensation gRPC address is not configured"));
    });
// Add services to the container.
builder.Services.AddDbContext<PeopleDbContext>();
builder.Services.AddSingleton(
    Channel.CreateUnbounded<ImportJob>());
builder.Services.AddSingleton<ImportJobStore>();

builder.Services.AddHostedService<EmployeeImportBackgroundService>();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<PeopleDbContext>();

    await context.Database.MigrateAsync();
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();