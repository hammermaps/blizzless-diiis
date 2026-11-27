using DiIiS_NA.Core.Logging;
using DiIiS_NA.LoginServer.Battle;
using DotNetty.Codecs;
using DotNetty.Codecs.Http;
using DotNetty.Common;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;


using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using System.Threading.Tasks;
using DotNetty.Codecs.Http.WebSockets;

namespace DiIiS_NA.LoginServer.Base
{
    public class ConnectHandler : ChannelInitializer<ISocketChannel>
    {
        private static readonly Logger Logger = LogManager.CreateLogger("BlizzLess.Net System");
        protected override void InitChannel(ISocketChannel socketChannel)
        {
            Logger.Info("Request of new Client - IP: - {0}", socketChannel.RemoteAddress);
            // Battle.net multiplexes plain HTTP ("/game/<product>_<locale>", "/agent", "/agent/download")
            // and the TLS/WebSocket gateway on the same port. The client sends HTTP first, so the
            // protocol is chosen from the first byte instead of answering every connection with TLS.
            socketChannel.Pipeline.AddLast(new ProtocolSniffer(socketChannel));
        }

        /// <summary>Builds the encrypted gateway pipeline: TLS -> HTTP/WebSocket -> BNet -> BattleClient.</summary>
        internal static void AttachGateway(ISocketChannel socketChannel, IChannelPipeline p)
        {
            var Certificate = LocalCertificate.Load();
            TlsHandler TLS = TlsHandler.Server(Certificate);

            p.AddLast(TLS);
            p.AddLast(new FirstReadLogger());
            p.AddLast(new HttpServerCodec());
            p.AddLast(new HttpObjectAggregator(8192));
            //p.AddLast(new WebSocketServerProtocolHandler("/", "jsonrpc.aurora.v1.30.battle.net", true));
            p.AddLast(new HandshakeHandler("/", "v1.rpc.battle.net", true));

            p.AddLast(new BNetCodec());
            p.AddLast(new BattleClient(socketChannel, TLS));
        }

        /// <summary>Builds the plain HTTP pipeline used for the client's game/agent requests.</summary>
        internal static void AttachPlainHttp(IChannelPipeline p)
        {
            p.AddLast(new HttpServerCodec());
            p.AddLast(new HttpObjectAggregator(8192));
            p.AddLast(new PlainHttpHandler());
        }

        private static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }

    /// <summary>
    /// Looks at the first byte of a connection and installs either the TLS gateway or the plain
    /// HTTP pipeline. 0x16 is a TLS handshake record; anything else is treated as HTTP.
    /// </summary>
    internal class ProtocolSniffer : ChannelHandlerAdapter
    {
        private static readonly Logger Logger = LogManager.CreateLogger("BlizzLess.Net System");
        private readonly ISocketChannel _socketChannel;

        public ProtocolSniffer(ISocketChannel socketChannel) => _socketChannel = socketChannel;

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is not IByteBuffer buffer || buffer.ReadableBytes == 0)
            {
                ctx.FireChannelRead(msg);
                return;
            }

            var first = buffer.GetByte(buffer.ReaderIndex);
            var pipeline = ctx.Channel.Pipeline;
            pipeline.Remove(this);

            if (first == 0x16)
            {
                ConnectHandler.AttachGateway(_socketChannel, pipeline);
            }
            else
            {
                Logger.Info("Plain HTTP request on the Battle.net port ({0} first byte, {1} bytes); answering without TLS.",
                    first, buffer.ReadableBytes);
                ConnectHandler.AttachPlainHttp(pipeline);
            }

