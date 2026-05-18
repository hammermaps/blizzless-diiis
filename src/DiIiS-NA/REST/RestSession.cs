using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DiIiS_NA.REST.Http;
using DiIiS_NA.REST.Extensions;
using DiIiS_NA.REST.Data.Authentication;
using DiIiS_NA.REST.JSON;
using DiIiS_NA.LoginServer.AccountsSystem;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Web;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.REST.Data.Forms;
using DiIiS_NA.REST.Manager;
using DiIiS_NA.REST.Data.Api;
using DiIiS_NA.LoginServer.Battle;
using DiIiS_NA.GameServer.CommandManager;

namespace DiIiS_NA.REST
{
    public class RestSession : SocketBase
    {
        public static bool ToGet = false;
        public static int b = 0;
        private static readonly Core.Logging.Logger Logger = Core.Logging.LogManager.CreateLogger();
        private readonly GameBitBuffer _incomingBuffer = new GameBitBuffer(ushort.MaxValue);
        private object _bufferLock = new object(); 


        public RestSession(Socket socket) : base(socket) { }

        public override void ReadHandler(int transferredBytes)
        {
            byte[] a = GetReceiveBuffer();
            var httpRequest = HttpHelper.ParseRequest(GetReceiveBuffer(), transferredBytes);
            if (httpRequest == null)
            {
                return;
            }
            else
            {
                Logger.Debug($"$[yellow]$REST Request: $[/]$ {httpRequest.Method.SafeAnsi()} {httpRequest.Path.SafeAnsi()}");
                if (httpRequest.Path == "200")
                {

                }
                else if (httpRequest.Path.Contains("/client/alert"))
                {
                    HandleInfoRequest(httpRequest);
                }
                else if (httpRequest.Path.Contains("/battlenet/login"))
                {
                    switch (httpRequest.Method)
                    {
                        default:
                            HandleConnectRequest(httpRequest);
                            break;
                        case "POST":
                            HandleLoginRequest(httpRequest);
                            return;
                    }
                }
                else if (httpRequest.Path.StartsWith("/api/v1/"))
                {
                    HandleApiRequest(httpRequest);
                }
                else
                {
                    #if DEBUG
                    Logger.Info($"$[red]$404 - REST Request: $[/]$ {httpRequest.Method.SafeAnsi()} {httpRequest.Path.SafeAnsi()}");
                    SendResponseHtml(HttpCode.NotFound, "404 Not Found");
                    #else
                    // sends 502 Bad Gateway to the client to prevent the client from trying to connect to the server again - in case it's a crawler or bad bot.
                    Logger.Info($"$[red]$[404/502] REST Request: $[/]$ {httpRequest.Method.SafeAnsi()} {httpRequest.Path.SafeAnsi()}");
                    SendResponseHtml(HttpCode.BadGateway, "502 Bad Gateway");
                    return;
                    #endif
                }
            }
            AsyncRead();
        }

        void HandleConnectRequest(HttpHeader request)
        {
            SendResponse(HttpCode.OK, SessionManager.Instance.GetFormInput());
        }

        void HandleInfoRequest(HttpHeader request)
        {
            SendResponseHtml(HttpCode.OK, "Welcome to BlizzLess.Net" + 
                                          "\nBuild " + Program.BUILD +
                                          "\nSupport: 2.7.4");
        }

        // ──────────────────────────────────────────────────────────────────────
        // REST API v1
        // ──────────────────────────────────────────────────────────────────────

