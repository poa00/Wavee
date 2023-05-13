﻿using System.Diagnostics.Contracts;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Wavee.Core.Infrastructure.Traits;

namespace Wavee.Core.Infrastructure.Sys.IO;

/// <summary>
/// Websocket IO 
/// </summary>
public static class Ws<RT>
    where RT : struct, HasWebsocket<RT>
{
    /// <summary>
    /// Connect to a wss:// url 
    /// </summary>
    /// <param name="url">
    /// The url to connect to. In the form of wss://...
    /// </param>
    /// <typeparam name="RT">Runtime</typeparam>
    /// <returns>A new websocket client.</returns>
    [Pure, MethodImpl(AffOpt.mops)]
    public static Aff<RT, WebSocket> Connect(string url, CancellationToken ct = default) =>
        from ws in default(RT).WsEff.MapAsync(e => e.Connect(url, ct))
        select ws;

    /// <summary>
    /// Write data to the websocket client.
    /// </summary>
    /// <param name="socket">The socket stream to write to</param>
    /// <param name="data">
    /// The data to write to the socket
    /// </param>
    /// <typeparam name="RT">Runtime</typeparam>
    /// <returns>Unit</returns>
    [Pure, MethodImpl(AffOpt.mops)]
    public static Aff<RT, Unit> Write(WebSocket socket, ReadOnlyMemory<byte> data) =>
        from ct in cancelToken<RT>()
        from _ in default(RT).WsEff.MapAsync(e => e.Write(socket, data, ct))
        select unit;

    /// <summary>
    /// Read the a message from the socket. This will block until a full message is read.
    /// </summary>
    /// <param name="socket">The socket stream to read from.</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="RT">Runtime</typeparam>
    /// <returns>
    /// A portion in memory of the data read from the remote host.
    /// </returns>
    [Pure, MethodImpl(AffOpt.mops)]
    public static Aff<RT, ReadOnlyMemory<byte>> Read(WebSocket socket, CancellationToken cancellationToken) =>
        from res in default(RT).WsEff.MapAsync(e => e.Receive(socket, cancellationToken))
        select res;
}