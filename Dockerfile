# Build stage – uses full SDK to compile and publish a self-contained Linux binary
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# CascLib is built from source so the Linux server reads the installed 2.8.0 game data
# instead of falling back to the bundled 2.7.4 MPQ archives.
RUN apt-get update && apt-get install -y --no-install-recommends cmake g++ make patch \
    && rm -rf /var/lib/apt/lists/*
COPY ["tools/casclib-read-only.patch", "/opt/casclib-read-only.patch"]
COPY ["external/CascLib", "/opt/CascLib"]
RUN cd /opt/CascLib \
    && (patch -p1 --dry-run --reverse < /opt/casclib-read-only.patch > /dev/null 2>&1 \
        || patch -p1 < /opt/casclib-read-only.patch) \
    && cmake -S . -B build -DCMAKE_BUILD_TYPE=Release \
    && cmake --build build -j"$(nproc)"

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
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

# Copy the self-contained publish output
COPY --from=build /app/publish .
# Linux CascLib so the CASC path works inside the container
COPY --from=build /opt/CascLib/build/libcasc.so /app/libcasc.so

# Game ports: Battle-Server (1119), REST (83), Game-Server (1345/2001),
#             Battle WebPort (9800), Game WebPort (9100)
EXPOSE 1119 83 1345 2001 9800 9100

ENTRYPOINT ["./Blizzless"]