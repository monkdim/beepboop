namespace TwoButton.Core.Model;

/// <summary>
/// A reference to a buff or debuff. Verified and repaired by name at startup in the
/// same way as <see cref="ActionRef"/>.
/// </summary>
public sealed class StatusRef(uint id, string name)
{
    public uint Id { get; private set; } = id;

    public string Name { get; } = name;

    public bool Verified { get; private set; }

    public bool WasRepaired { get; private set; }

    public void Bind(uint verifiedId)
    {
        if (verifiedId != Id)
        {
            Id = verifiedId;
            WasRepaired = true;
        }

        Verified = true;
    }

    public static implicit operator uint(StatusRef status) => status.Id;

    public override string ToString() => $"{Name} ({Id})";
}
