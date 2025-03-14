﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Streams;

public class LengthHeaderMessageHandlerTests : TestBase
{
    private SimplexStream halfDuplexStream = new SimplexStream();
    private LengthHeaderMessageHandler handler;

    public LengthHeaderMessageHandlerTests(ITestOutputHelper logger)
        : base(logger)
    {
        this.handler = new LengthHeaderMessageHandler(this.halfDuplexStream, this.halfDuplexStream, new JsonMessageFormatter());
    }

    [Fact]
    public void Ctor_NullPipe()
    {
        Assert.Throws<ArgumentNullException>(() => new LengthHeaderMessageHandler(null!, new JsonMessageFormatter()));
    }

    [Fact]
    public void Ctor_NullFormatter()
    {
        Assert.Throws<ArgumentNullException>(() => new LengthHeaderMessageHandler(new MemoryStream().UsePipe(cancellationToken: TestContext.Current.CancellationToken), null!));
    }

    [Fact]
    public void Ctor_NullWriter()
    {
        this.handler = new LengthHeaderMessageHandler(null, new MemoryStream().UsePipeReader(cancellationToken: TestContext.Current.CancellationToken), new JsonMessageFormatter());
        Assert.True(this.handler.CanRead);
        Assert.False(this.handler.CanWrite);
    }

    [Fact]
    public void Ctor_NullReader()
    {
        this.handler = new LengthHeaderMessageHandler(new MemoryStream().UsePipeWriter(cancellationToken: TestContext.Current.CancellationToken), null, new JsonMessageFormatter());
        Assert.False(this.handler.CanRead);
        Assert.True(this.handler.CanWrite);
    }

    [Fact]
    public void Ctor_NullWriteStream()
    {
        this.handler = new LengthHeaderMessageHandler(null, new MemoryStream(), new JsonMessageFormatter());
        Assert.True(this.handler.CanRead);
        Assert.False(this.handler.CanWrite);
    }

    [Fact]
    public void Ctor_NullReadStream()
    {
        this.handler = new LengthHeaderMessageHandler(new MemoryStream(), null, new JsonMessageFormatter());
        Assert.False(this.handler.CanRead);
        Assert.True(this.handler.CanWrite);
    }

    [Fact]
    public async Task EndOfStream()
    {
        this.halfDuplexStream.CompleteWriting();
        Assert.Null(await this.handler.ReadAsync(this.TimeoutToken));
    }

    [Fact]
    public async Task WriteAndRead()
    {
        JsonRpcRequest original = new JsonRpcRequest { Method = "test" };
        await this.handler.WriteAsync(original, this.TimeoutToken);
        var commuted = (JsonRpcRequest?)await this.handler.ReadAsync(this.TimeoutToken);
        Assert.Equal(original.Method, commuted!.Method);
    }
}
