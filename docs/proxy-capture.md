# Traffic proxy (capturing the real Battle.net conversation)

The proxy sits between the retail client and the real Battle.net service. It does not answer
anything itself: every client connection becomes exactly one upstream connection and every byte is
forwarded unchanged, in both directions. Battle.net therefore sees the original client's requests —
same bytes, same order, same HTTP headers and WebSocket frames — and the client sees the original
answers. The only thing added is a recording of the decrypted stream.

It reuses the redirection the emulator already relies on (`tools/patch_client.sh`): the Battle.net
names resolve to `127.0.0.1` through the hosts file, and the client trusts the local CA
(`~/.local/share/D3Offline/D3Offline-LocalCA.cer`). Upstream is looked up through an explicit DNS
resolver, because the hosts file points those names at this machine.

## Enabling

```ini
[Proxy]
Enabled = true
ListenIP = 127.0.0.1
HttpsPort = 443          ; Battle.net REST/HTTPS. Ports below 1024 need sudo or CAP_NET_BIND_SERVICE
BnetPort = 1119          ; TLS/HTTP/WebSocket gateway the client uses
GameServerPort = 1120    ; listener for the game server connection (see below)
UpstreamBnetHost = us.actual.battle.net
UpstreamBnetPort = 1119
UpstreamHttpsPort = 443
UpstreamGameServerHost =          ; filled in after the first capture (see below)
UpstreamGameServerPort = 1119
DnsServer = 1.1.1.1
CertificateNames =                 ; extra names for the local certificate
CaptureDirectory = captures
CapturePreviewBytes = 512
```

The proxy starts instead of the game server. `./Blizzless --proxy` does the same without editing
the config.

While the proxy runs, the emulator must not fight for the same ports: give it a different
`[Battle-Server] Port`, or stop it.

## Capturing the game connection

Battle.net answers the client with the address of the game server it should use. That address is
the only thing the proxy cannot derive on its own, so:

1. Run the proxy with `UpstreamGameServerHost` empty, log in once, and look at the capture log. When
   an address appears in the stream it is reported as
   `!! possible game server address in stream: <host>:<port>`.
2. Add that name to the hosts file pointing at `ListenIP` (like the Battle.net names), put the same
   name into `[Proxy] CertificateNames`, and set `UpstreamGameServerHost`/`UpstreamGameServerPort`
   to the address the capture reported.
3. Restart the proxy and log in again: the game connection is now captured as well.

## Capture files

`CaptureDirectory/capture-<date>.log` — readable log, one line per chunk with timestamp, direction,
byte count, ASCII preview and hex preview. Raw dumps are complete and can be replayed:

- `connNNN-<endpoint>-c2s.bin` — everything the client sent
- `connNNN-<endpoint>-s2c.bin` — everything the server answered

`<endpoint>` is `https`, `bnet` or `gs`, so the three conversations stay apart.

## Notes

- Nothing is modified, so protobufs do not have to be patched and the session works exactly as it
  would without the proxy.
- The recorded data is protocol traffic, not game assets; keep the capture directory out of the
  repository.
- Use it only with your own account and installation. Intercepting the client/server connection can
  violate the Battle.net terms of service, and an account ban cannot be ruled out.
