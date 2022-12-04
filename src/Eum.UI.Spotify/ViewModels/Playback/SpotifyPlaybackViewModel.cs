﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Eum.Connections.Spotify;
using Eum.Connections.Spotify.Models.Users;
using Eum.Connections.Spotify.Playback;
using Eum.Enums;
using Eum.Logging;
using Eum.Spotify.connectstate;
using Eum.Spotify.metadata;
using Eum.UI.Items;
using Eum.UI.Playlists;
using Eum.UI.Services.Directories;
using Eum.UI.Services.Playlists;
using Eum.UI.Services.Tracks;
using Eum.UI.ViewModels.Playback;
using Eum.UI.ViewModels.Playlists;
using Flurl;
using LiteDB;
using ReactiveUI;

namespace Eum.UI.Spotify.ViewModels.Playback;
public class SpotifyPlaybackViewModel : PlaybackViewModel
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifyPlaybackClient _spotifyPlaybackClient;
    private readonly IDisposable _disposable;
    public SpotifyPlaybackViewModel(ISpotifyPlaybackClient spotifyPlaybackClient, ISpotifyClient spotifyClient,
        ITrackAggregator trackAggregator)
    {
        _spotifyPlaybackClient = spotifyPlaybackClient;
        _spotifyClient = spotifyClient;

        _disposable = Observable
            .FromEventPattern<ClusterUpdate>(_spotifyPlaybackClient, nameof(ISpotifyPlaybackClient.ClusterChanged))
            .SelectMany(async x =>
            {
                if (x.EventArgs.Cluster.PlayerState.Track == null)
                {
                    return (x.EventArgs, null);
                }
                try
                {
                    var track = await trackAggregator.GetTrack(new ItemId(x.EventArgs.Cluster.PlayerState.Track.Uri));
                    return (x.EventArgs, track);
                }
                catch (Exception ex)
                {
                    S_Log.Instance.LogError(ex);
                    throw;
                }
            })
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(clusterChanged)
            .Subscribe();
    }
    private static string MakeValidFileName(string name)
    {
        string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

        return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
    }
    private async Task clusterChanged((ClusterUpdate? EventArgs, EumTrack item) obj)
    {
        var e = obj.EventArgs;
        if (e?.Cluster == null)
        {
            //clear playback state
            return;
        }

        if (obj.item == null)
        {
            Item?.Dispose();
            Item = null;
            return;
        }
        var playingUri = new SpotifyId(e.Cluster.PlayerState.Track.Uri);
        // if (!e.Cluster.PlayerState.Track.Metadata.Any())
        // {
        //     //if not we fetch!
        //     throw new NotImplementedException();
        // }
        //
        // var original = obj.original;
        //
        // string smallImage = default;
        // string bigImage = default;
        // if (playingUri.Type != EntityType.Local)
        // {
        //     smallImage = e.Cluster.PlayerState.Track.Metadata["image_small_url"].Split(":").Last();
        //     bigImage = e.Cluster.PlayerState.Track.Metadata["image_large_url"].Split(":").Last();
        //
        //     bigImage = $"https://i.scdn.co/image/{bigImage}";
        //     smallImage = $"https://i.scdn.co/image/{smallImage}";
        // }
        // else
        // {
        //     //try fetch the file on disk
        //     var smallImagePath
        //         = Url.Decode(e.Cluster.PlayerState.Track.Metadata["image_small_url"].Split(":").Last(), true);
        //     var bigImagePath
        //         = Url.Decode(e.Cluster.PlayerState.Track.Metadata["image_large_url"].Split(":").Last(), true);
        //
        //     if (File.Exists(bigImagePath))
        //     {
        //         var cleanFileName = $"{MakeValidFileName(original.Album?.Name ?? original.Name)}-big";
        //         var saveto = Path.Combine(Ioc.Default.GetRequiredService<ICommonDirectoriesProvider>()
        //             .WorkDir, cleanFileName);
        //         if (File.Exists(saveto))
        //         {
        //             bigImage = saveto;
        //         }
        //         else
        //         {
        //             using var tfile = TagLib.File.Create(bigImagePath);
        //             if (tfile.Tag.Pictures.Length > 0)
        //             {
        //                 TagLib.IPicture pic = tfile.Tag.Pictures[0];
        //                 using var ms = new MemoryStream(pic.Data.Data);
        //                 ms.Seek(0, SeekOrigin.Begin);
        //
        //                 using var fs = File.Open(saveto, FileMode.OpenOrCreate);
        //                 ms.CopyTo(fs);
        //                 bigImage = saveto;
        //             }
        //         }
        //     }
        //     if (File.Exists(smallImagePath))
        //     {
        //         var cleanFileName = $"{MakeValidFileName(original.Album?.Name ?? original.Name)}-small";
        //         var saveto = Path.Combine(Ioc.Default.GetRequiredService<ICommonDirectoriesProvider>()
        //             .WorkDir, cleanFileName);
        //         if (File.Exists(saveto))
        //         {
        //             smallImage = saveto;
        //         }
        //         else
        //         {
        //             using var tfile = TagLib.File.Create(smallImagePath);
        //             if (tfile.Tag.Pictures.Length > 0)
        //             {
        //                 TagLib.IPicture pic = tfile.Tag.Pictures[0];
        //                 using var ms = new MemoryStream(pic.Data.Data);
        //                 ms.Seek(0, SeekOrigin.Begin);
        //
        //
        //                 using var fs = File.Open(saveto, FileMode.OpenOrCreate);
        //                 ms.CopyTo(fs);
        //                 smallImage = saveto;
        //             }
        //         }
        //     }
        // }
        //
        // var albumName = e.Cluster.PlayerState.Track.Metadata["album_title"];
        // var albumId =
        //     e.Cluster.PlayerState.Track.Metadata.ContainsKey("album_uri") ?
        //     new ItemId(e.Cluster.PlayerState.Track.Metadata["album_uri"]) : default;
        // var contextId = new ItemId(e.Cluster.PlayerState.ContextUri);
        // var duration = e.Cluster.PlayerState.Duration;
        //
        //
        try
        {
            var contextId = new ItemId(e.Cluster.PlayerState.ContextUri);
            var duration = e.Cluster.PlayerState.Duration;
            Item?.Dispose();
            Item = new CurrentlyPlayingHolder
            {
                Title = new IdWithTitle
                {
                    Id = obj.item.Group.Id,
                    Title = obj.item.Name
                },
                Artists = obj.item.Artists,
                BigImage = (await obj.item.Images.MaxBy(a => a.Height ?? 0).ImageStream),
                SmallImage = (await obj.item.Images.MinBy(a => a.Height ?? 0).ImageStream),
                Duration = obj.item.Duration,
                Context = contextId
            };
            int diff = (int)(_spotifyClient.TimeProvider.CurrentTimeMillis() - e.Cluster.PlayerState.Timestamp);
            var initial = Math.Max(0, (int)(e.Cluster.PlayerState.PositionAsOfTimestamp + diff));
            StartTimer(initial);
        }
        catch (Exception ex)
        {
            var db = Ioc.Default.GetRequiredService<ILiteDatabase>();

            try
            {
                var rebuild = db.Rebuild();
            }
            catch (Exception j)
            {
                //"Cannot insert duplicate key in unique index '_id'. The duplicate value is '{\"f\":\"ab67616d0000b273be7757567d0c3dc3c98c8ba2\",\"n\":0}'."
                if (j.Message.StartsWith("Cannot insert duplicate key"))
                {
                    var findAll = db.FileStorage.FindAll();
                    foreach (var liteFileInfo in findAll)
                    {
                        db.FileStorage.Delete(liteFileInfo.Id);
                    }
                }
            }
            S_Log.Instance.LogError(ex);
        }
    }

    private static readonly ConcurrentDictionary<string, Track> _tracksCache = new ConcurrentDictionary<string, Track>();


    public override void Deconstruct()
    {
        _disposable.Dispose();
    }

    public override ServiceType Service => ServiceType.Spotify;
}