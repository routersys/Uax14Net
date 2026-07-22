using System;

namespace Uax14Net;

public interface IComplexContextResolver
{
    void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore);
}
