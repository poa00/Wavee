﻿using System.Collections.ObjectModel;
using ReactiveUI;

namespace Wavee.UI.ViewModels;

public sealed class PlaylistViewModel : ReactiveObject
{
    public bool IsInFolder { get; set; }
    public string Name { get; set; }
    public ObservableCollection<PlaylistViewModel> Items { get; set; }
}