# Troubleshooting

## Tracing

When investigating failures, you may find StreamJsonRpc's tracing functionality useful.

With the <xref:StreamJsonRpc.JsonRpc.TraceSource?displayProperty=nameWithType> property, you can listen for:

1. Which server methods are registered
1. Incoming and outgoing JSON-RPC messages and how they're being handled along the entire pipeline
1. When listening is started
1. RPC method invocation failures with full exception callstacks.

The above is just a sample. The full list of events is available on the <xref:StreamJsonRpc.JsonRpc.TraceEvents?displayProperty=nameWithType> enum.

## Other issues

### Requests failing with ConnectionLostException

Please see our [disconnecting](disconnecting.md) documentation.

### Hangs after connecting over IPC pipes

When connecting two processes using Windows (named) pipes, be sure to use <xref:System.IO.Pipes.PipeOptions.Asynchronous?displayProperty=nameWithType>
when creating those pipes to be used with our @StreamJsonRpc.JsonRpc class. All our I/O is asynchronous, and
without that flag, .NET Framework and .NET Core will hang.
See [dotnet/runtime#31390](https://github.com/dotnet/runtime/issues/31390).
