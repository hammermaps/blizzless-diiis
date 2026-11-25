<#
Starts, stops or checks the local D3Offline server without tying up a terminal, for scripts and
coding agents. "start" builds the server, runs it in its own minimized console window and returns
once it is ready. "stop" refuses while a game client is connected unless -Force is given.
The game itself is still started with play.bat.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)][ValidateSet('start', 'stop', 'status')][string]$Action,
    [switch]$NoBuild,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\DiIiS-NA\Blizzless.csproj'
$binDir = Join-Path $root 'src\DiIiS-NA\bin\Debug\net10.0'
$exe = Join-Path $binDir 'Blizzless.exe'
$logRoot = Join-Path $binDir 'logs'
$databasePort = 5432
$readyTimeoutSeconds = 120

function Get-Server { @(Get-Process -Name Blizzless -ErrorAction SilentlyContinue) }

# Established connections to the server from other processes, other than its database connections.
# The server also connects to itself (port 9800); both ends of that are its own sockets.
function Get-ClientConnections([int]$serverId) {
    $owned = @(netstat -ano | ForEach-Object { , (-split $_) } |
        Where-Object { $_.Count -eq 5 -and $_[0] -eq 'TCP' -and $_[4] -eq "$serverId" })
    $localEndpoints = @($owned | ForEach-Object { $_[1] })
    @($owned | Where-Object { $_[3] -eq 'ESTABLISHED' -and $localEndpoints -notcontains $_[2] -and
        -not $_[2].EndsWith(":$databasePort") } | ForEach-Object { $_ -join '  ' })
}

# The server keeps packet.log open for writing, so open it with write sharing.
function Read-NewLines([string]$path, [long]$offset) {
    $stream = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
    try {
        if ($offset -gt $stream.Length) { $offset = 0 } # the target was set to reset on startup
        [void]$stream.Seek($offset, 'Begin')
        (New-Object IO.StreamReader($stream)).ReadToEnd() -split "`r?`n"
    }
    finally { $stream.Dispose() }
}

function Test-Database {
    $client = New-Object Net.Sockets.TcpClient
    try { return $client.ConnectAsync('127.0.0.1', $databasePort).Wait(2000) }
    catch { return $false }
    finally { $client.Dispose() }
}

function Show-Status {
    $servers = Get-Server
    if ($servers.Count -eq 0) { Write-Output 'Server: not running.'; return }
    foreach ($server in $servers) {
        $clients = Get-ClientConnections $server.Id
        Write-Output "Server: running, PID $($server.Id), started $($server.StartTime), $($clients.Count) client connection(s)."
    }
}

switch ($Action) {
    'status' { Show-Status }

    'stop' {
        $servers = Get-Server
        if ($servers.Count -eq 0) { Write-Output 'Server: not running.'; exit 0 }
        foreach ($server in $servers) {
            $clients = Get-ClientConnections $server.Id
            if ($clients.Count -gt 0 -and -not $Force) {
                Write-Output "A game client is connected to PID $($server.Id); stopping now would drop the player without a clean save. Not stopped. Use -Force only if the user agrees."
                $clients | ForEach-Object { Write-Output "  $_" }
                exit 1
            }
            try { Stop-Process -Id $server.Id -Force; $server.WaitForExit(10000) | Out-Null }
            catch {
                Write-Output "Could not stop PID $($server.Id): $($_.Exception.Message) It was probably started by the user or as administrator; ask them to close it."
                exit 1
            }
            Write-Output "Stopped PID $($server.Id)."
        }
    }

    'start' {
        $running = Get-Server
        if ($running.Count -gt 0) {
            Write-Output "Already running (PID $($running[0].Id)). It runs the build it started with; stop it first to load a new build."
            exit 0
        }
        if (-not (Test-Database)) {
            Write-Output "PostgreSQL isn't reachable on 127.0.0.1:$databasePort. Start it with 'docker compose up -d db' from the repo root (Docker Desktop must be running), then try again."
            exit 1
        }
        if (-not $NoBuild) {
            # No restore: Codex's sandbox user can't open the user's NuGet.Config. After a package
            # change, run 'dotnet restore src/DiIiS-NA/Blizzless.csproj' outside the sandbox once.
            & dotnet build $project --no-restore --configuration Debug --nologo --verbosity quiet -consoleLoggerParameters:ErrorsOnly
            if ($LASTEXITCODE -ne 0) { Write-Output 'Build failed; server not started.'; exit 1 }
        }

        # The log folder is named by the minute, and a restart within that minute appends to the
        # same packet.log, so only read what this run adds.
        $previousLength = @{}
        Get-ChildItem $logRoot -Recurse -Filter packet.log -ErrorAction SilentlyContinue |
            ForEach-Object { $previousLength[$_.FullName] = $_.Length }

        $started = Get-Date
        $server = Start-Process -FilePath $exe -WorkingDirectory $binDir -WindowStyle Minimized -PassThru
        $null = $server.Handle # keeps ExitCode available after the process exits
        $deadline = $started.AddSeconds($readyTimeoutSeconds)
        $log = $null
        $lines = @()
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 1
            if (-not $log) {
                $log = Get-ChildItem $logRoot -Recurse -Filter packet.log -ErrorAction SilentlyContinue |
                    Where-Object { $_.LastWriteTime -ge $started } | Select-Object -First 1 -ExpandProperty FullName
            }
            if ($log) {
                $offset = 0
                if ($previousLength.ContainsKey($log)) { $offset = $previousLength[$log] }
                $lines = @(Read-NewLines $log $offset)
            }
            # Program.cs logs this line after every listener is bound.
            if ($lines | Where-Object { $_.Contains('shutdown with') }) {
                $errors = @($lines | Where-Object { $_ -match '\] \[(Error|Fatal)\] ' })
                if ($errors.Count -eq 0) { Write-Output "Server ready: PID $($server.Id). Log: $log"; exit 0 }
                # A clean start logs no errors. One that falls back to the 2.7.4 MPQs, for example,
                # still listens but isn't usable with the 2.8.0 client.
                Write-Output "Server is running (PID $($server.Id)) but logged $($errors.Count) Error/Fatal line(s) during startup. Log: $log"
                $errors | Select-Object -First 20 | ForEach-Object { Write-Output "  $_" }
                exit 1
            }
            if ($server.HasExited) { break }
        }

        if ($server.HasExited) { Write-Output "Server exited during startup (exit code $($server.ExitCode))." }
        else { Write-Output "Server not ready after $readyTimeoutSeconds s; PID $($server.Id) left running." }
        if ($log) {
            Write-Output "Last lines of ${log}:"
            $lines | Select-Object -Last 20 | ForEach-Object { Write-Output "  $_" }
        }
        else { Write-Output "No log appeared under $logRoot. The [PacketLog] file target in config.ini must be enabled." }
        exit 1
    }
}
