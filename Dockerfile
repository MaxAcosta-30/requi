# API para Render. Build desde la raíz del repo: docker build -t requi-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Requi.slnx ./
COPY src/ src/
RUN dotnet publish src/Requi.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render envía el tráfico al puerto 10000 por defecto.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "Requi.Api.dll"]
