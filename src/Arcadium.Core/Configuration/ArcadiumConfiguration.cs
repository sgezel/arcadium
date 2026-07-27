using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Configuration;

/// <summary>
/// The complete, strongly typed Arcadium configuration.
/// </summary>
public sealed class ArcadiumConfiguration
{
    public required Cabinet Cabinet { get; init; }

    public required IReadOnlyList<GameSystem> Systems { get; init; }

    public required IReadOnlyList<Emulator> Emulators { get; init; }
}
