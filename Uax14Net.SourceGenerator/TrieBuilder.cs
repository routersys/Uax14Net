using System.Collections.Generic;

namespace Uax14Net.SourceGenerator;

internal readonly struct TrieResult
{
    public readonly int BlockShift;
    public readonly ushort[] Stage1;
    public readonly ushort[] Stage2;

    public TrieResult(int blockShift, ushort[] stage1, ushort[] stage2)
    {
        BlockShift = blockShift;
        Stage1 = stage1;
        Stage2 = stage2;
    }

    public long ByteLength => (long)Stage1.Length * 2 + (long)Stage2.Length * 2;
}

internal static class TrieBuilder
{
    public static TrieResult Build(ushort[] values, IEnumerable<int> shiftCandidates)
    {
        TrieResult best = default;
        bool has = false;
        foreach (int shift in shiftCandidates)
        {
            TrieResult candidate = BuildOne(values, shift);
            if (!has || candidate.ByteLength < best.ByteLength)
            {
                best = candidate;
                has = true;
            }
        }
        return best;
    }

    private static TrieResult BuildOne(ushort[] values, int shift)
    {
        int blockSize = 1 << shift;
        int total = values.Length;
        int topCount = (total + blockSize - 1) / blockSize;
        var stage1 = new ushort[topCount];
        var stage2 = new List<ushort>();
        var map = new Dictionary<BlockKey, ushort>();

        for (int b = 0; b < topCount; b++)
        {
            var block = new ushort[blockSize];
            int start = b * blockSize;
            for (int i = 0; i < blockSize; i++)
            {
                int idx = start + i;
                block[i] = idx < total ? values[idx] : (ushort)0;
            }

            var key = new BlockKey(block);
            if (!map.TryGetValue(key, out ushort blockIndex))
            {
                blockIndex = (ushort)(stage2.Count / blockSize);
                map[key] = blockIndex;
                stage2.AddRange(block);
            }
            stage1[b] = blockIndex;
        }

        return new TrieResult(shift, stage1, stage2.ToArray());
    }

    private readonly struct BlockKey : System.IEquatable<BlockKey>
    {
        private readonly ushort[] _data;
        private readonly int _hash;

        public BlockKey(ushort[] data)
        {
            _data = data;
            int h = 17;
            for (int i = 0; i < data.Length; i++)
            {
                h = h * 31 + data[i];
            }
            _hash = h;
        }

        public bool Equals(BlockKey other)
        {
            if (_hash != other._hash || _data.Length != other._data.Length)
            {
                return false;
            }
            for (int i = 0; i < _data.Length; i++)
            {
                if (_data[i] != other._data[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is BlockKey other && Equals(other);

        public override int GetHashCode() => _hash;
    }
}
