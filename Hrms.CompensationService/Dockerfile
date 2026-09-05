FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Hrms.CompensationService.csproj", "./"]

RUN dotnet restore "Hrms.CompensationService.csproj"

COPY . .

RUN dotnet publish "Hrms.CompensationService.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Hrms.CompensationService.dll"]