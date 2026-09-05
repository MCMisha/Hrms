FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["Hrms.PeopleService/Hrms.PeopleService.csproj", "Hrms.PeopleService/"]
COPY ["../Hrms.GrpcContracts/Hrms.GrpcContracts.csproj", "Hrms.GrpcContracts/"]

RUN dotnet restore "Hrms.PeopleService/Hrms.PeopleService.csproj"

COPY . .

WORKDIR "/src/Hrms.PeopleService"

RUN dotnet publish "Hrms.PeopleService.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Hrms.PeopleService.dll"]