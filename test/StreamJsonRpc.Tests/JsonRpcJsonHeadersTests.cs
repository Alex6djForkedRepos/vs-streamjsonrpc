﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class JsonRpcJsonHeadersTests : JsonRpcTests
{
    public JsonRpcJsonHeadersTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    protected override Type FormatterExceptionType => typeof(JsonSerializationException);

    [Fact]
    public async Task CustomJsonConvertersAreNotAppliedToBaseMessage()
    {
        var clientMessageFormatter = (JsonMessageFormatter)this.clientMessageFormatter;
        var serverMessageFormatter = (JsonMessageFormatter)this.serverMessageFormatter;

        // This test works because it encodes any string value, such that if the json-rpc "method" property
        // were serialized using the same serializer as parameters, the invocation would fail because the server-side
        // doesn't find the method with the mangled name.

        // Test with the converter only on the client side.
        clientMessageFormatter.JsonSerializer.Converters.Add(new StringBase64Converter());
        string result = await this.clientRpc.InvokeAsync<string>(nameof(this.server.ExpectEncodedA), "a");
        Assert.Equal("a", result);

        // Test with the converter on both sides.
        serverMessageFormatter.JsonSerializer.Converters.Add(new StringBase64Converter());
        result = await this.clientRpc.InvokeAsync<string>(nameof(this.server.RepeatString), "a");
        Assert.Equal("a", result);

        // Test with the converter only on the server side.
        clientMessageFormatter.JsonSerializer.Converters.Clear();
        result = await this.clientRpc.InvokeAsync<string>(nameof(this.server.AsyncMethod), "YQ==");
        Assert.Equal("YSE=", result); // a!
    }

    [Fact]
    public async Task CanInvokeServerMethodWithParameterPassedAsObject()
    {
        string result1 = await this.clientRpc.InvokeWithParameterObjectAsync<string>(nameof(Server.TestParameter), new { test = "test" }, TestContext.Current.CancellationToken);
        Assert.Equal("object {" + Environment.NewLine + "  \"test\": \"test\"" + Environment.NewLine + "}", result1);
    }

    [Fact]
    public async Task CanInvokeServerMethodWithParameterPassedAsArray()
    {
        string result1 = await this.clientRpc.InvokeAsync<string>(nameof(Server.TestParameter), "test");
        Assert.Equal("object test", result1);
    }

    [Fact]
    public async Task InvokeWithCancellationAsync_AndCancel()
    {
        using (var cts = new CancellationTokenSource())
        {
            var invokeTask = this.clientRpc.InvokeWithCancellationAsync<string>(nameof(Server.AsyncMethodWithJTokenAndCancellation), new[] { "a" }, cts.Token);
            await Task.WhenAny(invokeTask, this.server.ServerMethodReached.WaitAsync(this.TimeoutToken));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        }
    }

    [Fact]
    public async Task InvokeWithParameterObjectAsync_AndCancel()
    {
        using (var cts = new CancellationTokenSource())
        {
            var invokeTask = this.clientRpc.InvokeWithParameterObjectAsync<string>(nameof(Server.AsyncMethodWithJTokenAndCancellation), new { b = "a" }, cts.Token);
            await Task.WhenAny(invokeTask, this.server.ServerMethodReached.WaitAsync(this.TimeoutToken));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        }
    }

    [Fact]
    public async Task InvokeWithParameterObjectAsync_NoResult_AndCancel()
    {
        using (var cts = new CancellationTokenSource())
        {
            var invokeTask = this.clientRpc.InvokeWithParameterObjectAsync(nameof(Server.AsyncMethodWithJTokenAndCancellation), new { b = "a" }, cts.Token);
            await Task.WhenAny(invokeTask, this.server.ServerMethodReached.WaitAsync(this.TimeoutToken));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        }
    }

    [Fact]
    public async Task InvokeWithParameterObjectAsync_AndComplete()
    {
        using (var cts = new CancellationTokenSource())
        {
            var invokeTask = this.clientRpc.InvokeWithParameterObjectAsync<string>(nameof(Server.AsyncMethodWithJTokenAndCancellation), new { b = "a" }, cts.Token);
            this.server.AllowServerMethodToReturn.Set();
            string result = await invokeTask;
            Assert.Equal(@"{""b"":""a""}!", result);
        }
    }

    [Fact]
    public async Task InvokeWithCancellationAsync_AndComplete()
    {
        using (var cts = new CancellationTokenSource())
        {
            var invokeTask = this.clientRpc.InvokeWithCancellationAsync<string>(nameof(Server.AsyncMethodWithJTokenAndCancellation), new[] { "a" }, cts.Token);
            this.server.AllowServerMethodToReturn.Set();
            string result = await invokeTask;
            Assert.Equal(@"""a""!", result);
        }
    }

    [Fact]
    public async Task Completion_FaultsOnFatalError()
    {
        Task completion = this.serverRpc.Completion;
        byte[] invalidMessage = Encoding.UTF8.GetBytes("A\n\n");
        await this.clientStream.WriteAsync(invalidMessage, 0, invalidMessage.Length, TestContext.Current.CancellationToken).WithCancellation(this.TimeoutToken);
        await this.clientStream.FlushAsync(TestContext.Current.CancellationToken).WithCancellation(this.TimeoutToken);
        await Assert.ThrowsAsync<BadRpcHeaderException>(() => completion).WithCancellation(this.TimeoutToken);
        Assert.Same(completion, this.serverRpc.Completion);
    }

    protected override void InitializeFormattersAndHandlers(
        Stream serverStream,
        Stream clientStream,
        out IJsonRpcMessageFormatter serverMessageFormatter,
        out IJsonRpcMessageFormatter clientMessageFormatter,
        out IJsonRpcMessageHandler serverMessageHandler,
        out IJsonRpcMessageHandler clientMessageHandler,
        bool controlledFlushingClient)
    {
        clientMessageFormatter = new JsonMessageFormatter
        {
            JsonSerializer =
            {
                Converters =
                {
                    new UnserializableTypeConverter(),
                    new TypeThrowsWhenDeserializedConverter(),
                },
            },
        };
        serverMessageFormatter = new JsonMessageFormatter
        {
            JsonSerializer =
            {
                Converters =
                {
                    new UnserializableTypeConverter(),
                    new TypeThrowsWhenDeserializedConverter(),
                },
            },
        };

        serverMessageHandler = new HeaderDelimitedMessageHandler(serverStream, serverStream, serverMessageFormatter);
        clientMessageHandler = controlledFlushingClient
            ? new DelayedFlushingHandler(clientStream, clientMessageFormatter)
            : new HeaderDelimitedMessageHandler(clientStream, clientStream, clientMessageFormatter);
    }

    protected override object[] CreateFormatterIntrinsicParamsObject(string arg) =>
        [new JObject { ["arg"] = arg }];

    protected class UnserializableTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(CustomSerializedType);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return new CustomSerializedType
            {
                Value = (string?)reader.Value,
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(((CustomSerializedType?)value)!.Value);
        }
    }

    protected class TypeThrowsWhenDeserializedConverter : JsonConverter<TypeThrowsWhenDeserialized>
    {
        public override TypeThrowsWhenDeserialized ReadJson(JsonReader reader, Type objectType, TypeThrowsWhenDeserialized? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw CreateExceptionToBeThrownByDeserializer();
        }

        public override void WriteJson(JsonWriter writer, TypeThrowsWhenDeserialized? value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }

    protected class DelayedFlushingHandler : HeaderDelimitedMessageHandler, IControlledFlushHandler
    {
        public DelayedFlushingHandler(Stream stream, IJsonRpcMessageFormatter formatter)
            : base(stream, formatter)
        {
        }

        public AsyncAutoResetEvent FlushEntered { get; } = new AsyncAutoResetEvent();

        public AsyncManualResetEvent AllowFlushAsyncExit { get; } = new AsyncManualResetEvent();

        protected override async ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            this.FlushEntered.Set();
            await this.AllowFlushAsyncExit.WaitAsync(CancellationToken.None);
            await base.FlushAsync(cancellationToken);
        }
    }

    private class StringBase64Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String((string)reader.Value!));
            return decoded;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var stringValue = (string?)value;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(stringValue!));
            writer.WriteValue(encoded);
        }
    }
}
