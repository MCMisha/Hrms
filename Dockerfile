FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["WebAPI/Hrms/Hrms/Hrms.csproj", "WebAPI/Hrms/Hrms/"]
COPY ["NugetPKGs/", "/localnuget/"]

RUN dotnet nuget add source /localnuget \
    --name LocalPackages

RUN dotnet restore "WebAPI/Hrms/Hrms/Hrms.csproj"

COPY . .

WORKDIR "/src/WebAPI/Hrms/Hrms"

RUN dotnet publish "Hrms.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Hrms.dll"]