        void HandleApiRequest(HttpHeader request)
        {
            // Strip query string for routing
            var cleanPath = request.Path.Contains('?')
                ? request.Path.Substring(0, request.Path.IndexOf('?'))
                : request.Path;
            var pathSegments = cleanPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // pathSegments: ["api", "v1", <endpoint>, ...]
            if (pathSegments.Length < 3)
            {
                SendResponseJson(HttpCode.NotFound, new CommandResponse { Success = false, Output = "Not found." });
                return;
            }

            var endpoint = pathSegments[2].ToLowerInvariant();

            switch (endpoint)
            {
                case "status" when request.Method == "GET":
                    HandleApiStatus();
                    break;

                case "players" when request.Method == "GET":
                    if (pathSegments.Length >= 4)
                    {
                        string identifier;
                        try
                        {
                            identifier = Uri.UnescapeDataString(pathSegments[3]);
                        }
                        catch (ArgumentException)
                        {
                            SendResponseJson(HttpCode.BadRequest,
                                new CommandResponse { Success = false, Output = "Invalid URL encoding in player identifier." });
                            return;
                        }
                        HandleApiPlayerInfo(identifier);
                    }
                    else
                        HandleApiPlayerList();
                    break;

                case "stats" when request.Method == "GET":
                    HandleApiStats();
                    break;

                case "leaderboard" when request.Method == "GET":
                    HandleApiLeaderboard(pathSegments, request.Path);
                    break;

                case "command" when request.Method == "POST":
                    HandleApiCommand(request);
                    break;

                default:
                    SendResponseJson(HttpCode.NotFound, new CommandResponse { Success = false, Output = "Endpoint not found." });
                    break;
            }
        }

        void HandleApiStatus()
        {
            var uptime = DateTime.Now - Program.StartupTime;
            int onlineCount, inGameCount;
            lock (PlayerManager.OnlinePlayers)
            {
                onlineCount = PlayerManager.OnlinePlayers.Count;
                inGameCount = PlayerManager.OnlinePlayers.Count(p => p.InGameClient?.Player?.World != null);
            }
            var response = new ServerStatusResponse
            {
                Status = "online",
                Version = "2.7.4.84161",
                Build = Program.BUILD,
                Stage = Program.STAGE,
                Type = Program.TypeBuild.ToString(),
                UptimeSeconds = (long)uptime.TotalSeconds,
                OnlinePlayers = onlineCount,
                InGamePlayers = inGameCount
            };
            SendResponseJson(HttpCode.OK, response);
        }

        // GET /api/v1/stats
        void HandleApiStats()
        {
            var stats = ServerStatsManager.GetServerStats();
            SendResponseJson(HttpCode.OK, stats);
        }

        // GET /api/v1/leaderboard[/<category>[?limit=N]]
        void HandleApiLeaderboard(string[] pathSegments, string fullPath)
        {
            // Parse optional ?limit= query parameter
            int limit = 10;
            if (fullPath.Contains('?'))
            {
                var query = fullPath.Substring(fullPath.IndexOf('?') + 1);
                foreach (var part in query.Split('&'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && kv[0].Equals("limit", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(HttpUtility.UrlDecode(kv[1]), out int parsed))
                        limit = parsed;
                }
            }
            limit = Math.Max(1, Math.Min(limit, 100));

            // pathSegments: ["api","v1","leaderboard", optional category]
            var category = pathSegments.Length >= 4
                ? pathSegments[3].ToLowerInvariant()
                : string.Empty;

            switch (category)
            {
                case "kills":
                    SendResponseJson(HttpCode.OK, ServerStatsManager.GetKillsLeaderboard(limit));
                    break;
                case "playtime":
                    SendResponseJson(HttpCode.OK, ServerStatsManager.GetPlaytimeLeaderboard(limit));
                    break;
                case "level":
                    SendResponseJson(HttpCode.OK, ServerStatsManager.GetLevelLeaderboard(limit));
                    break;
                case "elites":
                    SendResponseJson(HttpCode.OK, ServerStatsManager.GetElitesLeaderboard(limit));
                    break;
                case "rifts":
                    SendResponseJson(HttpCode.OK, ServerStatsManager.GetRiftLeaderboard(limit));
                    break;
                default:
                    // No category or unknown – return all leaderboards in one response
                    var all = new CombinedLeaderboardResponse
                    {
                        Kills    = ServerStatsManager.GetKillsLeaderboard(limit),
                        Playtime = ServerStatsManager.GetPlaytimeLeaderboard(limit),
                        Level    = ServerStatsManager.GetLevelLeaderboard(limit),
                        Elites   = ServerStatsManager.GetElitesLeaderboard(limit),
                        Rifts    = ServerStatsManager.GetRiftLeaderboard(limit)
                    };
                    SendResponseJson(HttpCode.OK, all);
                    break;
            }
        }

        void HandleApiPlayerList()
        {
            List<PlayerInfoResponse> players;
            lock (PlayerManager.OnlinePlayers)
            {
                players = PlayerManager.OnlinePlayers
                    .Select(BuildPlayerInfo)
                    .ToList();
            }

            var response = new PlayerListResponse
            {
                Count = players.Count,
                Players = players
            };
            SendResponseJson(HttpCode.OK, response);
        }

        void HandleApiPlayerInfo(string identifier)
        {
            var client = PlayerManager.GetClientByBattleTag(identifier)
                         ?? PlayerManager.GetClientByEmail(identifier);

            if (client == null)
            {
                SendResponseJson(HttpCode.NotFound,
                    new CommandResponse { Success = false, Output = "Player not found." });
                return;
            }

            SendResponseJson(HttpCode.OK, BuildPlayerInfo(client));
        }

        void HandleApiCommand(HttpHeader request)
        {
            var configuredKey = RestConfig.Instance.ApiKey;
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                SendResponseJson(HttpCode.Unauthorized,
                    new CommandResponse { Success = false, Output = "Command endpoint is disabled: no ApiKey configured." });
                return;
            }

            var providedKey = request.XApiKey ?? string.Empty;
            if (providedKey != configuredKey)
            {
                SendResponseJson(HttpCode.Unauthorized,
                    new CommandResponse { Success = false, Output = "Invalid or missing API key." });
                return;
            }

            var commandRequest = Json.CreateObject<CommandRequest>(request.Content ?? string.Empty);
            if (commandRequest == null || string.IsNullOrWhiteSpace(commandRequest.Command))
            {
                SendResponseJson(HttpCode.BadRequest,
                    new CommandResponse { Success = false, Output = "Missing or empty 'command' field." });
                return;
            }

            var (success, output) = CommandManager.ParseWithOutput(commandRequest.Command);
            SendResponseJson(HttpCode.OK, new CommandResponse { Success = success, Output = output });
        }

