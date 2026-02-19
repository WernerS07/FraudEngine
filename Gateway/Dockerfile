# Stage 1: Build the application with the SDK image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


# Copy the project files first
COPY ["Gateway/Gateway.csproj", "Gateway/"]


#COPY /certs/ /usr/local/share/ca-certificates/
#RUN cat /usr/local/share/ca-certificates/* >> /etc/ssl/certs/ca-certificates.crt

RUN dotnet restore "Gateway/Gateway.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet publish "Gateway/Gateway.csproj" -c Release -o /app/publish

# Stage 2: Run the application with the smaller runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.dll"]