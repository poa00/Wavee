﻿using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using DynamicData.PLinq;
using Eum.Spotify.playlist4;
using Google.Protobuf;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Spotify.Metadata;
using Wavee.Core.Ids;
using Wavee.Spotify.Infrastructure.Playback;
using Wavee.UI.Infrastructure.Live;
using Wavee.UI.Infrastructure.Sys;
using Wavee.UI.Services;
using ReactiveUI;

namespace Wavee.UI.ViewModels;

public interface IPlaylistViewModel : INavigableViewModel
{
    bool IsInFolder { get; }
    bool IsFolder { get; }
    string Id { get; }
    string OwnerId { get; }
    Seq<IPlaylistViewModel> SubItems { get; }
    ByteString Revision { get; }
    DateTimeOffset Timestamp { get; }
    int Index { get; set; }
    string Name { get; set; }
    void AddSubitem(IPlaylistViewModel playlistViewModel);

    void Destroy();
}
public sealed class PlaylistViewModel : ReactiveObject, IPlaylistViewModel
{
    private readonly IDisposable _listener;

    private readonly Subject<Diff> _dummySubj = new();
    private readonly SourceCache<(Item Item, int Index), ByteString> _items = new(s => s.Item.Attributes.ItemId);
    private readonly WaveeUIRuntime _runtime;
    public PlaylistViewModel(string id, WaveeUIRuntime runtime, bool isFolder = false)
    {
        Id = id;
        _runtime = runtime;
        //setup a listener
        IsFolder = isFolder;
        if (!isFolder)
        {
            var a = Spotify<WaveeUIRuntime>
                .ObservePlaylist(AudioId.FromUri(id))
                .Run(runtime)
                .ThrowIfFail()
                .ValueUnsafe();
            var b = _dummySubj;

            var merged = a.Merge(b);
            _listener =
                merged
                .SelectMany(async x =>
                {
                    //if add: fetc from cache
                    var tracksToFetch = x.
                        Ops.Where(op => op.Kind == Op.Types.Kind.Add)
                        .SelectMany(op => op.Add.Items)
                        .Select(x => AudioId.FromUri(x.Uri))
                        .Where(c => !Cache.ContainsKey(c))
                        .ToSeq();
                    if (tracksToFetch.Count > 0)
                    {
                        var tracks = await TrackEnqueueService<WaveeUIRuntime>.GetTracks(tracksToFetch);
                        foreach (var track in tracks)
                        {
                            Cache[track.Key] = track.Value;
                        }
                    }

                    return x;
                })
                .Select(c =>
                {
                    _items.Edit(updater =>
                    {
                        var oldList = updater.Items.ToList();
                        foreach (var op in c.Ops)
                        {
                            switch (op.Kind)
                            {
                                case Op.Types.Kind.Add:
                                    var projected = op.Add.Items.Select((f, i) => (f, i));
                                    if (op.Add.AddFirst)
                                    {
                                        oldList.InsertRange(0, projected);
                                    }
                                    else if (op.Add.AddLast)
                                    {
                                        oldList.AddRange(projected);
                                    }
                                    else if (op.Add.HasFromIndex)
                                    {
                                        oldList.InsertRange(op.Add.FromIndex, projected);
                                    }
                                    break;
                                case Op.Types.Kind.Rem:
                                    if (op.Rem.HasFromIndex)
                                    {
                                        oldList.RemoveRange(op.Rem.FromIndex, op.Rem.Length);
                                    }
                                    else
                                    {
                                        oldList.RemoveRange(0, op.Rem.Length);
                                    }
                                    break;
                                case Op.Types.Kind.Mov:
                                    break;
                                case Op.Types.Kind.UpdateItemAttributes:
                                    break;
                                case Op.Types.Kind.UpdateListAttributes:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        updater.Load(oldList);
                    });
                    // GC.Collect();
                    return unit;
                })
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe();
        }
    }

    private static readonly Dictionary<AudioId, Option<TrackOrEpisode>> Cache = new();
    private List<PlaylistTrackViewModel> _view;

    private IDisposable uiListener;
    public List<PlaylistTrackViewModel> View => _view;
    public async Task SetupForUI()
    {
        //build observable sort comparer
        var sort = SortParameters.WhenValueChanged(t => t.SelectedItem)
            .Select(prop => prop.Comparer)
            .ObserveOn(RxApp.TaskpoolScheduler);

        // var pager =
        //     PageParameters
        //         .WhenChanged(vm => vm.PageSize, vm => vm.CurrentPage,
        //             (_, size, pge) => new PageRequest(pge, size))
        //         .StartWith(new PageRequest(1, 25));
        var latestPlaylist = (await Spotify<WaveeUIRuntime>
                .GetPlaylistMaybeCached(AudioId.FromUri(Id))
                .Run(runtime: _runtime))
            .ThrowIfFail();
        if (latestPlaylist.FromCache)
        {
            //diff
        }

        var newItems = latestPlaylist.Playlist.Contents.Items
            .Select(x => AudioId.FromUri(x.Uri))
            .Where(c => !Cache.ContainsKey(c))
            .ToSeq();
        if (newItems.Count > 0)
        {
            var tracks = await TrackEnqueueService<WaveeUIRuntime>.GetTracks(newItems);
            foreach (var track in tracks)
            {
                Cache[track.Key] = track.Value;
            }
        }

        _view = latestPlaylist.Playlist.Contents.Items.Select(
            GetFrom).ToList();

        static PlaylistTrackViewModel GetFrom(Item x, int i)
        {
            var id = AudioId.FromUri(x.Uri);
            var track = Cache[id];
            //by now, we should have it in cache
            var trck = track.ValueUnsafe();
            return new PlaylistTrackViewModel
            {
                Index = i,
                Id = id,
                Name = trck
                    .Name,
                Artists = trck.Artists.Select(c => new PlaylistShortItem
                {
                    Id = c.Id,
                    Name = c.Name
                }).ToArray(),
                Album = new PlaylistShortItem
                {
                    Id = trck.Group.Id,
                    Name = trck.Group.Name
                },
                Duration = trck.Duration,
                AddedAt = x.Attributes.HasTimestamp ? DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes.Timestamp) :
                    DateTimeOffset.MinValue,
                HasAddedAt = x.Attributes.HasTimestamp,
                SmallestImage = trck.GetImage(Image.Types.Size.Small)
            };
        }
        //
        // uiListener = _items
        //     .Connect()
        //     //.ObserveOn(RxApp.TaskpoolScheduler)
        //     .Transform(static yx => GetFrom(yx.Item, yx.Index), new ParallelisationOptions(ParallelType.Ordered, 5))
        //     .Sort(sort)
        //     .ObserveOn(RxApp.MainThreadScheduler)
        //     // .Do(changes => PageParameters.Update(changes.Response))
        //     .Bind(out _view)
        //     .DisposeMany()
        //     .Subscribe();
    }

    public PageParameterData PageParameters { get; } = new PageParameterData(1, 25);
    public SortParameterData SortParameters { get; } = new SortParameterData();

    public void DestroyForUI()
    {
        uiListener?.Dispose();
        //_cache.Clear();
    }

    public required bool IsInFolder { get; init; }
    public required bool IsFolder { get; init; }
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required Seq<IPlaylistViewModel> SubItems { get; set; }
    public required ByteString Revision { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public int Index { get; set; }
    public string Name { get; set; }
    public void AddSubitem(IPlaylistViewModel playlistViewModel)
    {
        SubItems = SubItems.Add(playlistViewModel);
    }

    public void Destroy()
    {
        _listener.Dispose();
        _items.Clear();
        _items.Dispose();

        foreach (var subItem in SubItems)
        {
            subItem.Destroy();
        }
    }

    public void OnNavigatedTo(object? parameter)
    {

    }
}

public readonly record struct PlaylistShortItem
{
    public required AudioId Id { get; init; }
    public required string Name { get; init; }
}

public class PlaylistTrackViewModel
{
    public required int Index { get; init; }
    public required AudioId Id { get; init; }
    public required string Name { get; init; }
    public required PlaylistShortItem[] Artists { get; init; }
    public required PlaylistShortItem Album { get; init; }
    public required TimeSpan Duration { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
    public required bool HasAddedAt { get; init; }
    public required string SmallestImage { get; init; }

    public string FormatToRelativeDate(DateTimeOffset dateTimeOffset)
    {
        //less than 10 seconds: "Just now"
        //less than 1 minute: "X seconds ago"
        //less than 1 hour: "X minutes ago" OR // "1 minute ago"
        //less than 1 day: "X hours ago" OR // "1 hour ago"
        //less than 1 week: "X days ago" OR // "1 day ago"
        //Exact date

        var totalSeconds = (int)DateTimeOffset.Now.Subtract(dateTimeOffset).TotalSeconds;
        var totalMinutes = totalSeconds / 60;
        var totalHours = totalMinutes / 60;
        var totalDays = totalHours / 24;
        var totalWeeks = totalDays / 7;
        return dateTimeOffset switch
        {
            _ when dateTimeOffset > DateTimeOffset.Now.AddSeconds(-10) => "Just now",
            _ when dateTimeOffset > DateTimeOffset.Now.AddMinutes(-1) =>
                $"{totalSeconds} second{(totalSeconds > 1 ? "s" : "")} ago",
            _ when dateTimeOffset > DateTimeOffset.Now.AddHours(-1) =>
                $"{totalMinutes} minute{(totalMinutes > 1 ? "s" : "")} ago",
            _ when dateTimeOffset > DateTimeOffset.Now.AddDays(-1) =>
                $"{totalHours} hour{(totalHours > 1 ? "s" : "")} ago",
            _ when dateTimeOffset > DateTimeOffset.Now.AddDays(-7) =>
                $"{totalDays} day{(totalDays > 1 ? "s" : "")} ago",
            _ when dateTimeOffset > DateTimeOffset.Now.AddMonths(-1) =>
                $"{totalWeeks} week{(totalWeeks > 1 ? "s" : "")} ago",
            _ => GetFullMonthStr(dateTimeOffset)
        };

        static string GetFullMonthStr(DateTimeOffset d)
        {
            string fullMonthName =
                d.ToString("MMMM");
            return $"{fullMonthName} {d.Day}, {d.Year}";
        }
    }

    public string FormatToShorterTimestamp(TimeSpan timeSpan)
    {
        //only show minutes and seconds
        return timeSpan.ToString(@"mm\:ss");
    }
}