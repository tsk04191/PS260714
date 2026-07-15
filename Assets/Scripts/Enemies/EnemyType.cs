public enum EEnemyType
{
    Basic = 0,
    Assault = 1,
    Heavy = 2,
    Medic = 3,
    Mechanic = 4,
}

public static class EnemyTypeDisplay
{
    public static string GetName(EEnemyType type)
    {
        return type switch
        {
            EEnemyType.Assault => "ASSAULT",
            EEnemyType.Heavy => "HEAVY",
            EEnemyType.Medic => "MEDIC",
            EEnemyType.Mechanic => "MECHANIC",
            _ => "BASIC",
        };
    }

    public static string GetCardCode(EEnemyType type)
    {
        return type switch
        {
            EEnemyType.Assault => "AS",
            EEnemyType.Heavy => "HV",
            EEnemyType.Medic => "MD",
            EEnemyType.Mechanic => "MC",
            _ => string.Empty,
        };
    }
}
