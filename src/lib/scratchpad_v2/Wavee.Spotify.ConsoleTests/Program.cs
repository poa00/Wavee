﻿using System.Diagnostics;
using Eum.Spotify;
using Eum.Spotify.connectstate;
using Google.Protobuf;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Serilog;
using Wavee.Spotify;
using Wavee.Spotify.Configs;
using ILogger = Microsoft.Extensions.Logging.ILogger;

var loginCredentials = new LoginCredentials
{
    Username = Environment.GetEnvironmentVariable("SPOTIFY_USERNAME"),
    AuthData = ByteString.CopyFromUtf8(Environment.GetEnvironmentVariable("SPOTIFY_PASSWORD")),
    Typ = AuthenticationType.AuthenticationUserPass
};

var config = new SpotifyConfig(
    Remote: new SpotifyRemoteConfig(
        DeviceName: "Wavee Test",
        DeviceType: DeviceType.Computer
    ),
    Playback: new SpotifyPlaybackConfig(
        PreferredQualityType.High
    )
);
var log = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();
ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(log);
});
var logger = loggerFactory.CreateLogger<Program>();
var connection = await SpotifyClient.Create(loginCredentials, config, Option<ILogger>.Some(logger));
var remoteCluster = await connection.Remote.Connect(CancellationToken.None);
remoteCluster.Subscribe(state => { logger.LogInformation("Remote state: {state}", state); });

//https://open.spotify.com/track/3K8wfMDLxwtLGuVrYobxVe?si=518e3b12e14443fb
//https://open.spotify.com/playlist/2TAyTkj953qz8fG2IXq1V0?si=1028b95ac678473e
//https://open.spotify.com/track/0afoCntatBcJGjz525RxBT?si=62bb8201d730434d
//https://open.spotify.com/track/49jhaFKylisSzgaReEP2Jt?si=4eef71c9b1cf4897
//var playback = await connection.Playback.PlayContext
var playback = await connection.Playback.PlayTrack("spotify:track:49jhaFKylisSzgaReEP2Jt",
    Option<PreferredQualityType>.None,
    CancellationToken.None);

Console.ReadLine();