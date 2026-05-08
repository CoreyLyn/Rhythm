using System;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class RhythmStateService
{
    public RhythmState State { get; }

    public RhythmStateService(RhythmState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    public void Persist() => StateStore.Save(State.ToDocument());
}
