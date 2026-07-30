using Arcadium.Core.Mame;

namespace Arcadium.Core.Models;

/// <summary>A scanned rom combined with its MAME machine metadata, when available.</summary>
public sealed class LibraryRom
{
    public required RomRecord Rom { get; init; }

    /// <summary>Null for non-MAME systems or when no machine matches the rom's basename.</summary>
    public MameMachine? Machine { get; init; }
}
