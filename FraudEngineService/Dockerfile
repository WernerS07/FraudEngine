# Stage 1: Build the application with the SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


# Copy the project files first
COPY ["FraudEngineService/FraudEngineService.csproj", "FraudEngineService/"]
COPY ["Shared/Shared.csproj", "Shared/"]


COPY /certs/ /usr/local/share/ca-certificates/
RUN cat /usr/local/share/ca-certificates/* >> /etc/ssl/certs/ca-certificates.crt

RUN dotnet restore "FraudEngineService/FraudEngineService.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet publish "FraudEngineService/FraudEngineService.csproj" -c Release -o /app/publish

# Stage 2: Run the application with the smaller runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FraudEngineService.dll"]