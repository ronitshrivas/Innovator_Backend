# Generic multi-stage build for any service in the solution.
# docker-compose passes PROJECT (path to .csproj) and DLL (published entry dll).
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ARG DLL
ENV DLL=${DLL}
# Listen on all interfaces by default; compose overrides the port per service.
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["sh", "-c", "exec dotnet $DLL"]
