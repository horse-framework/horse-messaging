FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

ARG port=34000
ARG jport=34001
ARG juser="admin"
ARG jpass=""
ARG datapath="/etc/horse/data"

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /src
COPY ["HorseService.csproj", "HorseService/"]
RUN dotnet restore "HorseService/HorseService.csproj"
COPY . ./HorseService/
WORKDIR "/src/HorseService"
RUN dotnet build "HorseService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HorseService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV HORSE_PORT $port
ENV HORSE_JOCKEY_PORT $jport
ENV HORSE_JOCKEY_USERNAME $juser
ENV HORSE_JOCKEY_PASSWORD $jpass
ENV HORSE_DATA_PATH $datapath

EXPOSE $port
EXPOSE $jport

ENTRYPOINT dotnet HorseService.dll