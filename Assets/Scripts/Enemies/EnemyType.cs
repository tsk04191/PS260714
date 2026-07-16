public enum EEnemyType
{
    Basic = 0,
    Assault = 1,
    Heavy = 2,
    Medic = 3,
    Mechanic = 4,
    Pointman = 5,
    ShieldBearer = 6,
    Infiltrator = 7,
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
            EEnemyType.Pointman => "POINTMAN",
            EEnemyType.ShieldBearer => "SHIELD BEARER",
            EEnemyType.Infiltrator => "INFILTRATOR",
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
            EEnemyType.Pointman => "PM",
            EEnemyType.ShieldBearer => "SH",
            EEnemyType.Infiltrator => "IF",
            _ => string.Empty,
        };
    }
}
