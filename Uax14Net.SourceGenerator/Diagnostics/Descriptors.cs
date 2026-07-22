using Microsoft.CodeAnalysis;
using Uax14Net.SourceGenerator.Resources;

namespace Uax14Net.SourceGenerator.Diagnostics;

internal static class Descriptors
{
    private const string Category = "Uax14Net";

    internal static readonly DiagnosticDescriptor ReferenceDataNotFound = Create("UAX14001", nameof(Strings.UAX14001_Title), nameof(Strings.UAX14001_Message), DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor ReferenceDataParseError = Create("UAX14002", nameof(Strings.UAX14002_Title), nameof(Strings.UAX14002_Message), DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Create(string id, string titleResourceName, string messageResourceName, DiagnosticSeverity severity) =>
        new(
            id,
            new LocalizableResourceString(titleResourceName, Strings.ResourceManager, typeof(Strings)),
            new LocalizableResourceString(messageResourceName, Strings.ResourceManager, typeof(Strings)),
            Category,
            severity,
            isEnabledByDefault: true);
}
