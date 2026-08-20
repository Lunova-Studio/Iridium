namespace Iridium.Helpers.Resources;


public static class CurseForgeFingerprintHelper {

    public static uint Compute(ReadOnlySpan<byte> bytes) {
        Span<byte> buffer = bytes.Length <= 8192 ? stackalloc byte[bytes.Length] : new byte[bytes.Length];
        var length = 0;
        foreach (var value in bytes) {
            if (value is 0x09 or 0x0A or 0x0D or 0x20)
                continue;
            buffer[length++] = value;
        }

        return MurmurHash2(buffer[..length]);
    }


    public static async Task<uint> ComputeAsync(Stream stream, CancellationToken cancellationToken = default) {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Compute(buffer.ToArray());
    }


    public static uint MurmurHash2(ReadOnlySpan<byte> data, uint seed = 1) {
        const uint multiplier = 0x5BD1E995;
        const int rotation = 24;
        var length = data.Length;
        var hash = seed ^ (uint)length;
        var offset = 0;

        while (length >= 4) {
            var k = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
            k *= multiplier;
            k ^= k >> rotation;
            k *= multiplier;
            hash *= multiplier;
            hash ^= k;
            offset += 4;
            length -= 4;
        }

        switch (length) {
            case 3:
                hash ^= (uint)data[offset + 2] << 16;
                goto case 2;
            case 2:
                hash ^= (uint)data[offset + 1] << 8;
                goto case 1;
            case 1:
                hash ^= data[offset];
                hash *= multiplier;
                break;
        }

        hash ^= hash >> 13;
        hash *= multiplier;
        hash ^= hash >> 15;
        return hash;
    }
}
