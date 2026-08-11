using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Iridium.Primitives;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Uuid : IEquatable<Uuid> {
    private static readonly uint[] TableToHex = CreateHexTable();

    private readonly ulong _high;
    private readonly ulong _low;
    
    private static uint[] CreateHexTable() {
        var table = new uint[256];

        for (var i = 0; i < 256; i++) {
            var high = (byte)(i >> 4);
            var low = (byte)(i & 0xF);

            var c1 = (char)(high < 10 ? '0' + high : 'a' + high - 10);
            var c2 = (char)(low < 10 ? '0' + low : 'a' + low - 10);

            table[i] = c1 | ((uint)c2 << 16);
        }

        return table;
    }
    
    public Uuid(byte[] bytes) {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != 16)
            throw new ArgumentException("Uuid requires exactly 16 bytes.", 
                nameof(bytes));

        _high = Unsafe.ReadUnaligned<ulong>(
            ref MemoryMarshal.GetArrayDataReference(bytes));

        _low = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(
            ref MemoryMarshal.GetArrayDataReference(bytes), 
            8));
    }

    public Uuid(ReadOnlySpan<byte> bytes) {
        if (bytes.Length != 16)
            throw new ArgumentException("Uuid requires exactly 16 bytes.",
                nameof(bytes));

        ref var reference = ref MemoryMarshal.GetReference(bytes);

        _high = Unsafe.ReadUnaligned<ulong>(ref reference);

        _low =
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref reference, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<byte> destination) {
        if (destination.Length < 16)
            throw new ArgumentException("Destination must contain at least 16 bytes.",
                nameof(destination));
        
        ref var reference = ref MemoryMarshal.GetReference(destination);

        Unsafe.WriteUnaligned(ref reference, _high);

        Unsafe.WriteUnaligned(
            ref Unsafe.Add(ref reference, 8), _low);
    }
    
    public override string ToString() {
        string result = new('\0', 32);
        unsafe {
            fixed (char* chars = result) {
                Format(chars);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void Format(char* destination) {
        Span<byte> bytes = stackalloc byte[16];
        CopyTo(bytes);

        var output = (uint*)destination;

        output[0] = TableToHex[bytes[0]];
        output[1] = TableToHex[bytes[1]];
        output[2] = TableToHex[bytes[2]];
        output[3] = TableToHex[bytes[3]];
        output[4] = TableToHex[bytes[4]];
        output[5] = TableToHex[bytes[5]];
        output[6] = TableToHex[bytes[6]];
        output[7] = TableToHex[bytes[7]];
        output[8] = TableToHex[bytes[8]];
        output[9] = TableToHex[bytes[9]];
        output[10] = TableToHex[bytes[10]];
        output[11] = TableToHex[bytes[11]];
        output[12] = TableToHex[bytes[12]];
        output[13] = TableToHex[bytes[13]];
        output[14] = TableToHex[bytes[14]];
        output[15] = TableToHex[bytes[15]];
    }


    public bool Equals(Uuid other) {
        return _high == other._high && _low == other._low;
    }
    
    public override bool Equals(object? obj) {
        return obj is Uuid other && Equals(other);
    }
    
    public override int GetHashCode() {
        return HashCode.Combine(_high, _low);
    }

    public static bool operator ==(Uuid left, Uuid right) => left.Equals(right);
    
    public static bool operator !=(Uuid left, Uuid right) => !left.Equals(right);
}