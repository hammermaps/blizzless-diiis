# Blizzless D3 – REST API Reference

The built-in REST server exposes a small HTTP API on the same socket already used for Battle.net authentication. All API responses use **JSON** (`application/json`).

> **Base URL** `http://<host>:<port>/api/v1`  
> Port is set via `PORT` in the `[REST]` section of `config.ini` (template default: `83`; code fallback when not configured: `8081`)

---

## Configuration

Add or adjust the following keys in the `[REST]` section of `config.ini`:

```ini
[REST]
IP       = 127.0.0.1
PORT     = 83
ApiKey   = your-strong-secret-here   ; required to use POST /command
```

> ⚠️ **Security notice**: Leave `ApiKey` empty to disable the command endpoint entirely.  
> Use a long, random string in production environments.

---

## Endpoints

### 1. Server Status

```
GET /api/v1/status
```

Returns general server information and runtime statistics.

**Response `200 OK`**

```json
{
  "status": "online",
  "version": "2.7.4.84161",
  "build": 30,
  "stage": 3,
  "type": "Beta",
  "uptime_seconds": 3724,
  "online_players": 5,
  "in_game_players": 3
}
```

| Field            | Type   | Description                                      |
|------------------|--------|--------------------------------------------------|
| `status`         | string | Always `"online"` when the server responds       |
| `version`        | string | Diablo III client version string                 |
| `build`          | int    | Internal server build number                     |
| `stage`          | int    | Internal server stage                            |
| `type`           | string | Build type (e.g. `Alpha`, `Beta`, `Release`)     |
| `uptime_seconds` | int    | Seconds since server start                       |
| `online_players` | int    | Total connected players                          |
| `in_game_players`| int    | Players currently inside a game world            |

---

### 2. List Online Players

```
GET /api/v1/players
```

Returns all currently connected players.

**Response `200 OK`**

```json
{
  "count": 2,
  "players": [
    {
      "battle_tag": "Hero#1234",
      "user_level": "User",
      "in_game": true
    },
    {
      "battle_tag": "Admin#0001",
      "user_level": "GM",
      "in_game": false
    }
  ]
}
```

| Field        | Type   | Description                                               |
|--------------|--------|-----------------------------------------------------------|
| `count`      | int    | Number of online players                                  |
| `players`    | array  | List of player objects (see fields below)                 |
| `battle_tag` | string | Player's BattleTag (`Name#XXXX`)                         |
| `user_level` | string | Account privilege level (`User`, `Tester`, `GM`, `Owner`)|
| `in_game`    | bool   | `true` if the player is currently in a game world        |

---

### 3. Get Player by BattleTag or E-mail

```
GET /api/v1/players/{identifier}
```

`{identifier}` can be a BattleTag (e.g. `Hero%231234` – `#` URL-encoded) or the player's e-mail address.

**Response `200 OK`**

```json
{
  "battle_tag": "Hero#1234",
  "user_level": "User",
  "in_game": true
}
```

**Response `404 Not Found`** (player is offline or does not exist)

```json
{
  "success": false,
  "output": "Player not found."
}
```

---

### 4. Send a Server Command

```
POST /api/v1/command
```

Executes a console command on the server as the server operator.  
Requires the `X-Api-Key` header to match the `ApiKey` value in `config.ini`.

**Request headers**

| Header        | Required | Description                        |
|---------------|----------|------------------------------------|
| `X-Api-Key`   | ✅ Yes   | API key configured in `config.ini` |
| `Content-Type`| No       | `application/json` recommended     |

**Request body**

```json
{
  "command": "!players"
}
```

| Field     | Type   | Description                                                       |
|-----------|--------|-------------------------------------------------------------------|
| `command` | string | Full command string including the command prefix (default `!`)    |

**Response `200 OK`**

```json
{
  "success": true,
  "output": "Online players: Hero#1234, Admin#0001"
}
```

**Response `401 Unauthorized`** (wrong or missing key)

```json
{
  "success": false,
  "output": "Invalid or missing API key."
}
```

**Response `401 Unauthorized`** (endpoint disabled – no `ApiKey` configured)

```json
{
  "success": false,
  "output": "Command endpoint is disabled: no ApiKey configured."
}
```

**Response `400 Bad Request`** (missing command)

```json
{
  "success": false,
  "output": "Missing or empty 'command' field."
}
```

---

## Usage Examples

### cURL

```bash
# Server status
curl http://127.0.0.1:83/api/v1/status

# List online players
curl http://127.0.0.1:83/api/v1/players

# Get a specific player (BattleTag – encode # as %23)
curl "http://127.0.0.1:83/api/v1/players/Hero%231234"

# Get a specific player by e-mail
curl "http://127.0.0.1:83/api/v1/players/admin%40example.com"

# Send a server command
curl -X POST http://127.0.0.1:83/api/v1/command \
  -H "X-Api-Key: your-strong-secret-here" \
  -H "Content-Type: application/json" \
  -d '{"command":"!players"}'
```

### PowerShell

```powershell
# Server status
Invoke-RestMethod -Uri "http://127.0.0.1:83/api/v1/status"

# List online players
Invoke-RestMethod -Uri "http://127.0.0.1:83/api/v1/players"

# Send a server command
$headers = @{ "X-Api-Key" = "your-strong-secret-here" }
$body    = '{"command":"!account show admin@"}'
Invoke-RestMethod -Uri "http://127.0.0.1:83/api/v1/command" `
                  -Method POST -Headers $headers -Body $body `
                  -ContentType "application/json"
```

### Python (requests)

```python
import requests

BASE = "http://127.0.0.1:83/api/v1"
API_KEY = "your-strong-secret-here"

# Server status
status = requests.get(f"{BASE}/status").json()
print(status)

# List players
players = requests.get(f"{BASE}/players").json()
for p in players["players"]:
    print(p["battle_tag"], "– in game:", p["in_game"])

# Send command
resp = requests.post(
    f"{BASE}/command",
    headers={"X-Api-Key": API_KEY},
    json={"command": "!players"}
)
print(resp.json())
```

---

## Error Reference

| HTTP Status | Meaning                                              |
|-------------|------------------------------------------------------|
| `200 OK`    | Request succeeded                                    |
| `400 Bad Request` | Malformed request body or missing required field |
| `401 Unauthorized` | Missing, empty, or incorrect `X-Api-Key`       |
| `404 Not Found` | Endpoint or resource does not exist              |

---

*For available server commands see [commands-list.md](commands-list.md).*
