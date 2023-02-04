#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Acorle.csproj", "."]
RUN dotnet restore "./Acorle.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Acorle.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Acorle.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN rm -rf appsettings.json nlog.config
RUN ln -s /config/appsettings.json /app/appsettings.json
RUN ln -s /config/nlog.config /app/nlog.config
ENTRYPOINT ["dotnet", "Acorle.dll"]
