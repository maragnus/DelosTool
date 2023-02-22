FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Delos.ServerManager/Delos.ServerManager.csproj", "Delos.ServerManager/"]
RUN dotnet restore "Delos.ServerManager/Delos.ServerManager.csproj"
COPY . .
WORKDIR "/src/Delos.ServerManager"
RUN dotnet build "Delos.ServerManager.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Delos.ServerManager.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Delos.ServerManager.dll"]
