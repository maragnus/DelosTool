using JetBrains.Annotations;

namespace Delos.ScreenSystem;

[PublicAPI]
public readonly record struct Option(string Label)
{
    public Guid Id { get; } = Guid.NewGuid();
    public override string ToString() => Label;
    
    public bool Equals(Option? other)
    {
        return other?.Id == Id;
    }
}