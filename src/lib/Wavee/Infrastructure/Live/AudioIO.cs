﻿using NAudio.Wave;

namespace Wavee.Infrastructure.Live;

internal readonly struct AudioIO : Traits.AudioIO
{
    private readonly NAudioHolder _nAudioHolder;
    private readonly AudioSamplesConverter _audioSamplesConverter;

    public AudioIO(NAudioHolder nAudioHolder)
    {
        _nAudioHolder = nAudioHolder;
        _audioSamplesConverter = new AudioSamplesConverter();
    }

    public async ValueTask<Unit> Write(ReadOnlyMemory<double> data)
    {
        await _nAudioHolder.Write(Either<ReadOnlyMemory<double>, ReadOnlyMemory<byte>>.Left(data),
            _audioSamplesConverter);
        return unit;
    }
    public async ValueTask<Unit> Write(ReadOnlyMemory<byte> data)
    {
        await _nAudioHolder.Write(Either<ReadOnlyMemory<double>, ReadOnlyMemory<byte>>.Right(data),
            _audioSamplesConverter);
        return unit;
    }

    public Unit Start()
    {
        _nAudioHolder.Start();
        return unit;
    }

    public Unit Stop()
    {
        _nAudioHolder.Stop();
        return unit;
    }
}