        static PlayerInfoResponse BuildPlayerInfo(BattleClient client) => new PlayerInfoResponse
        {
            BattleTag = client.Account?.BattleTag ?? string.Empty,
            UserLevel = client.Account?.UserLevel.ToString() ?? string.Empty,
            InGame = client.InGameClient?.Player?.World != null
        };

        void SendResponseJson<T>(HttpCode code, T response)
        {
            AsyncWrite(HttpHelper.CreateResponse(code, Json.CreateString(response)));
        }

        // ──────────────────────────────────────────────────────────────────────

        void SendResponse<T>(HttpCode code, T response)
        {
            AsyncWrite(HttpHelper.CreateResponse(code, JSON.Json.CreateString(response)));
        }

        void SendResponseHtml(HttpCode code, string response)
        {
            AsyncWrite(HttpHelper.CreateResponse(code, response, contentType: "text/html"));
        }

        public override void Start()
        {
            AsyncRead();
        }

        void HandleLoginRequest(HttpHeader request)
        {
            LogonData loginForm = Json.CreateObject<LogonData>(request.Content);
            LogonResult loginResult = new LogonResult();
            if (loginForm?.Inputs is null or {Count: 0})
            {
                loginResult.AuthenticationState = "LOGIN";
                loginResult.ErrorCode = "UNABLE_TO_DECODE";
                loginResult.ErrorMessage = "There was an internal error while connecting to Battle.net. Please try again later.";
                SendResponse(HttpCode.BadRequest, loginResult);
                return;
            }

            string login = "";
            string password = "";

            foreach (var input in loginForm.Inputs)
            {
                switch (input.Id)
                {
                    case "account_name":
                        login = input.Value;
                        break;
                    case "password":
                        password = input.Value;
                        break;
                }
            }

            bool result = false;
            
            if(AccountManager.GetAccountBySaltTicket(password + " asa " + login.ToLower()) != null)
            {
                loginResult.LoginTicket = password + " asa " + login.ToLower();// "AiDiE";
                result = true;
            }

            if (result)
            {
                loginResult.AuthenticationState = "DONE";
                SendResponse(HttpCode.OK, loginResult);
                Logger.Warn("Authentication completed: Login - {0}.", login);
            }
            else
            {
                loginResult.AuthenticationState = "LOGIN";
                loginResult.ErrorCode = "UNABLE_TO_DECODE";
                loginResult.ErrorMessage = "The information you have entered is not valid.";
                SendResponse(HttpCode.BadRequest, loginResult);
                Logger.Error("Authentication failed: Login - {0}.", login);
            }
            CloseSocket();
            
        }
    }
}
