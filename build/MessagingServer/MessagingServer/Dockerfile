FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src
COPY ["MessagingServer.csproj", "MessagingServer/"]
RUN dotnet restore "MessagingServer/MessagingServer.csproj"
COPY . ./MessagingServer/
WORKDIR "/src/MessagingServer"
RUN dotnet build "MessagingServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MessagingServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 2626
EXPOSE 2627
EXPOSE 2628
EXPOSE 2680

ENTRYPOINT ["dotnet", "MessagingServer.dll"]