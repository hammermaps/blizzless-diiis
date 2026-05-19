# Build stage – uses full SDK to compile and publish a self-contained Linux binary
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restore dependencies (separate layer for better caching)
COPY ["src/DiIiS-NA/Blizzless.csproj", "src/DiIiS-NA/"]
COPY ["src/DiIiSNet/BZNET.csproj", "src/DiIiSNet/"]
RUN dotnet restore "src/DiIiS-NA/Blizzless.csproj"

# Copy remaining source files and publish a self-contained linux-x64 binary
COPY ["src/", "src/"]
WORKDIR "/app/src/DiIiS-NA"
RUN dotnet publish "Blizzless.csproj" -c Release \
    --runtime linux-x64 \
    --self-contained true \
    -p:DebugSymbols=false \
    -p:DebugType=none \
    -o /app/publish

# Runtime stage – runtime-deps is the minimal base for self-contained .NET apps
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app

# Copy the self-contained publish output
COPY --from=build /app/publish .

# Game ports: Battle-Server (1119), REST (83), Game-Server (1345/2001),
#             Battle WebPort (9800), Game WebPort (9100)
EXPOSE 1119 83 1345 2001 9800 9100

ENTRYPOINT ["./Blizzless"]