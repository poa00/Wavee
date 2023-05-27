﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using LanguageExt.UnsafeValueAccess;
using ReactiveUI;
using Wavee.Core.Contracts;
using Wavee.Core.Ids;
using Wavee.Spotify.Infrastructure.Remote.Messaging;
using Wavee.Spotify.Models.Response;
using Wavee.UI.Infrastructure.Sys;
using Wavee.UI.Infrastructure.Traits;
using Wavee.UI.Services;

namespace Wavee.UI.ViewModels.Library;

public sealed class LibrarySongsViewModel<R> :
    ReactiveObject where R : struct, HasSpotify<R>, HasFile<R>, HasDirectory<R>, HasLocalPath<R>
{
    private readonly IDisposable _cleanup;
    private string? _searchText;
    private TrackSortType _sortParameters;
    private readonly ReadOnlyObservableCollection<LibraryTrack> _data;

    public LibrarySongsViewModel(R runtime)
    {
        SortParameters = TrackSortType.OriginalIndex_Desc;
        var filterApplier =
            this.WhenValueChanged(t => t.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Select(propargs => BuildFilter(propargs))
                .ObserveOn(RxApp.TaskpoolScheduler);

        var sortChange =
            this.WhenValueChanged(t => t.SortParameters)
                .Select(c => c switch
                {
                    TrackSortType.OriginalIndex_Asc or TrackSortType.Added_Asc =>
                        SortExpressionComparer<LibraryTrack>.Ascending(x => x.Added),
                    TrackSortType.OriginalIndex_Desc or TrackSortType.Added_Desc =>
                        SortExpressionComparer<LibraryTrack>.Descending(x => x.Added),
                    TrackSortType.Title_Asc =>
                        SortExpressionComparer<LibraryTrack>.Ascending(x => x.Track.Title),
                    TrackSortType.Title_Desc =>
                        SortExpressionComparer<LibraryTrack>.Descending(x => x.Track.Title),
                    TrackSortType.Artist_Asc =>
                        SortExpressionComparer<LibraryTrack>.Ascending(x => x.Track.Artists.First().Name),
                    TrackSortType.Artist_Desc =>
                        SortExpressionComparer<LibraryTrack>.Descending(x => x.Track.Artists.First().Name),
                    TrackSortType.Album_Asc =>
                        SortExpressionComparer<LibraryTrack>.Ascending(x => x.Track.Album.Name),
                    TrackSortType.Album_Desc =>
                        SortExpressionComparer<LibraryTrack>.Descending(x => x.Track.Album.Name),
                    TrackSortType.Duration_Asc =>
                        SortExpressionComparer<LibraryTrack>.Ascending(x => x.Track.Duration),
                    TrackSortType.Duration_Desc =>
                            SortExpressionComparer<LibraryTrack>.Descending(x => x.Track.Duration),
                    _ => throw new ArgumentOutOfRangeException()
                })
                .ObserveOn(RxApp.TaskpoolScheduler);
        var country = Spotify<R>.CountryCode().Run(runtime).Result.ThrowIfFail().ValueUnsafe();
        var cdnUrl = Spotify<R>.CdnUrl().Run(runtime).ThrowIfFail().ValueUnsafe();
        var playCommand = ReactiveCommand.CreateFromTask<AudioId>(Execute);

        _cleanup = Library.Items
             .Filter(c => c.Id.Type is AudioItemType.Track)
             .TransformAsync(async item =>
             {
                 var response = await TrackEnqueueService<R>.GetTrack(item.Id);
                 var tr = SpotifyTrackResponse.From(country, cdnUrl,
                     response.Value.Match(Left: _ => throw new NotSupportedException(), Right: r => r));

                 return new LibraryTrack
                 {
                     Track = tr,
                     Added = item.AddedAt,
                     PlayCommand = playCommand
                 };
             })
             .Filter(filterApplier)
             .Sort(sortChange)
             .ObserveOn(RxApp.MainThreadScheduler)
             .Bind(out _data)     // update observable collection bindings
             .DisposeMany()   //dispose when no longer required
             .Subscribe();
    }

    private async Task Execute(AudioId id)
    {
        var index = Library.GetLibraryItems().OrderByDescending(x=> x.AddedAt)
            .Select(c => c.Id).IndexOf(id);

        //spotify:user:7ucghdgquf6byqusqkliltwc2:collection
        var userId = ShellViewModel<R>.Instance.User.Id;
        var ctxId = $"spotify:user:{userId}:collection";
        var context = new PlayContextStruct(
            ContextId: ctxId,
            Index: index,
            TrackId: id,
            ContextUrl: $"context://{ctxId}",
            NextPages: None,
            PageIndex: None
        );

        await ShellViewModel<R>.Instance.Playback.PlayContextAsync(context);
    }

    public ReadOnlyObservableCollection<LibraryTrack> Data => _data;
    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public TrackSortType SortParameters
    {
        get => _sortParameters;
        set => this.RaiseAndSetIfChanged(ref _sortParameters, value);
    }
    public LibraryViewModel<R> Library => ShellViewModel<R>
        .Instance.Library;
    private static Func<LibraryTrack, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return _ => true;
        return t => t.Track.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || t.Track.Album.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || t.Track.Artists.Any(a => a.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
    public void Dispose()
    {
        _cleanup.Dispose();
    }
}

public class LibraryTrack : INotifyPropertyChanged
{
    private int _index;
    public required ITrack Track { get; init; }
    public required DateTimeOffset Added { get; init; }

    public int OriginalIndex
    {
        get => _index;
        set => this.SetField(ref _index, value);
    }

    public required ICommand PlayCommand { get; init; }

    public string GetSmallestImage(ITrack track)
    {
        return track.Album.Artwork.OrderBy(i => i.Width).First().Url;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public string FormatToRelativeDate(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset.ToString("g");
    }

    public string FormatToShorterTimestamp(TimeSpan timeSpan)
    {
        //only show minutes and seconds
        return timeSpan.ToString(@"mm\:ss");
    }
}

public enum TrackSortType
{
    OriginalIndex_Asc,
    OriginalIndex_Desc,
    Title_Asc,
    Title_Desc,
    Artist_Asc,
    Artist_Desc,
    Album_Asc,
    Album_Desc,
    Duration_Asc,
    Duration_Desc,
    Added_Asc,
    Added_Desc
}