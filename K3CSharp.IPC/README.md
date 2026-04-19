# K3CSharp.IPC

A C# port of the K3 IPC protocol used by the
[k3ipc-go](https://github.com/tangentstream/k3ipc-go) reference implementation.
It provides a small, dependency-free library that can talk to a running K3
interpreter (or to any peer that speaks the same wire format), plus a matching
server for the ksharp side.

## Components

- `K3Codec` — bit-for-bit compatible encoder / decoder for the K3 IPC wire
  format. Supports the primitive types (`int`, `double`, `byte`, `string`,
  symbol, symbol list), nested lists, dictionaries, and null.
- `KSym` — lightweight struct used to distinguish K3 symbols from ordinary
  strings when encoding.
- `K3IpcClient` — synchronous TCP client. Supports fire-and-forget `Set`
  messages and blocking `Get`/`Query` calls.
- `K3IpcServer` — TCP listener that dispatches incoming messages to a
  user-supplied `K3MessageHandler` delegate. A handler returning a non-null
  value (or a message with type `Get`) causes the server to send a
  `Response` frame back to the caller; exceptions are automatically reported
  as `(1; error-text)`, matching the convention used by `.m.g` in `kipc.k`.

## Message types

| Name                 | Value | K meaning                               |
| -------------------- | :---: | --------------------------------------- |
| `K3MsgType.Set`      | 0     | async request, dispatched to `.m.s`     |
| `K3MsgType.Get`      | 1     | sync request, dispatched to `.m.g`      |
| `K3MsgType.Response` | 2     | response to a `Get`                     |

## Quick start

```csharp
using K3CSharp.IPC;

// Synchronous query to a running K3 instance on port 5000:
using var client = new K3IpcClient("localhost");
var reply = client.Query("2 + 2");
Console.WriteLine(reply);

// Hosting a server that echoes every request back with a success status:
using var server = new K3IpcServer(port: 1024,
    (header, value) => new object?[] { 0, value });
server.Start();
```

## Wire-format compatibility

The test project, `K3CSharp.IPC.Tests`, reuses the exact byte vectors from the
Go reference test suite (`k3ipc/k3ipc_test.go`). A passing run of
`dotnet run --project K3CSharp.IPC.Tests` proves that encoder and decoder match
the Go implementation byte for byte, then exercises a small round trip between
`K3IpcClient` and `K3IpcServer` on a loopback port.
