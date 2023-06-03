﻿using Google.Protobuf.WellKnownTypes;
using LanguageExt;
using Wavee.Core.Ids;

namespace Wavee.Spotify.Infrastructure.Remote.Contracts;

public enum RemoteSpotifyPlaybackEventType
{
    Play
}
public readonly struct RemoteSpotifyPlaybackEvent
{
    public required RemoteSpotifyPlaybackEventType EventType { get; init; }
    public AudioId TrackId { get; init; }
    public required Option<string> TrackUid { get; init; }
    public required Option<int> TrackIndex { get; init; }
    public TimeSpan PlaybackPosition { get; init; }
    public Option<string> ContextUri { get; init; }
    public bool IsPaused { get; init; }
    public bool IsShuffling { get; init; }
    public RepeatState RepeatState { get; init; }
}