# Establishing a JSON-RPC connection

A JSON-RPC connection is created and managed via the @StreamJsonRpc.JsonRpc class and communicates over an existing transport, such as a .NET @System.IO.Stream, @System.IO.Pipelines.IDuplexPipe or @System.Net.WebSockets.WebSocket.

## Connecting

If using the @System.IO.Stream class, you may use one duplex @System.IO.Stream (e.g. a @System.IO.Pipes.PipeStream or @System.Net.Sockets.NetworkStream)
or a pair of simplex @System.IO.Streams (e.g. STDIN and STDOUT streams).
Most APIs accept both forms.

You can use the APIs that accept just one duplex stream by splicing two simplex streams together using the @Nerdbank.Streams.FullDuplexStream.Splice* API:

```cs
var stdioStream = FullDuplexStream.Splice(readingStream, writingStream);
var jsonRpc = JsonRpc.Attach(stdioStream);
```

Certain decisions about the protocol details must be made up front while constructing the @StreamJsonRpc.JsonRpc class.
This library includes several built-in protocol variants and options, and you can add your own. This is all documented in our [extensibility](extensibility.md) document.

In the remaining samples in this section we use the convenient static @StreamJsonRpc.JsonRpc.Attach* method which
instantiates the class with the default settings and begins listening for messages immediately. Samples for changing aspects of the protocol are in [a section below](#Configuring).

### Client

To establish the JSON-RPC connection over a @System.IO.Stream, where you will only issue requests (not respond to them),
use the static @StreamJsonRpc.JsonRpc.Attach* method:

```cs
JsonRpc rpc = JsonRpc.Attach(stream);
```

You can then proceed to send requests using the `rpc` variable. [Learn more about sending requests](sendrequest.md).

Consider a process that spawns a child process, redirecting its STDIN/STDOUT to communicate with that child process using JSON-RPC:

```cs
Process childProcess = Process.Start(new ProcessStartInfo("childprocess.exe")
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
});
JsonRpc jsonRpc = JsonRpc.Attach(childProcess.StandardInput.BaseStream, childProcess.StandardOutput.BaseStream);
```

### Server (and possibly client also)

If you expect to respond to RPC requests, you can provide the target object that defines the methods that may be
invoked by the remote party:

```cs
RpcTarget target = new RpcTarget();
JsonRpc rpc = JsonRpc.Attach(stream, target);
```

The @StreamJsonRpc.JsonRpc object assigned to the `rpc` variable is now listening for requests on the stream and will invoke
methods on the `target` object as requested. You can also make requests with this `rpc` object just like the earlier example.
[Learn more about receiving requests](recvrequest.md).

For servers that should wait for incoming RPC requests until the client disconnects, utilize the @StreamJsonRpc.JsonRpc.Completion?displayProperty=nameWithType property
which returns a @System.Threading.Tasks.Task that completes when the connection drops. By awaiting this after attaching @StreamJsonRpc.JsonRpc to the stream,
and before disposing the stream, you can hold the connection open as long as the client maintains it:

```cs
await rpc.Completion;
```

For an invisible process that uses STDIN/STDOUT as its transport for JSON-RPC, this can be trivially done with code like this:

```cs
JsonRpc rpc = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput());
```

But beware that STDIN/STDOUT handles are freely available to all code running in a process.
Any code that interacts with STDIN/STDOUT can potentially corrupt the JSON-RPC protocol messages being exchanged.
For example if the process uses `Console.WriteLine` for logging anywhere, this will corrupt the JSON-RPC stream and ultimately lead to malfunction and/or disconnection.

## Configuring/customizing the protocol <a name="Configuring"></a>

To alter the protocol in any way from the defaults, use the @StreamJsonRpc.JsonRpc constructor directly, instead of using the static @StreamJsonRpc.JsonRpc.Attach* method.
This gives you a chance to provide your own @StreamJsonRpc.IJsonRpcMessageHandler, set text encoding, etc. before sending or receiving any messages.
Remember after configuring your instance to start listening by calling the @StreamJsonRpc.JsonRpc.StartListening*?displayProperty=nameWithType method. This step is not necessary when using the static @StreamJsonRpc.JsonRpc.Attach* method because it calls @StreamJsonRpc.JsonRpc.StartListening* for you.

To make it easier for the receiver to know when it has received a complete JSON-RPC message,
we transmit the length in bytes of a message before the message itself.
We also transmit the text encoding used in the message.
The default is to use HTTP-like headers to do so.

    Content-Length: 38
    Content-Type: application/vscode-jsonrpc;charset=utf-8

    {"jsonrpc":"2.0","id":1,"result":"hi"}

When receiving a message, UTF-8 is assumed if not explicitly specified.
When transmitting a message with UTF-8 encoding, the `Content-Type` header is omitted for brevity.

Suppose that instead of introducing each JSON-RPC message with an HTTP-like header to disclose the size of the message, you want to establish a high performance connection that simply transmits a 4-byte big endian integer set to the length before each message.

You can do that like so:

```cs
Stream send, recv;
var formatter = new JsonMessageFormatter(Encoding.UTF8);
var handler = new LengthHeaderMessageHandler(send, recv, formatter);
var jsonRpc = new JsonRpc(handler);
// Add any applicable target objects/methods here, or in the JsonRpc constructor above
jsonRpc.StartListening();
```

You could go further in achieving a high performance connection by replacing the @StreamJsonRpc.JsonMessageFormatter in the above code with a binary formatter. [Learn more about alternative formatters and handlers](extensibility.md).

It is important that both sides of a connection agree on the protocol settings.

**Important**: Create a new @StreamJsonRpc.IJsonRpcMessageFormatter for each new instance of @StreamJsonRpc.IJsonRpcMessageHandler.
Message formatters make certain thread safety decisions based on assumptions guaranteed by a single message handler.
Sharing message formatters across handlers breaks those assumptions and can lead to instability and data corruption.

## Disconnecting

Once connected, a *listening* @StreamJsonRpc.JsonRpc object will continue to operate till the connection is terminated, even if
the original creator drops the reference to that @StreamJsonRpc.JsonRpc object. It is not subject to garbage collection because
the underlying transport has a reference to it for notifying of an incoming message.

[Learn more about disconnection](disconnecting.md).
