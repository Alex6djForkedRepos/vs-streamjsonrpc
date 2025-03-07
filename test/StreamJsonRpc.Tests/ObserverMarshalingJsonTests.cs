﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ObserverMarshalingJsonTests : ObserverMarshalingTests
{
    public ObserverMarshalingJsonTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    protected override IJsonRpcMessageFormatter CreateFormatter() => new JsonMessageFormatter();
}