            // Fire from the pipeline head: this handler was removed above, so its own context
            // no longer propagates reads and the buffer would be dropped.
            pipeline.FireChannelRead(msg);
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Logger.ErrorException(cause, "Connection error from {0}", ctx.Channel.RemoteAddress);
            ctx.CloseAsync();
        }
    }

    /// <summary>
    /// Answers the client's plain HTTP requests on the Battle.net port. The game only needs a
    /// successful status line to continue with the TLS login; the bodies are intentionally empty.
    /// </summary>
    internal class PlainHttpHandler : SimpleChannelInboundHandler<IFullHttpRequest>
    {
        private static readonly Logger Logger = LogManager.CreateLogger("BlizzLess.Net System");

        protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            var uri = request.Uri ?? string.Empty;
            Logger.Info("Plain HTTP {0} {1} (host {2})", request.Method, uri, request.Headers.Get(HttpHeaderNames.Host, AsciiString.Empty));

            // The client fetches the achievement file from the same port it logs in on.
            if (request.Method.Equals(HttpMethod.Get) && uri.EndsWith(".achu", StringComparison.OrdinalIgnoreCase))
            {
                var file = DiIiS_NA.GameServer.AchievementSystem.AchievementManager.AchievementFilePath;
                if (System.IO.File.Exists(file))
                {
                    var bytes = System.IO.File.ReadAllBytes(file);
                    var body = Unpooled.WrappedBuffer(bytes);
                    var fileResponse = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, body);
                    fileResponse.Headers.Set(HttpHeaderNames.ContentLength, bytes.Length);
                    fileResponse.Headers.Set(HttpHeaderNames.ContentType, "application/octet-stream");
                    fileResponse.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                    Logger.Info("Serving achievement file ({0} bytes) for {1}", bytes.Length, uri);
                    ctx.WriteAndFlushAsync(fileResponse).ContinueWith(_ => ctx.CloseAsync());
                    return;
                }
                Logger.Warn("Achievement file requested but {0} does not exist.", file);
            }

            var status = uri.StartsWith("/game/", StringComparison.OrdinalIgnoreCase) ||
                         uri.StartsWith("/agent", StringComparison.OrdinalIgnoreCase)
                ? HttpResponseStatus.OK
                : HttpResponseStatus.NotFound;

            var response = new DefaultFullHttpResponse(HttpVersion.Http11, status, Unpooled.Empty);
            response.Headers.Set(HttpHeaderNames.ContentLength, 0);
            response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
            ctx.WriteAndFlushAsync(response).ContinueWith(_ => ctx.CloseAsync());
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Logger.ErrorException(cause, "Plain HTTP error from {0}", ctx.Channel.RemoteAddress);
            ctx.CloseAsync();
        }
    }
    /// <summary>
    /// Logs the TLS handshake result, connection errors and the start of the first decrypted data,
    /// to see what a new client version sends.
    /// </summary>
    internal class FirstReadLogger : ChannelHandlerAdapter
    {
        private static readonly Logger Logger = LogManager.CreateLogger("BlizzLess.Net System");

        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            if (evt is TlsHandshakeCompletionEvent tls)
            {
                if (tls.IsSuccessful)
                    Logger.Info("TLS handshake with {0} completed", ctx.Channel.RemoteAddress);
                else
                    Logger.ErrorException(tls.Exception, "TLS handshake with {0} failed", ctx.Channel.RemoteAddress);
            }

            ctx.FireUserEventTriggered(evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            Logger.ErrorException(cause, "Connection error from {0}", ctx.Channel.RemoteAddress);
            ctx.FireExceptionCaught(cause);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IByteBuffer buffer)
            {
                var length = Math.Min(buffer.ReadableBytes, 512);
                var text = new StringBuilder(length);
                var hex = new StringBuilder(length * 3);
                for (var i = 0; i < length; i++)
                {
                    var b = buffer.GetByte(buffer.ReaderIndex + i);
                    text.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                    if (i < 32) hex.Append(b.ToString("x2")).Append(' ');
                }
                // Logged every connection: it is the only record of what a new client version
                // sends before the protocol handlers reject it.
                Logger.Info("First {0} of {1} bytes from client: {2}", length, buffer.ReadableBytes, text);
                Logger.Debug("First bytes (hex): {0}", hex);
            }

            ctx.FireChannelRead(msg);
            ctx.Channel.Pipeline.Remove(this);
        }
    }

    public class HandshakeHandler : WebSocketProtocolHandler
    {
        public sealed class HandshakeComplete
        {
            private readonly string requestUri;

            private readonly HttpHeaders requestHeaders;

            private readonly string selectedSubprotocol;

            public string RequestUri => requestUri;

            public HttpHeaders RequestHeaders => requestHeaders;

            public string SelectedSubprotocol => selectedSubprotocol;

            internal HandshakeComplete(string requestUri, HttpHeaders requestHeaders, string selectedSubprotocol)
            {
                this.requestUri = requestUri;
                this.requestHeaders = requestHeaders;
                this.selectedSubprotocol = selectedSubprotocol;
            }
        }

        private sealed class ForbiddenResponseHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                IFullHttpRequest fullHttpRequest;
                if ((fullHttpRequest = (msg as IFullHttpRequest)) != null)
                {
                    fullHttpRequest.Release();
                    DefaultFullHttpResponse message = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Forbidden);
                    ctx.Channel.WriteAndFlushAsync(message);
                }
                else
                {
                    ctx.FireChannelRead(msg);
                }
            }
        }

        private static readonly AttributeKey<WebSocketServerHandshaker> HandshakerAttrKey = AttributeKey<WebSocketServerHandshaker>.ValueOf("HANDSHAKER");

        private readonly string websocketPath;

        private readonly string subprotocols;

        private readonly bool allowExtensions;

        private readonly int maxFramePayloadLength;

        private readonly bool allowMaskMismatch;

        private readonly bool checkStartsWith;

        public HandshakeHandler(string websocketPath)
            : this(websocketPath, null, allowExtensions: false)
        {
        }

        public HandshakeHandler(string websocketPath, bool checkStartsWith)
            : this(websocketPath, null, allowExtensions: false, 65536, allowMaskMismatch: false, checkStartsWith)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols)
            : this(websocketPath, subprotocols, allowExtensions: false)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions)
            : this(websocketPath, subprotocols, allowExtensions, 65536)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch: false)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize, bool allowMaskMismatch)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith: false)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith, dropPongFrames: true)
        {
        }

        public HandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith, bool dropPongFrames)
            : base()// : base(dropPongFrames)
        {
            this.websocketPath = websocketPath;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            maxFramePayloadLength = maxFrameSize;
            this.allowMaskMismatch = allowMaskMismatch;
            this.checkStartsWith = checkStartsWith;
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            IChannelPipeline pipeline = ctx.Channel.Pipeline;
            if (pipeline.Get<WebSocketServerProtocolHandshakeHandler>() == null)
            {
                ctx.Channel.Pipeline.AddBefore(ctx.Name, "WebSocketServerProtocolHandshakeHandler", new WebSocketServerProtocolHandshakeHandler(websocketPath, subprotocols, allowExtensions, maxFramePayloadLength, allowMaskMismatch, checkStartsWith));
            }

            if (pipeline.Get<Utf8FrameValidator>() == null)
            {
                ctx.Channel.Pipeline.AddBefore(ctx.Name, "Utf8FrameValidator", new Utf8FrameValidator());
            }
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            CloseWebSocketFrame frame2;
            if ((frame2 = (frame as CloseWebSocketFrame)) != null)
            {
                WebSocketServerHandshaker handshaker = GetHandshaker(ctx.Channel);
                if (handshaker != null)
                {
                    frame.Retain();
                    handshaker.CloseAsync(ctx.Channel, frame2);
                }
                else
                {
                    ctx.WriteAndFlushAsync(Unpooled.Empty).ContinueWith((Task t, object c) => ((IChannelHandlerContext)c).CloseAsync(), ctx, TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            else
            {
                base.Decode(ctx, frame, output);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (cause is WebSocketHandshakeException)
            {
                DefaultFullHttpResponse message = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.BadRequest, Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(cause.Message)));
                ctx.Channel.WriteAndFlushAsync(message).ContinueWith((Task t, object c) => ((IChannelHandlerContext)c).CloseAsync(), ctx, TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                ctx.FireExceptionCaught(cause);
                ctx.CloseAsync();
            }
        }

        internal static WebSocketServerHandshaker GetHandshaker(IChannel channel)
        {
            return channel.GetAttribute(HandshakerAttrKey).Get();
        }

        internal static void SetHandshaker(IChannel channel, WebSocketServerHandshaker handshaker)
        {
            channel.GetAttribute(HandshakerAttrKey).Set(handshaker);
        }

        internal static IChannelHandler ForbiddenHttpRequestResponder()
        {
            return new ForbiddenResponseHandler();
        }
    }

    public abstract class WebSocketProtocolHandler : MessageToMessageDecoder<WebSocketFrame>
    {
        private readonly bool dropPongFrames;

        internal WebSocketProtocolHandler()
            : this(dropPongFrames: true)
        {
        }

        internal WebSocketProtocolHandler(bool dropPongFrames)
        {
            this.dropPongFrames = dropPongFrames;
        }

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame frame, List<object> output)
        {
            if (frame is PingWebSocketFrame)
            {
                frame.Content.Retain();
                ctx.Channel.WriteAndFlushAsync(new PongWebSocketFrame(frame.Content));
            }
            else if (!(frame is PongWebSocketFrame) || !dropPongFrames)
            {
                output.Add(frame.Retain());
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            ctx.FireExceptionCaught(cause);
            ctx.CloseAsync();
        }
    }
    internal class WebSocketServerProtocolHandshakeHandler : ChannelHandlerAdapter
    {
        
        static readonly Logger _logger = LogManager.CreateLogger();
        private readonly string websocketPath;

        private readonly string subprotocols;

        private readonly bool allowExtensions;

        private readonly int maxFramePayloadSize;

        private readonly bool allowMaskMismatch;

        private readonly bool checkStartsWith;

        internal WebSocketServerProtocolHandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize, bool allowMaskMismatch)
            : this(websocketPath, subprotocols, allowExtensions, maxFrameSize, allowMaskMismatch, checkStartsWith: false)
        {
        }

        internal WebSocketServerProtocolHandshakeHandler(string websocketPath, string subprotocols, bool allowExtensions, int maxFrameSize, bool allowMaskMismatch, bool checkStartsWith)
        {
            this.websocketPath = websocketPath;
            this.subprotocols = subprotocols;
            this.allowExtensions = allowExtensions;
            maxFramePayloadSize = maxFrameSize;
            this.allowMaskMismatch = allowMaskMismatch;
            this.checkStartsWith = checkStartsWith;
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is not IFullHttpRequest req)
            {
                _logger.Error("Expected a WebSocket upgrade request, got {0}", msg?.GetType().Name ?? "null");
                ctx.FireChannelRead(msg);
                return;
            }

            var headers = new StringBuilder();
            foreach (var header in req.Headers)
                headers.Append(header.Key).Append(": ").Append(header.Value).Append("; ");
            _logger.Debug("WebSocket upgrade request: {0} {1} (decode: {2}) {3}", req.Method, req.Uri, req.Result, headers);

            if (IsNotWebSocketPath(req))
            {
                _logger.Warn("Request path {0} does not match the WebSocket path {1}", req.Uri, websocketPath);
                ctx.FireChannelRead(msg);
                return;
            }

            try
            {
                if (!object.Equals(req.Method, HttpMethod.Get))
                {
                    SendHttpResponse(ctx, req,
                        new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Forbidden));
                    return;
                }

                //v1.rpc.battle.net
                //
                WebSocketServerHandshakerFactory webSocketServerHandshakerFactory =
                    new WebSocketServerHandshakerFactory(GetWebSocketLocation(ctx.Channel.Pipeline, req, websocketPath),
                        subprotocols, allowExtensions, maxFramePayloadSize, allowMaskMismatch);
                WebSocketServerHandshaker handshaker = webSocketServerHandshakerFactory.NewHandshaker(req);
                if (handshaker == null)
                {
                    WebSocketServerHandshakerFactory.SendUnsupportedVersionResponse(ctx.Channel);
                    return;
                }

                handshaker.HandshakeAsync(ctx.Channel, req).ContinueWith(delegate(Task t)
                {
                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        _logger.ErrorException(t.Exception, "WebSocket handshake failed");
                        ctx.FireExceptionCaught(t.Exception);
                    }
                    else
                    {
                        _logger.Debug("WebSocket handshake complete, subprotocol: {0}", handshaker.SelectedSubprotocol ?? "none");
                        ctx.FireUserEventTriggered(new HandshakeHandler.HandshakeComplete(req.Uri, req.Headers,
                            handshaker.SelectedSubprotocol));
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
                HandshakeHandler.SetHandshaker(ctx.Channel, handshaker);
                ctx.Channel.Pipeline.Replace(this, "WS403Responder", HandshakeHandler.ForbiddenHttpRequestResponder());
            }
            catch (Exception ex)
            {
                _logger.ErrorException(ex, "Handshake failure");
            }
            finally
            {
                req.Release();
            }
        }

        private bool IsNotWebSocketPath(IFullHttpRequest req)
        {
            if (!checkStartsWith)
            {
                return !req.Uri.Equals(websocketPath);
            }

            return !req.Uri.StartsWith(websocketPath);
        }

        private static void SendHttpResponse(IChannelHandlerContext ctx, IHttpRequest req, IHttpResponse res)
        {
            Task task = ctx.Channel.WriteAndFlushAsync(res);
            if (!HttpUtil.IsKeepAlive(req) || res.Status.Code != 200)
            {
                task.ContinueWith((Task t, object c) => ((IChannel)c).CloseAsync(), ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        private static string GetWebSocketLocation(IChannelPipeline cp, IHttpRequest req, string path)
        {
            string protocol = cp.Get<TlsHandler>() != null ? "wss" : "ws";

            // Ignore the Host header and default to a placeholder or IP address
            string host = "192.168.1.100"; // Replace with your desired default host, e.g., the server's IP or DNS.

            return $"{protocol}://{host}{path}";
        }
    }
}
