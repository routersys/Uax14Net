using System;

namespace Uax14Net;

internal interface IUtfDecoder
{
    int Length { get; }

    int Decode(int index, out int length);

    bool TryGetCharRun(int start, int length, out ReadOnlySpan<char> chars);
}
