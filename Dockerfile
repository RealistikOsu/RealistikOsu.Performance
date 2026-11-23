FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*

COPY osu.ref ./
COPY scripts/fetch-osu.sh scripts/
RUN scripts/fetch-osu.sh

COPY RealistikOsu.Performance/RealistikOsu.Performance.csproj RealistikOsu.Performance/
RUN dotnet restore RealistikOsu.Performance

COPY RealistikOsu.Performance RealistikOsu.Performance
RUN dotnet publish RealistikOsu.Performance -c Release -o /out --no-restore
RUN rm -rf /out/runtimes /out/osu.Game.Resources.dll

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "RealistikOsu.Performance.dll"]
