# Use the SDK image to build, run migrations, and publish the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . /src

# Install dotnet-ef tool globally (for running migrations)
RUN dotnet tool install --global dotnet-ef --version 8.0.0

# Ensure the dotnet tools are available in the PATH
ENV PATH="$PATH:/root/.dotnet/tools"

# Apply migrations (dotnet ef)
FROM build AS migrations
WORKDIR /src
RUN dotnet ef database update

# Build and publish the application
FROM build AS publish
RUN dotnet publish "IdentityApiAuth.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the runtime image to run the app (no SDK, only the runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Start the application after migrations
ENTRYPOINT ["dotnet", "IdentityApiAuth.dll"]
