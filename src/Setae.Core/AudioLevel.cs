namespace Setae.Core;

/// <summary>
/// Converts normalized audio samples into the relative level used by Setae.
/// </summary>
public static class AudioLevel
{
    public const float MinimumDbfs = -60f;
    public const float MaximumDbfs = 0f;
    public const float MinimumRelativeLevel = 0f;
    public const float MaximumRelativeLevel = 100f;

    private const float RmsEpsilon = 1e-12f;

    /// <summary>
    /// Calculates RMS over all samples in the buffer, including all channels.
    /// </summary>
    public static float CalculateRms(ReadOnlySpan<float> normalizedSamples)
    {
        if (normalizedSamples.IsEmpty)
        {
            return 0f;
        }

        double sumOfSquares = 0d;

        for (var index = 0; index < normalizedSamples.Length; index++)
        {
            var sample = normalizedSamples[index];

            if (!float.IsFinite(sample))
            {
                throw new ArgumentException("Las muestras deben ser valores finitos.", nameof(normalizedSamples));
            }

            var sampleAsDouble = (double)sample;
            sumOfSquares += sampleAsDouble * sampleAsDouble;
        }

        return (float)Math.Sqrt(sumOfSquares / normalizedSamples.Length);
    }

    /// <summary>
    /// Converts RMS amplitude to dBFS using the fixed reference range.
    /// </summary>
    public static float RmsToDbfs(float rms)
    {
        if (!float.IsFinite(rms) || rms < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(rms), "El RMS debe ser un valor finito no negativo.");
        }

        return 20f * MathF.Log10(MathF.Max(rms, RmsEpsilon));
    }

    /// <summary>
    /// Maps dBFS to 0..100, clamping values outside -60..0.
    /// </summary>
    public static float DbfsToRelative(float dbfs)
    {
        if (float.IsNaN(dbfs))
        {
            throw new ArgumentOutOfRangeException(nameof(dbfs), "El nivel dBFS no puede ser NaN.");
        }

        var relative = ((dbfs - MinimumDbfs) / (MaximumDbfs - MinimumDbfs)) * MaximumRelativeLevel;
        return Math.Clamp(relative, MinimumRelativeLevel, MaximumRelativeLevel);
    }

    /// <summary>
    /// Calculates the relative level directly from normalized samples.
    /// </summary>
    public static float SamplesToRelative(ReadOnlySpan<float> normalizedSamples)
    {
        return DbfsToRelative(RmsToDbfs(CalculateRms(normalizedSamples)));
    }

    /// <summary>
    /// Converts one signed 16-bit PCM sample to a normalized float sample.
    /// </summary>
    public static float Pcm16ToNormalized(short sample)
    {
        return sample / 32768f;
    }

    /// <summary>
    /// Converts signed 16-bit PCM samples without retaining the source buffer.
    /// </summary>
    public static void ConvertPcm16ToNormalized(ReadOnlySpan<short> pcmSamples, Span<float> destination)
    {
        if (destination.Length < pcmSamples.Length)
        {
            throw new ArgumentException("El destino no tiene espacio suficiente.", nameof(destination));
        }

        for (var index = 0; index < pcmSamples.Length; index++)
        {
            destination[index] = Pcm16ToNormalized(pcmSamples[index]);
        }
    }
}
