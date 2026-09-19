using System.Buffers.Binary;
using System.Text;
using Setae.App.Infrastructure;

namespace Setae.App.Tests;

public class BeepPlayerTests
{
    [Fact]
    public void CreateWavData_ProducesValidWavHeader()
    {
        var data = BeepPlayer.CreateWavData();

        Assert.Equal("RIFF", ReadAscii(data, 0, 4));
        Assert.Equal(data.Length - 8, ReadInt32(data, 4));
        Assert.Equal("WAVE", ReadAscii(data, 8, 4));
        Assert.Equal("fmt ", ReadAscii(data, 12, 4));
        Assert.Equal(16, ReadInt32(data, 16));
        Assert.Equal(1, ReadInt16(data, 20));
        Assert.Equal(1, ReadInt16(data, 22));
        Assert.Equal(44_100, ReadInt32(data, 24));
        Assert.Equal(16, ReadInt16(data, 34));
        Assert.Equal("data", ReadAscii(data, 36, 4));
        Assert.Equal(data.Length - 44, ReadInt32(data, 40));
    }

    [Fact]
    public void CreateWavData_ContainsNonSilentSamples()
    {
        var data = BeepPlayer.CreateWavData();

        Assert.Contains(data.Skip(44), value => value != 0);
    }

    [Fact]
    public void CreateWavData_HeaderDurationIsApproximately350Milliseconds()
    {
        var data = BeepPlayer.CreateWavData();

        var sampleRate = ReadInt32(data, 24);
        var blockAlign = ReadInt16(data, 32);
        var dataSize = ReadInt32(data, 40);
        var durationSeconds = dataSize / (double)(sampleRate * blockAlign);

        Assert.InRange(durationSeconds, 0.34, 0.36);
    }

    private static short ReadInt16(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
    }

    private static int ReadInt32(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
    }

    private static string ReadAscii(byte[] data, int offset, int length)
    {
        return Encoding.ASCII.GetString(data, offset, length);
    }
}