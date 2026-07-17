# Contexto de build: la carpeta FinanzasIA (raíz de la solución)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FinanzasIA.Core/FinanzasIA.Core.csproj FinanzasIA.Core/
COPY FinanzasIA.Application/FinanzasIA.Application.csproj FinanzasIA.Application/
COPY FinanzasIA.Backoffice/FinanzasIA.Backoffice.csproj FinanzasIA.Backoffice/
RUN dotnet restore FinanzasIA.Backoffice/FinanzasIA.Backoffice.csproj

COPY . .
RUN dotnet publish FinanzasIA.Backoffice/FinanzasIA.Backoffice.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinanzasIA.Backoffice.dll"]
