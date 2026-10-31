using System.Diagnostics.CodeAnalysis;

namespace Faber.Modules.Resumes.Application.Features.Documents.Shared;

/// <summary>A rendered resume PDF, cached so download and generate share a single render.</summary>
/// <param name="Content">The rendered PDF bytes.</param>
/// <param name="FileName">The file name suggested to the client for the download.</param>
[SuppressMessage(
    "Performance",
    "CA1819:Properties should not return arrays",
    Justification = "Immutable rendered payload that is cached and serialized as a byte array.")]
public record ResumePdf(byte[] Content, string FileName);
