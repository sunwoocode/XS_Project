using UnityEngine;

public enum GridUnitTeam
{
    Player,
    Enemy
}

public sealed class GridUnit : MonoBehaviour
{
    public const int MaxActionPoints = 2;
    public const float HeightMeters = 1.8f;
    public const float RadiusMeters = 0.3f;
    public const float DiameterMeters = RadiusMeters * 2f;

    [SerializeField, Min(1)] private int unitNumber = 1;
    [SerializeField] private GridUnitTeam team = GridUnitTeam.Player;
    [SerializeField, Range(0, MaxActionPoints)] private int remainingActionPoints = MaxActionPoints;

    public int UnitNumber => unitNumber;
    public GridUnitTeam Team => team;
    public bool IsPlayerControlled => team == GridUnitTeam.Player;
    public string DisplayName => team == GridUnitTeam.Player ? $"Unit {unitNumber}" : $"Enemy {unitNumber}";
    public int RemainingActionPoints => remainingActionPoints;

    public void Configure(int number, GridUnitTeam targetTeam = GridUnitTeam.Player)
    {
        unitNumber = Mathf.Max(1, number);
        team = targetTeam;
        ResetActionPoints();
    }

    public bool TrySpendActionPoint()
    {
        if (remainingActionPoints <= 0)
        {
            return false;
        }

        remainingActionPoints--;
        return true;
    }

    public void ResetActionPoints()
    {
        remainingActionPoints = MaxActionPoints;
    }

    private void OnValidate()
    {
        unitNumber = Mathf.Max(1, unitNumber);
        remainingActionPoints = Mathf.Clamp(remainingActionPoints, 0, MaxActionPoints);
    }
}
