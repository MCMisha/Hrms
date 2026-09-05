FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["Hrms.CompensationService/Hrms.CompensationService.csproj", "Hrms.CompensationService/"]
COPY ["../Hrms.GrpcContracts/Hrms.GrpcContracts.csproj", "Hrms.GrpcContracts/"]

RUN dotnet restore "Hrms.CompensationService/Hrms.CompensationService.csproj"

COPY . .

WORKDIR "/src/Hrms.CompensationService"

RUN dotnet publish "Hrms.CompensationService.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Hrms.CompensationService.dll"]