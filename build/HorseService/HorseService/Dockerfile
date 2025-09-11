FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src
COPY ["HorseService/HorseService.csproj", "HorseService/"]
RUN dotnet restore "HorseService/HorseService.csproj"
COPY . ./HorseService/
WORKDIR "/src/HorseService"
RUN dotnet build "./HorseService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./HorseService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 2626
EXPOSE 2627
EXPOSE 2680

ENTRYPOINT ["dotnet", "HorseService.dll"]
