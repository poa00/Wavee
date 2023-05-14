﻿using Wavee.Core.Contracts;

namespace Wavee.Core.Infrastructure.Traits;

public interface AudioOutputIO
{
    Unit Start();

    Unit Pause();

    //ValueTask<Unit> WriteSamples(ReadOnlySpan<float> samples, CancellationToken ct = default);
    Task PlayStream(IAudioStream stream, Action<TimeSpan> onPositionChanged, bool closeOtherStreams);
    TimeSpan Position();
    Unit Seek(TimeSpan seekPosition);
}

/// <summary>
/// Type-class giving a struct the trait of supporting Audio IO
/// </summary>
/// <typeparam name="RT">Runtime</typeparam>
[Typeclass("*")]
public interface HasAudioOutput<RT> : HasCancel<RT>
    where RT : struct, HasCancel<RT>
{
    /// <summary>
    /// Access the audio synchronous effect environment
    /// </summary>
    /// <returns>Audio synchronous effect environment</returns>
    Eff<RT, Option<AudioOutputIO>> AudioOutputEff { get; }
}