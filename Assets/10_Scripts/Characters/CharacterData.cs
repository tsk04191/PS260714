using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterCumulativeUpgradeProgress
{
    [SerializeField] private string upgradeId;
    [SerializeField, Min(0)] private int level;

    public string UpgradeId => upgradeId ?? string.Empty;
    public int Level => Mathf.Max(0, level);

    public CharacterCumulativeUpgradeProgress(string id, int value)
    {
        upgradeId = id ?? string.Empty;
        level = Mathf.Max(0, value);
    }

    public void SetLevel(int value)
    {
        level = Mathf.Max(0, value);
    }

    internal bool Normalize()
    {
        upgradeId = (upgradeId ?? string.Empty).Trim();
        level = Mathf.Max(0, level);
        return !string.IsNullOrWhiteSpace(upgradeId);
    }
}

[Serializable]
public sealed class CharacterProgressData
{
    [SerializeField] private string characterId;
    [SerializeField] private bool isOwned;
    [SerializeField, Range(0, 100)] private int trust;
    [SerializeField]
    private List<CharacterCumulativeUpgradeProgress> cumulativeUpgrades =
        new();

    public string CharacterId => characterId ?? string.Empty;
    public bool IsOwned => isOwned;
    public int Trust => Mathf.Clamp(trust, 0, 100);
    public IReadOnlyList<CharacterCumulativeUpgradeProgress>
        CumulativeUpgrades
    {
        get
        {
            cumulativeUpgrades ??=
                new List<CharacterCumulativeUpgradeProgress>();
            return cumulativeUpgrades;
        }
    }

    public CharacterProgressData(string id, bool owned = false)
    {
        characterId = id ?? string.Empty;
        isOwned = owned;
    }

    public void SetOwned(bool value)
    {
        isOwned = value;
    }

    public void SetTrust(int value)
    {
        trust = Mathf.Clamp(value, 0, 100);
    }

    public int GetCumulativeUpgradeLevel(string upgradeId)
    {
        CharacterCumulativeUpgradeProgress progress = FindUpgrade(upgradeId);
        return progress?.Level ?? 0;
    }

    public void SetCumulativeUpgradeLevel(string upgradeId, int level)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return;

        cumulativeUpgrades ??=
            new List<CharacterCumulativeUpgradeProgress>();
        CharacterCumulativeUpgradeProgress progress = FindUpgrade(upgradeId);
        if (progress == null)
        {
            cumulativeUpgrades.Add(new CharacterCumulativeUpgradeProgress(
                upgradeId,
                level));
            return;
        }

        progress.SetLevel(level);
    }

    public int AddCumulativeUpgradeLevel(string upgradeId, int amount = 1)
    {
        long total =
            (long)GetCumulativeUpgradeLevel(upgradeId) + amount;
        int nextLevel = (int)Math.Min(
            int.MaxValue,
            Math.Max(0L, total));
        SetCumulativeUpgradeLevel(upgradeId, nextLevel);
        return nextLevel;
    }

    internal bool Normalize()
    {
        characterId = (characterId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        trust = Mathf.Clamp(trust, 0, 100);

        cumulativeUpgrades ??=
            new List<CharacterCumulativeUpgradeProgress>();
        List<CharacterCumulativeUpgradeProgress> normalized = new();
        Dictionary<string, CharacterCumulativeUpgradeProgress> byId =
            new(StringComparer.Ordinal);
        foreach (CharacterCumulativeUpgradeProgress progress in
                 cumulativeUpgrades)
        {
            if (progress == null || !progress.Normalize())
                continue;

            if (byId.TryGetValue(
                    progress.UpgradeId,
                    out CharacterCumulativeUpgradeProgress existing))
            {
                existing.SetLevel(Mathf.Max(
                    existing.Level,
                    progress.Level));
                continue;
            }

            byId.Add(progress.UpgradeId, progress);
            normalized.Add(progress);
        }

        cumulativeUpgrades = normalized;
        return true;
    }

    internal void MergeFrom(CharacterProgressData other)
    {
        if (other == null)
            return;

        isOwned |= other.IsOwned;
        trust = Mathf.Max(Trust, other.Trust);
        foreach (CharacterCumulativeUpgradeProgress progress in
                 other.CumulativeUpgrades)
        {
            if (progress == null)
                continue;

            SetCumulativeUpgradeLevel(
                progress.UpgradeId,
                Mathf.Max(
                    GetCumulativeUpgradeLevel(progress.UpgradeId),
                    progress.Level));
        }
    }

    internal CharacterProgressData CreateSnapshot()
    {
        CharacterProgressData snapshot = new(CharacterId, IsOwned);
        snapshot.SetTrust(Trust);
        foreach (CharacterCumulativeUpgradeProgress progress in
                 CumulativeUpgrades)
        {
            if (progress != null)
            {
                snapshot.SetCumulativeUpgradeLevel(
                    progress.UpgradeId,
                    progress.Level);
            }
        }

        return snapshot;
    }

    private CharacterCumulativeUpgradeProgress FindUpgrade(string upgradeId)
    {
        if (cumulativeUpgrades == null ||
            string.IsNullOrWhiteSpace(upgradeId))
        {
            return null;
        }

        foreach (CharacterCumulativeUpgradeProgress progress in
                 cumulativeUpgrades)
        {
            if (progress != null && string.Equals(
                    progress.UpgradeId,
                    upgradeId,
                    StringComparison.Ordinal))
            {
                return progress;
            }
        }

        return null;
    }
}

[Serializable]
internal sealed class CharacterCollectionSaveData
{
    public const int CurrentVersion = 1;

    [SerializeField] private int version = CurrentVersion;
    [SerializeField] private List<CharacterProgressData> characters = new();

    public int Version => version;
    public bool HasCharacters => characters != null;

    public List<CharacterProgressData> Characters
    {
        get
        {
            characters ??= new List<CharacterProgressData>();
            return characters;
        }
    }

    public void Normalize()
    {
        characters ??= new List<CharacterProgressData>();
        List<CharacterProgressData> normalized = new();
        Dictionary<string, CharacterProgressData> byId =
            new(StringComparer.Ordinal);
        foreach (CharacterProgressData progress in characters)
        {
            if (progress == null || !progress.Normalize())
                continue;

            if (byId.TryGetValue(
                    progress.CharacterId,
                    out CharacterProgressData existing))
            {
                existing.MergeFrom(progress);
                continue;
            }

            byId.Add(progress.CharacterId, progress);
            normalized.Add(progress);
        }

        characters = normalized;
    }
}

public enum CharacterCumulativeUpgradeChangeResult
{
    Success = 0,
    InvalidCharacter = 1,
    CharacterNotOwned = 2,
    UpgradeNotFound = 3,
    InvalidAmount = 4,
    MaxLevelReached = 5,
    PersistenceBlocked = 6
}

public sealed class CharacterCollectionData
{
    private const string PlayerPrefsKey = "Characters.Collection.v1";
    private const string BackupPlayerPrefsKey =
        PlayerPrefsKey + ".backup";
    private const string CorruptPlayerPrefsKey =
        PlayerPrefsKey + ".corrupt";

    private CharacterCollectionSaveData _saveData = new();
    private readonly Dictionary<string, List<WeakReference<CharacterData>>>
        _runtimeDataByCharacterId =
            new(StringComparer.Ordinal);
    private LocalDataLoadStatus _lastLoadStatus =
        LocalDataLoadStatus.NotLoaded;
    private bool _saveBlocked;

    public event Action<CharacterSO> CharacterProgressChanged;

    public IReadOnlyList<CharacterProgressData> Characters =>
        _saveData.Characters;
    public LocalDataLoadStatus LastLoadStatus => _lastLoadStatus;
    public bool IsSaveBlocked => _saveBlocked;

    public CharacterProgressData GetOrCreate(CharacterSO definition)
    {
        return GetOrCreate(
            definition,
            definition != null && definition.InitiallyOwned);
    }

    public CharacterProgressData GetOrCreate(
        CharacterSO definition,
        bool initiallyOwned)
    {
        if (definition == null)
            return null;

        return GetOrCreate(definition.CharacterId, initiallyOwned);
    }

    public CharacterProgressData GetOrCreate(
        string characterId,
        bool initiallyOwned = false)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        foreach (CharacterProgressData progress in _saveData.Characters)
        {
            if (progress != null && string.Equals(
                    progress.CharacterId,
                    characterId,
                    StringComparison.Ordinal))
            {
                if (initiallyOwned && !progress.IsOwned)
                    progress.SetOwned(true);
                return progress;
            }
        }

        CharacterProgressData created = new(characterId, initiallyOwned);
        _saveData.Characters.Add(created);
        return created;
    }

    public CharacterData CreateRuntimeData(CharacterSO definition)
    {
        if (definition == null)
            return null;

        CharacterData data = definition.CreateData(GetOrCreate(
            definition,
            definition.InitiallyOwned));
        RegisterRuntimeData(data);
        return data;
    }

    public CharacterData CreateDetachedRuntimeData(CharacterSO definition)
    {
        if (definition == null)
            return null;

        CharacterProgressData progress = null;
        foreach (CharacterProgressData candidate in _saveData.Characters)
        {
            if (candidate != null && string.Equals(
                    candidate.CharacterId,
                    definition.CharacterId,
                    StringComparison.Ordinal))
            {
                progress = candidate.CreateSnapshot();
                break;
            }
        }

        progress ??= new CharacterProgressData(
            definition.CharacterId,
            definition.InitiallyOwned);
        return definition.CreateData(progress);
    }

    public CharacterData CreatePreviewData(CharacterSO definition)
    {
        if (definition == null)
            return null;

        CharacterProgressData progress = GetOrCreate(
            definition,
            definition.InitiallyOwned);
        return definition.CreateData(progress?.CreateSnapshot());
    }

    public CharacterCumulativeUpgradeChangeResult
        TryAddCumulativeUpgradeLevel(
            CharacterSO definition,
            string upgradeId,
            int amount,
            out int newLevel,
            bool save = true)
    {
        newLevel = 0;
        if (_saveBlocked)
        {
            return CharacterCumulativeUpgradeChangeResult
                .PersistenceBlocked;
        }

        if (definition == null)
        {
            return CharacterCumulativeUpgradeChangeResult
                .InvalidCharacter;
        }
        if (amount <= 0)
        {
            return CharacterCumulativeUpgradeChangeResult
                .InvalidAmount;
        }

        CharacterProgressData progress = GetOrCreate(
            definition,
            definition.InitiallyOwned);
        if (progress == null)
        {
            return CharacterCumulativeUpgradeChangeResult
                .InvalidCharacter;
        }
        if (!progress.IsOwned)
        {
            return CharacterCumulativeUpgradeChangeResult
                .CharacterNotOwned;
        }

        CharacterCumulativeUpgradeDefinition upgrade =
            definition.GetCumulativeUpgradeDefinition(upgradeId);
        if (upgrade == null)
        {
            return CharacterCumulativeUpgradeChangeResult
                .UpgradeNotFound;
        }

        int currentLevel = upgrade.ClampLevel(
            progress.GetCumulativeUpgradeLevel(upgrade.UpgradeId));
        long requestedLevel = (long)currentLevel + amount;
        int targetLevel = upgrade.ClampLevel((int)Math.Min(
            int.MaxValue,
            requestedLevel));
        if (targetLevel <= currentLevel)
        {
            newLevel = currentLevel;
            return CharacterCumulativeUpgradeChangeResult
                .MaxLevelReached;
        }

        progress.SetCumulativeUpgradeLevel(
            upgrade.UpgradeId,
            targetLevel);
        newLevel = targetLevel;
        RefreshRegisteredRuntimeData(definition.CharacterId);
        if (save)
            Save();
        CharacterProgressChanged?.Invoke(definition);
        return CharacterCumulativeUpgradeChangeResult.Success;
    }

    public bool TrySetOwned(
        CharacterSO definition,
        bool isOwned,
        bool save = true)
    {
        if (_saveBlocked)
            return false;

        if (definition == null)
            return false;

        CharacterProgressData progress = GetOrCreate(
            definition,
            false);
        if (progress == null || progress.IsOwned == isOwned)
            return false;

        progress.SetOwned(isOwned);
        if (save)
            Save();
        CharacterProgressChanged?.Invoke(definition);
        return true;
    }

    public bool TrySetTrust(
        CharacterSO definition,
        int trust,
        bool save = true)
    {
        if (_saveBlocked || definition == null)
            return false;

        CharacterProgressData progress = GetOrCreate(
            definition,
            definition.InitiallyOwned);
        int nextTrust = Mathf.Clamp(trust, 0, 100);
        if (progress == null || progress.Trust == nextTrust)
            return false;

        progress.SetTrust(nextTrust);
        if (save)
            Save();
        CharacterProgressChanged?.Invoke(definition);
        return true;
    }

    public void Save(bool flush = true)
    {
        if (_saveBlocked)
        {
            Debug.LogWarning(
                "Character progress save was skipped because the primary " +
                "save data could not be loaded safely. Reset or recover " +
                "local data before saving again.");
            return;
        }

        string json = ExportJson();
        LocalSaveRecovery.BackupCurrentValidSave(
            PlayerPrefsKey,
            BackupPlayerPrefsKey,
            CorruptPlayerPrefsKey,
            json,
            IsValidSerializedSave);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        if (flush)
            PlayerPrefs.Save();
    }

    public string ExportJson()
    {
        _saveData.Normalize();
        return JsonUtility.ToJson(_saveData);
    }

    public bool TryImportJson(string json)
    {
        if (!TryDeserialize(
                json,
                out CharacterCollectionSaveData imported,
                out LocalDataLoadStatus loadStatus,
                out _))
        {
            return false;
        }

        imported.Normalize();
        ApplySaveData(imported);
        SetLoadState(loadStatus, false);
        return true;
    }

    public LocalDataLoadStatus Load()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            ApplySaveData(new CharacterCollectionSaveData());
            SetLoadState(LocalDataLoadStatus.MissingInitialized, false);
            Save();
            return _lastLoadStatus;
        }

        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (TryDeserialize(
                json,
                out CharacterCollectionSaveData imported,
                out LocalDataLoadStatus loadStatus,
                out LocalDataLoadStatus failureStatus))
        {
            imported.Normalize();
            ApplySaveData(imported);
            SetLoadState(loadStatus, false);
            return _lastLoadStatus;
        }

        if (TryRecoverFromBackup(json))
            return _lastLoadStatus;

        ApplySaveData(new CharacterCollectionSaveData());
        SetLoadState(failureStatus, true);
        Debug.LogError(
            "Character progress save data is corrupt or uses an unsupported " +
            "version. The original PlayerPrefs value was preserved and " +
            "character saving is blocked until local data is reset or " +
            "recovered.");
        return _lastLoadStatus;
    }

    private bool TryRecoverFromBackup(string corruptPrimaryJson)
    {
        if (!LocalSaveRecovery.TryRecover(
                BackupPlayerPrefsKey,
                CorruptPlayerPrefsKey,
                corruptPrimaryJson,
                TryDeserializeBackup,
                out CharacterCollectionSaveData backup))
        {
            return false;
        }

        backup.Normalize();
        ApplySaveData(backup);
        SetLoadState(LocalDataLoadStatus.RecoveredFromBackup, false);
        Debug.LogWarning(
            "Character progress was restored from its last valid backup. " +
            "The rejected primary value was preserved under the corrupt key.");
        return true;
    }

    private static bool IsValidSerializedSave(string json)
    {
        return TryDeserialize(json, out _, out _, out _);
    }

    private static bool TryDeserializeBackup(
        string json,
        out CharacterCollectionSaveData saveData)
    {
        return TryDeserialize(json, out saveData, out _, out _);
    }

    private static bool TryDeserialize(
        string json,
        out CharacterCollectionSaveData saveData,
        out LocalDataLoadStatus loadStatus,
        out LocalDataLoadStatus failureStatus)
    {
        saveData = null;
        loadStatus = LocalDataLoadStatus.Success;
        failureStatus = LocalDataLoadStatus.Corrupt;
        if (string.IsNullOrWhiteSpace(json) ||
            !LocalSaveJson.HasNonNullTopLevelProperty(json, "characters"))
        {
            return false;
        }

        bool hasVersion =
            LocalSaveJson.HasTopLevelProperty(json, "version");
        try
        {
            saveData = JsonUtility.FromJson<CharacterCollectionSaveData>(
                json);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (saveData == null || !saveData.HasCharacters)
            return false;

        if (!hasVersion)
        {
            loadStatus = LocalDataLoadStatus.Migrated;
            return true;
        }

        if (saveData.Version != CharacterCollectionSaveData.CurrentVersion)
        {
            saveData = null;
            failureStatus = LocalDataLoadStatus.UnsupportedVersion;
            return false;
        }

        return true;
    }

    private void SetLoadState(
        LocalDataLoadStatus status,
        bool saveBlocked)
    {
        _lastLoadStatus = status;
        _saveBlocked = saveBlocked;
    }

    private void RegisterRuntimeData(CharacterData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.CharacterId))
            return;

        if (!_runtimeDataByCharacterId.TryGetValue(
                data.CharacterId,
                out List<WeakReference<CharacterData>> references))
        {
            references = new List<WeakReference<CharacterData>>();
            _runtimeDataByCharacterId.Add(data.CharacterId, references);
        }

        for (int index = references.Count - 1; index >= 0; index--)
        {
            if (!references[index].TryGetTarget(out _))
                references.RemoveAt(index);
        }
        references.Add(new WeakReference<CharacterData>(data));
    }

    private void RefreshRegisteredRuntimeData(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) ||
            !_runtimeDataByCharacterId.TryGetValue(
                characterId,
                out List<WeakReference<CharacterData>> references))
        {
            return;
        }

        for (int index = references.Count - 1; index >= 0; index--)
        {
            if (!references[index].TryGetTarget(
                    out CharacterData data))
            {
                references.RemoveAt(index);
                continue;
            }

            data.RefreshCumulativeUpgradeState();
        }

        if (references.Count == 0)
            _runtimeDataByCharacterId.Remove(characterId);
    }

    private void ApplySaveData(CharacterCollectionSaveData saveData)
    {
        _saveData = saveData ?? new CharacterCollectionSaveData();
        _saveData.Normalize();
        RebindRegisteredRuntimeData();
    }

    private void RebindRegisteredRuntimeData()
    {
        List<string> emptyCharacterIds = new();
        HashSet<CharacterSO> changedDefinitions = new();
        foreach (KeyValuePair<
                     string,
                     List<WeakReference<CharacterData>>> pair in
                 _runtimeDataByCharacterId)
        {
            List<WeakReference<CharacterData>> references = pair.Value;
            CharacterProgressData progress = null;
            for (int index = references.Count - 1; index >= 0; index--)
            {
                if (!references[index].TryGetTarget(
                        out CharacterData data))
                {
                    references.RemoveAt(index);
                    continue;
                }

                progress ??= GetOrCreate(
                    data.Definition,
                    data.Definition != null &&
                    data.Definition.InitiallyOwned);
                data.RebindProgress(progress);
                if (data.Definition != null)
                    changedDefinitions.Add(data.Definition);
            }

            if (references.Count == 0)
                emptyCharacterIds.Add(pair.Key);
        }

        foreach (string characterId in emptyCharacterIds)
            _runtimeDataByCharacterId.Remove(characterId);
        foreach (CharacterSO definition in changedDefinitions)
            CharacterProgressChanged?.Invoke(definition);
    }
}

public readonly struct CharacterActionConditionData
{
    public CharacterActionLinkage Linkage { get; }
    public CharacterConditionMatchMode MatchMode { get; }
    public IReadOnlyList<CharacterNumericCondition> NumericConditions { get; }
    public bool HasNumericConditions => NumericConditions != null &&
                                        NumericConditions.Count > 0;

    public static CharacterActionConditionData Empty => new(
        CharacterActionLinkage.None,
        CharacterConditionMatchMode.All,
        Array.Empty<CharacterNumericCondition>());

    public CharacterActionConditionData(
        CharacterActionLinkage linkage,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        Linkage = linkage;
        MatchMode = matchMode;
        NumericConditions = numericConditions ??
                            Array.Empty<CharacterNumericCondition>();
    }
}

public enum CharacterPassiveOrigin
{
    Role = 0,
    Character = 1,
    Archetype = 2,
}

public sealed class CharacterResolvedPassive
{
    public CharacterPassiveDefinition Definition { get; }
    public CharacterPassiveOrigin Origin { get; }
    public CharacterRoleSO Role { get; }
    public CharacterRolePassiveDefinition RolePassive { get; }
    public CharacterArchetypeSO Archetype { get; }
    public CharacterRolePassiveDefinition ArchetypePassive { get; }
    public bool IsRolePassive => Origin == CharacterPassiveOrigin.Role;
    public bool IsArchetypePassive =>
        Origin == CharacterPassiveOrigin.Archetype;
    public Sprite IconSprite =>
        IsRolePassive && RolePassive?.IconSprite != null
            ? RolePassive.IconSprite
            : IsArchetypePassive && ArchetypePassive?.IconSprite != null
                ? ArchetypePassive.IconSprite
            : Definition?.IconSprite != null
                ? Definition.IconSprite
                : IsArchetypePassive
                    ? Archetype?.IconSprite
                    : Role?.IconSprite;

    public CharacterResolvedPassive(
        CharacterPassiveDefinition definition,
        CharacterPassiveOrigin origin,
        CharacterRoleSO role = null,
        CharacterRolePassiveDefinition rolePassive = null,
        CharacterArchetypeSO archetype = null,
        CharacterRolePassiveDefinition archetypePassive = null)
    {
        Definition = definition;
        Origin = origin;
        Role = role;
        RolePassive = rolePassive;
        Archetype = archetype;
        ArchetypePassive = archetypePassive;
    }
}

public sealed class CharacterData
{
    private const int DungeonUpgradeTypeCount = 6;
    public const float DefaultHealthPerformanceCap = 100f;

    private readonly CharacterSO _definition;
    private readonly List<CharacterPassiveDefinition>
        _effectivePassiveDefinitions = new();
    private readonly List<CharacterResolvedPassive>
        _resolvedPassives = new();
    private CharacterProgressData _progress;
    private readonly int _baseMaximumHealth;
    private readonly float _baseAttackPower;
    private readonly int _baseJudgment;
    private readonly int _baseKnowledge;
    private readonly float _baseAttackCooldown;
    private readonly Dictionary<int, int> _dungeonUpgradeCounts = new();
    private readonly Dictionary<string, int> _dungeonUpgradeCountsById =
        new(StringComparer.Ordinal);
    private readonly CharacterModifierCollection _modifierCollection = new();
    private int _cumulativeMaximumHealthBonus;
    private float _cumulativeHealthPerformanceCapBonus;
    private float _cumulativeAttackPowerBonus;
    private float _cumulativeAttackCooldownBonus;
    private float _cumulativePassiveDamageBonus;
    private float _cumulativeAttackDamageBonus;
    private float _cumulativeSkillDamageBonus;
    private int _cumulativeSkillCostReduction;
    private float _dungeonAttackPowerBonus;
    private float _dungeonAttackCooldownBonus;
    private float _dungeonPassiveDamageBonus;
    private float _dungeonAttackDamageBonus;
    private float _dungeonSkillDamageBonus;
    private int _dungeonSkillCostReduction;

    public event Action StatsChanged;

    public CharacterSO Definition => _definition;
    public CharacterProgressData Progress => _progress;
    public string CharacterId { get; }
    public CharacterGrade Grade => _definition != null
        ? _definition.Grade
        : CharacterGrade.Grade0;
    public CharacterRoleSO Role => _definition?.Role;
    public CharacterArchetypeSO Archetype => _definition?.Archetype;
    public bool IsOwned => _progress?.IsOwned ?? false;
    public int Trust => _progress?.Trust ?? 0;
    public IReadOnlyList<CharacterCumulativeUpgradeProgress>
        CumulativeUpgrades => _progress?.CumulativeUpgrades ??
            Array.Empty<CharacterCumulativeUpgradeProgress>();
    public string NameLocalizationKey { get; }
    public string DescriptionLocalizationKey { get; }
    public string CharacterName { get; }
    public string CharacterDescription { get; }
    public Sprite StandingSprite { get; }
    public Sprite IconSprite { get; }
    public Sprite WaitingSdSprite { get; }
    public Sprite AttackSdSprite { get; }
    public Sprite DamagedSdSprite { get; }
    public Sprite DefeatSdSprite { get; }
    public Sprite SittingSdSprite { get; }
    public Sprite SkillSdSprite { get; }
    public Sprite PassiveSdSprite { get; }
    public Vector2 DungeonHudStandingFocus { get; }
    public float DungeonHudStandingZoom { get; }
    public float WorldSdScaleMultiplier { get; }
    public float WorldSdGroundOffset { get; }
    public float WorldSdHeadHeightNormalized { get; }
    public bool WorldSdFacesRight { get; }
    public int MaximumHealth => ResolveMaximumHealth();
    public float HealthPerformanceCap => Mathf.Max(
        0f,
        _modifierCollection.Resolve(
            DefaultHealthPerformanceCap +
            _cumulativeHealthPerformanceCapBonus,
            CharacterModifierStat.HealthPerformanceCap));
    public float AttackPower => Mathf.Max(
        0f,
        _modifierCollection.Resolve(
            _baseAttackPower +
            _cumulativeAttackPowerBonus +
            _dungeonAttackPowerBonus,
            CharacterModifierStat.AttackPower));
    public int Judgment => Mathf.Max(0, _baseJudgment);
    public int Knowledge => Mathf.Max(0, _baseKnowledge);
    public float AttackCooldown => TimePrecision.Normalize(
        Mathf.Max(
            TimePrecision.Step,
            _modifierCollection.Resolve(
                _baseAttackCooldown +
                _cumulativeAttackCooldownBonus +
                _dungeonAttackCooldownBonus,
                CharacterModifierStat.AttackCooldown)),
        TimePrecision.Step);
    public float AttackRecoveryDuration { get; }
    public float ActiveSkillRecoveryDuration { get; }
    public float AttackDamageFlatBonus => Mathf.Max(
        0f,
        _cumulativeAttackDamageBonus +
        _dungeonAttackDamageBonus);
    public float PassiveDamageAmountBonus => Mathf.Max(
        0f,
        _cumulativePassiveDamageBonus +
        _dungeonPassiveDamageBonus);
    public float SkillDamageFlatBonus => Mathf.Max(
        0f,
        _cumulativeSkillDamageBonus +
        _dungeonSkillDamageBonus);

    private int ResolveMaximumHealth()
    {
        float resolved = _modifierCollection.Resolve(
            _baseMaximumHealth + _cumulativeMaximumHealthBonus,
            CharacterModifierStat.MaximumHealth);
        if (float.IsNaN(resolved) || resolved <= 1f)
            return 1;
        if (float.IsInfinity(resolved) || resolved >= int.MaxValue)
            return int.MaxValue;
        return Mathf.Max(1, Mathf.RoundToInt(resolved));
    }

    public IReadOnlyList<CharacterAttackDefinition> AttackDefinitions =>
        _definition?.AttackDefinitions ?? Array.Empty<CharacterAttackDefinition>();
    public IReadOnlyList<CharacterPassiveDefinition> PassiveDefinitions =>
        _effectivePassiveDefinitions;
    public IReadOnlyList<CharacterResolvedPassive> ResolvedPassives =>
        _resolvedPassives;
    public IReadOnlyList<CharacterSkillDefinition> SkillDefinitions =>
        _definition?.SkillDefinitions ?? Array.Empty<CharacterSkillDefinition>();
    public CharacterSkillExecutionPolicy SkillExecutionPolicy =>
        _definition?.SkillExecutionPolicy ??
        CharacterSkillExecutionPolicy.FirstSuccessful;
    public IReadOnlyList<CharacterCumulativeUpgradeDefinition>
        CumulativeUpgradeDefinitions =>
            _definition?.CumulativeUpgradeDefinitions ??
            Array.Empty<CharacterCumulativeUpgradeDefinition>();
    public IReadOnlyList<CharacterDungeonUpgradeDefinition>
        DungeonUpgradeDefinitions => _definition?.DungeonUpgradeDefinitions ??
            Array.Empty<CharacterDungeonUpgradeDefinition>();

    public bool HasCustomAttackDefinitions => AttackDefinitions.Count > 0;
    public int ConfiguredPassiveDefinitionCount
    {
        get
        {
            int count = 0;
            foreach (CharacterPassiveDefinition definition in
                     PassiveDefinitions)
            {
                if (definition != null && !definition.IsEmptyPlaceholder)
                    count++;
            }

            return count;
        }
    }

    public bool HasCustomPassiveDefinitions =>
        ConfiguredPassiveDefinitionCount > 0;
    public bool HasCustomSkillDefinitions => SkillDefinitions.Count > 0;
    public Sprite PassiveAbilityIconSprite =>
        ResolvePassiveAbilityIconSprite();
    public Sprite ActiveAbilityIconSprite =>
        ResolveActiveAbilityIconSprite();
    public bool HasCustomCumulativeUpgrades =>
        CumulativeUpgradeDefinitions.Count > 0;
    public bool HasCustomDungeonUpgrades => DungeonUpgradeDefinitions.Count > 0;
    public bool UsesCustomFormat =>
        HasCustomAttackDefinitions ||
        HasCustomPassiveDefinitions ||
        HasCustomSkillDefinitions ||
        HasCustomCumulativeUpgrades ||
        HasCustomDungeonUpgrades;

    public CharacterActionConditionData GetActionConditionData(
        ICharacterConditionalActionDefinition definition)
    {
        if (definition == null)
            return CharacterActionConditionData.Empty;

        CharacterActionLinkage linkage = definition.HasLinkageSection
            ? definition.Linkage
            : CharacterActionLinkage.None;
        IReadOnlyList<CharacterNumericCondition> numericConditions =
            definition.HasConditionSection
                ? definition.NumericConditions
                : Array.Empty<CharacterNumericCondition>();
        return new CharacterActionConditionData(
            linkage,
            definition.HasConditionSection
                ? definition.ConditionMatchMode
                : CharacterConditionMatchMode.All,
            numericConditions);
    }

    public int ActiveSkillCost => HasCustomSkillDefinitions
        ? GetSkillCost(SkillDefinitions[0])
        : 0;
    public int AttackDamage => Mathf.Max(
        1,
        Mathf.RoundToInt(AttackPower));
    public int SkillAttackDamage => HasCustomSkillDefinitions
        ? CalculateSkillDamage(SkillDefinitions[0])
        : 0;

    private Sprite ResolvePassiveAbilityIconSprite()
    {
        CharacterPassiveDefinition definition =
            PassiveDefinitions.Count > 0
                ? PassiveDefinitions[0]
                : null;
        return GetPassiveAbilityIconSprite(definition);
    }

    private Sprite ResolveActiveAbilityIconSprite()
    {
        CharacterSkillDefinition definition = SkillDefinitions.Count > 0
            ? SkillDefinitions[0]
            : null;
        return GetActiveAbilityIconSprite(definition);
    }

    public Sprite GetPassiveAbilityIconSprite(
        CharacterPassiveDefinition definition)
    {
        if (definition != null)
        {
            foreach (CharacterResolvedPassive resolved in
                     ResolvedPassives)
            {
                if (ReferenceEquals(resolved?.Definition, definition) &&
                    resolved.IconSprite != null)
                {
                    return resolved.IconSprite;
                }
            }

            if (definition.IconSprite != null)
                return definition.IconSprite;
        }

        return PassiveSdSprite != null
            ? PassiveSdSprite
            : IconSprite;
    }

    public Sprite GetActiveAbilityIconSprite(
        CharacterSkillDefinition definition)
    {
        if (definition?.IconSprite != null)
            return definition.IconSprite;

        return SkillSdSprite != null
            ? SkillSdSprite
            : IconSprite;
    }

    public Sprite ResolveDefeatSdSprite()
    {
        if (DefeatSdSprite != null)
            return DefeatSdSprite;
        if (DamagedSdSprite != null)
            return DamagedSdSprite;
        return WaitingSdSprite != null
            ? WaitingSdSprite
            : IconSprite;
    }

    public Sprite ResolveSittingSdSprite()
    {
        return SittingSdSprite != null
            ? SittingSdSprite
            : WaitingSdSprite != null
                ? WaitingSdSprite
                : IconSprite;
    }

    public CharacterData(
        CharacterSO original,
        CharacterProgressData progress = null)
    {
        _definition = original;
        CharacterId = original != null ? original.CharacterId : string.Empty;
        _progress = progress ?? new CharacterProgressData(CharacterId);
        NameLocalizationKey = original != null
            ? original.NameLocalizationKey
            : string.Empty;
        DescriptionLocalizationKey = original != null
            ? original.DescriptionLocalizationKey
            : string.Empty;
        CharacterName = original != null ? original.CharacterName : string.Empty;
        CharacterDescription = original != null
            ? original.CharacterDescription
            : string.Empty;
        StandingSprite = original != null ? original.StandingSprite : null;
        IconSprite = original != null ? original.IconSprite : null;
        WaitingSdSprite = original != null ? original.WaitingSdSprite : null;
        AttackSdSprite = original != null ? original.AttackSdSprite : null;
        DamagedSdSprite = original != null ? original.DamagedSdSprite : null;
        DefeatSdSprite = original != null ? original.DefeatSdSprite : null;
        SittingSdSprite = original != null ? original.SittingSdSprite : null;
        SkillSdSprite = original != null ? original.SkillSdSprite : null;
        PassiveSdSprite = original != null ? original.PassiveSdSprite : null;
        DungeonHudStandingFocus = original != null
            ? original.DungeonHudStandingFocus
            : CharacterStandingFraming.DefaultFocus;
        DungeonHudStandingZoom = original != null
            ? original.DungeonHudStandingZoom
            : CharacterStandingFraming.DefaultZoom;
        WorldSdScaleMultiplier = original != null
            ? original.WorldSdScaleMultiplier
            : 1f;
        WorldSdGroundOffset = original != null
            ? original.WorldSdGroundOffset
            : 0f;
        WorldSdHeadHeightNormalized = original != null
            ? original.WorldSdHeadHeightNormalized
            : 1f;
        WorldSdFacesRight = original == null || original.WorldSdFacesRight;
        _baseMaximumHealth = original != null
            ? original.MaximumHealth
            : 1;
        _baseAttackPower = original != null ? original.AttackPower : 1f;
        _baseJudgment = original != null ? original.Judgment : 0;
        _baseKnowledge = original != null ? original.Knowledge : 0;
        _baseAttackCooldown = original != null
            ? original.AttackCooldown
            : 1f;
        AttackRecoveryDuration = original != null
            ? original.AttackRecoveryDuration
            : 0.5f;
        ActiveSkillRecoveryDuration = original != null
            ? original.ActiveSkillRecoveryDuration
            : 0f;
        BuildEffectivePassives();
        RecalculateCumulativeUpgradeModifiers();
    }

    private void BuildEffectivePassives()
    {
        _effectivePassiveDefinitions.Clear();
        _resolvedPassives.Clear();

        CharacterRoleSO role = Role;
        if (role != null)
        {
            foreach (CharacterRolePassiveDefinition rolePassive in
                     role.PassiveDefinitions)
            {
                CharacterPassiveDefinition definition =
                    rolePassive?.Ability;
                if (definition == null ||
                    definition.IsEmptyPlaceholder)
                {
                    continue;
                }

                _effectivePassiveDefinitions.Add(definition);
                _resolvedPassives.Add(new CharacterResolvedPassive(
                    definition,
                    CharacterPassiveOrigin.Role,
                    role,
                    rolePassive));
            }
        }

        CharacterArchetypeSO archetype = Archetype;
        if (archetype != null)
        {
            foreach (CharacterRolePassiveDefinition archetypePassive in
                     archetype.PassiveDefinitions)
            {
                CharacterPassiveDefinition definition =
                    archetypePassive?.Ability;
                if (definition == null ||
                    definition.IsEmptyPlaceholder)
                {
                    continue;
                }

                _effectivePassiveDefinitions.Add(definition);
                _resolvedPassives.Add(new CharacterResolvedPassive(
                    definition,
                    CharacterPassiveOrigin.Archetype,
                    archetype: archetype,
                    archetypePassive: archetypePassive));
            }
        }

        IReadOnlyList<CharacterPassiveDefinition> personalPassives =
            _definition?.PassiveDefinitions ??
            Array.Empty<CharacterPassiveDefinition>();
        foreach (CharacterPassiveDefinition definition in
                 personalPassives)
        {
            if (definition == null)
                continue;

            _effectivePassiveDefinitions.Add(definition);
            if (!definition.IsEmptyPlaceholder)
            {
                _resolvedPassives.Add(new CharacterResolvedPassive(
                    definition,
                    CharacterPassiveOrigin.Character));
            }
        }
    }

    public void SetOwned(bool value)
    {
        _progress?.SetOwned(value);
    }

    public int GetCumulativeUpgradeLevel(string upgradeId)
    {
        int savedLevel =
            _progress?.GetCumulativeUpgradeLevel(upgradeId) ?? 0;
        CharacterCumulativeUpgradeDefinition definition =
            FindCumulativeUpgradeDefinition(upgradeId);
        return definition != null
            ? definition.ClampLevel(savedLevel)
            : savedLevel;
    }

    public void SetCumulativeUpgradeLevel(string upgradeId, int level)
    {
        if (_progress == null || string.IsNullOrWhiteSpace(upgradeId))
            return;

        CharacterCumulativeUpgradeDefinition definition =
            FindCumulativeUpgradeDefinition(upgradeId);
        int nextLevel = definition != null
            ? definition.ClampLevel(level)
            : Mathf.Max(0, level);
        if (_progress.GetCumulativeUpgradeLevel(upgradeId) == nextLevel)
            return;

        _progress.SetCumulativeUpgradeLevel(upgradeId, nextLevel);
        RecalculateCumulativeUpgradeModifiers();
        StatsChanged?.Invoke();
    }

    public int AddCumulativeUpgradeLevel(string upgradeId, int amount = 1)
    {
        if (_progress == null || string.IsNullOrWhiteSpace(upgradeId))
            return 0;

        long nextLevel =
            (long)GetCumulativeUpgradeLevel(upgradeId) + amount;
        SetCumulativeUpgradeLevel(
            upgradeId,
            (int)Math.Min(
                int.MaxValue,
                Math.Max(0L, nextLevel)));
        return GetCumulativeUpgradeLevel(upgradeId);
    }

    private CharacterCumulativeUpgradeDefinition
        FindCumulativeUpgradeDefinition(string upgradeId)
    {
        return _definition?.GetCumulativeUpgradeDefinition(upgradeId);
    }

    private void RecalculateCumulativeUpgradeModifiers()
    {
        _modifierCollection.ClearScope(
            CharacterModifierLifetimeScope.Permanent);
        double maximumHealth = 0d;
        double healthPerformanceCap = 0d;
        double attackPower = 0d;
        double attackCooldown = 0d;
        double passiveDamage = 0d;
        double attackDamage = 0d;
        double skillDamage = 0d;
        double skillCostReduction = 0d;
        HashSet<string> appliedUpgradeIds =
            new(StringComparer.Ordinal);

        foreach (CharacterCumulativeUpgradeDefinition definition in
                 CumulativeUpgradeDefinitions)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.UpgradeId) ||
                !appliedUpgradeIds.Add(definition.UpgradeId))
            {
                continue;
            }

            int level = definition.ClampLevel(
                _progress?.GetCumulativeUpgradeLevel(
                    definition.UpgradeId) ?? 0);
            if (level <= 0)
                continue;

            if (definition.ModifierModules.Count > 0)
            {
                _modifierCollection.ReplaceSource(
                    $"cumulative:{definition.UpgradeId}",
                    definition.ModifierModules,
                    level,
                    CharacterModifierLifetimeScope.Permanent);
            }

            foreach (CharacterCumulativeUpgradeModifier modifier in
                     definition.Modifiers)
            {
                if (modifier == null ||
                    float.IsNaN(modifier.ValuePerLevel) ||
                    float.IsInfinity(modifier.ValuePerLevel) ||
                    modifier.ValuePerLevel == 0f)
                {
                    continue;
                }

                double value = modifier.ValuePerLevel * (double)level;
                switch (modifier.Type)
                {
                    case CharacterCumulativeUpgradeModifierType.AttackPower:
                        attackPower += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType.MaximumHealth:
                        maximumHealth += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType
                        .HealthPerformanceCap:
                        healthPerformanceCap += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType.AttackCooldown:
                        attackCooldown += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType.PassiveDamage:
                        passiveDamage += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType.AttackDamage:
                        attackDamage += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType.SkillDamage:
                        skillDamage += value;
                        break;
                    case CharacterCumulativeUpgradeModifierType
                        .SkillCostReduction:
                        skillCostReduction += value;
                        break;
                }
            }
        }

        _cumulativeMaximumHealthBonus =
            RoundModifierToInt(maximumHealth);
        _cumulativeHealthPerformanceCapBonus =
            ClampModifierToFloat(healthPerformanceCap);
        _cumulativeAttackPowerBonus = ClampModifierToFloat(attackPower);
        _cumulativeAttackCooldownBonus =
            ClampModifierToFloat(attackCooldown);
        _cumulativePassiveDamageBonus =
            ClampModifierToFloat(passiveDamage);
        _cumulativeAttackDamageBonus =
            ClampModifierToFloat(attackDamage);
        _cumulativeSkillDamageBonus =
            ClampModifierToFloat(skillDamage);
        _cumulativeSkillCostReduction =
            Mathf.Max(0, RoundModifierToInt(skillCostReduction));
    }

    internal void RefreshCumulativeUpgradeState()
    {
        RecalculateCumulativeUpgradeModifiers();
        StatsChanged?.Invoke();
    }

    internal void RebindProgress(CharacterProgressData progress)
    {
        _progress = progress ?? new CharacterProgressData(CharacterId);
        RefreshCumulativeUpgradeState();
    }

    private static int RoundModifierToInt(double value)
    {
        if (double.IsNaN(value))
            return 0;
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= int.MinValue)
            return int.MinValue;

        return Mathf.RoundToInt((float)value);
    }

    private static float ClampModifierToFloat(double value)
    {
        if (double.IsNaN(value))
            return 0f;

        return (float)Math.Max(
            -int.MaxValue,
            Math.Min(int.MaxValue, value));
    }

    public int CalculateAttackDamage(
        CharacterAttackDefinition definition,
        float attackPowerMultiplier = 1f)
    {
        if (definition == null)
            return 0;

        return CalculateDamage(
            definition.DamageScaling,
            definition.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Attack,
                attackPowerMultiplier),
            definition.ActionId);
    }

    public int CalculateAttackDamage(
        CharacterEffectDefinition effect,
        float attackPowerMultiplier = 1f)
    {
        if (effect == null || effect.Type != CharacterEffectType.Damage)
            return 0;

        return CalculateDamage(
            effect.DamageScaling,
            effect.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Attack,
                attackPowerMultiplier),
            null,
            effect.EffectId);
    }

    public int CalculatePassiveDamage(
        CharacterPassiveDefinition definition,
        float attackPowerMultiplier = 1f)
    {
        if (definition == null)
            return 0;

        return CalculateDamage(
            definition.DamageScaling,
            definition.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Passive,
                attackPowerMultiplier),
            definition.ActionId);
    }

    public int CalculatePassiveDamage(
        CharacterEffectDefinition effect,
        float attackPowerMultiplier = 1f)
    {
        if (effect == null || effect.Type != CharacterEffectType.Damage)
            return 0;

        return CalculateDamage(
            effect.DamageScaling,
            effect.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Passive,
                attackPowerMultiplier),
            null,
            effect.EffectId);
    }

    public int CalculateSkillDamage(
        CharacterSkillDefinition definition,
        float attackPowerMultiplier = 1f)
    {
        if (definition == null)
            return 0;

        return CalculateDamage(
            definition.DamageScaling,
            definition.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Skill,
                attackPowerMultiplier),
            definition.ActionId);
    }

    public int CalculateSkillDamage(
        CharacterEffectDefinition effect,
        float attackPowerMultiplier = 1f)
    {
        if (effect == null || effect.Type != CharacterEffectType.Damage)
            return 0;

        return CalculateDamage(
            effect.DamageScaling,
            effect.DamageAmountMode,
            CreatePreviewEffectContext(
                CharacterActionKind.Skill,
                attackPowerMultiplier),
            null,
            effect.EffectId);
    }

    public int CalculateEffectDamage(
        CharacterEffectDefinition effect,
        EffectContext context,
        string actionId = null)
    {
        if (effect == null || effect.Type != CharacterEffectType.Damage)
            return 0;

        return CalculateDamage(
            effect.DamageScaling,
            effect.DamageAmountMode,
            context,
            actionId,
            effect.EffectId);
    }

    public int CalculateEffectAmount(
        CharacterEffectDefinition effect,
        EffectContext context,
        string actionId = null)
    {
        if (effect == null)
            return 0;

        float amount = effect.AmountScaling.Evaluate(context);
        amount = ResolveModifier(
            amount,
            CharacterModifierStat.EffectAmount,
            context.ActionKind,
            actionId,
            effect.EffectId);
        return RoundEffectValue(amount);
    }

    private EffectContext CreatePreviewEffectContext(
        CharacterActionKind actionKind,
        float attackPowerMultiplier)
    {
        float sourceAttackPower =
            AttackPower * Mathf.Max(0f, attackPowerMultiplier);
        return EffectContext.ForPreview(
            actionKind,
            sourceAttackPower);
    }

    private int CalculateDamage(
        ScalingValue scaling,
        CharacterDamageAmountMode legacyAmountMode,
        EffectContext context,
        string actionId = null,
        string effectId = null)
    {
        scaling += context.ActionKind switch
        {
            CharacterActionKind.Attack =>
                ScalingValue.Fixed(AttackDamageFlatBonus),
            CharacterActionKind.Passive =>
                legacyAmountMode == CharacterDamageAmountMode.Ratio
                    ? ScalingValue.SourceAttackPower(
                        PassiveDamageAmountBonus)
                    : ScalingValue.Fixed(PassiveDamageAmountBonus),
            CharacterActionKind.Skill =>
                ScalingValue.Fixed(SkillDamageFlatBonus),
            _ => default
        };

        float damage = scaling.Evaluate(context);
        damage = ResolveModifier(
            damage,
            CharacterModifierStat.Damage,
            context.ActionKind,
            actionId,
            effectId);
        return RoundEffectValue(damage);
    }

    private static int RoundEffectValue(float amount)
    {
        if (float.IsNaN(amount) ||
            float.IsInfinity(amount) ||
            amount <= 0f)
        {
            return 0;
        }
        if (amount >= int.MaxValue)
            return int.MaxValue;

        return Mathf.Max(0, Mathf.RoundToInt(amount));
    }

    public int GetSkillCost(CharacterSkillDefinition definition)
    {
        if (definition == null || !definition.HasSection(
                CharacterSkillSectionType.Cost))
        {
            return 0;
        }

        int totalReduction = (int)Math.Min(
            int.MaxValue,
            (long)_cumulativeSkillCostReduction +
            _dungeonSkillCostReduction);
        float cost = ResolveModifier(
            definition.Cost - totalReduction,
            CharacterModifierStat.SkillCost,
            CharacterActionKind.Skill,
            definition.ActionId);
        return Mathf.Max(1, Mathf.RoundToInt(cost));
    }

    public float ResolveModifier(
        float baseValue,
        CharacterModifierStat stat,
        CharacterActionKind actionKind = default,
        string actionId = null,
        string effectId = null)
    {
        return _modifierCollection.Resolve(
            baseValue,
            stat,
            actionKind,
            actionId,
            effectId);
    }

    public float ResolveStatusDuration(
        float baseDuration,
        CharacterActionKind actionKind,
        string actionId = null,
        string effectId = null)
    {
        if (float.IsPositiveInfinity(baseDuration))
            return baseDuration;

        return TimePrecision.Normalize(
            Mathf.Max(
                TimePrecision.Step,
                ResolveModifier(
                    baseDuration,
                    CharacterModifierStat.StatusDuration,
                    actionKind,
                    actionId,
                    effectId)),
            TimePrecision.Step);
    }

    public float ResolveStatusStacks(
        float baseStacks,
        CharacterActionKind actionKind,
        string actionId = null,
        string effectId = null)
    {
        return Mathf.Max(
            0.1f,
            ResolveModifier(
                baseStacks,
                CharacterModifierStat.StatusStacks,
                actionKind,
                actionId,
                effectId));
    }

    public bool ReplaceModifierSource(
        string sourceId,
        IReadOnlyList<CharacterModifierModule> modules,
        int stackCount,
        CharacterModifierLifetimeScope lifetimeScope,
        float duration = float.PositiveInfinity)
    {
        bool changed = _modifierCollection.ReplaceSource(
            sourceId,
            modules,
            stackCount,
            lifetimeScope,
            duration);
        if (changed)
            StatsChanged?.Invoke();
        return changed;
    }

    public bool RemoveModifierSource(string sourceId)
    {
        bool changed = _modifierCollection.RemoveSource(sourceId);
        if (changed)
            StatsChanged?.Invoke();
        return changed;
    }

    public bool ClearModifierScope(CharacterModifierLifetimeScope scope)
    {
        bool changed = _modifierCollection.ClearScope(scope);
        if (changed)
            StatsChanged?.Invoke();
        return changed;
    }

    public bool TickModifiers(float deltaTime)
    {
        bool changed = _modifierCollection.Tick(deltaTime);
        if (changed)
            StatsChanged?.Invoke();
        return changed;
    }

    public bool CanApplyDungeonUpgrade(
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        CharacterDungeonUpgradeEntry entry = definitionIndex >= 0 &&
            definitionIndex < DungeonUpgradeDefinitions.Count
                ? DungeonUpgradeDefinitions[definitionIndex]
                    ?.GetEntry(upgradeType)
                : null;
        return entry != null && CanApplyDungeonUpgrade(
            definitionIndex,
            entry.UpgradeId);
    }

    public bool CanApplyDungeonUpgrade(
        int definitionIndex,
        string upgradeId)
    {
        if (definitionIndex < 0 ||
            definitionIndex >= DungeonUpgradeDefinitions.Count)
        {
            return false;
        }

        CharacterDungeonUpgradeEntry entry =
            DungeonUpgradeDefinitions[definitionIndex]?.GetEntry(upgradeId);
        if (entry == null || entry.Probability <= 0f)
            return false;

        int appliedCount = GetDungeonUpgradeAppliedCount(
            definitionIndex,
            entry.UpgradeId);
        if (!entry.HasUnlimitedLimit && appliedCount >= entry.Limit)
            return false;

        if (entry.HasModifierModules)
            return true;

        return entry.Type switch
        {
            CharacterDungeonUpgradeType.Speed =>
                AttackCooldown > TimePrecision.Step,
            CharacterDungeonUpgradeType.SkillCostReduction =>
                ActiveSkillCost > 1,
            _ => true,
        };
    }

    public bool ApplyDungeonUpgrade(
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        CharacterDungeonUpgradeEntry entry = definitionIndex >= 0 &&
            definitionIndex < DungeonUpgradeDefinitions.Count
                ? DungeonUpgradeDefinitions[definitionIndex]
                    ?.GetEntry(upgradeType)
                : null;
        return entry != null && ApplyDungeonUpgrade(
            definitionIndex,
            entry.UpgradeId);
    }

    public bool ApplyDungeonUpgrade(
        int definitionIndex,
        string upgradeId)
    {
        if (!CanApplyDungeonUpgrade(definitionIndex, upgradeId))
            return false;

        CharacterDungeonUpgradeEntry entry =
            DungeonUpgradeDefinitions[definitionIndex].GetEntry(upgradeId);
        int nextCount = GetDungeonUpgradeAppliedCount(
            definitionIndex,
            entry.UpgradeId) + 1;
        if (entry.HasModifierModules)
        {
            if (!_modifierCollection.ReplaceSource(
                    $"dungeon:{entry.UpgradeId}",
                    entry.ModifierModules,
                    nextCount,
                    CharacterModifierLifetimeScope.Dungeon))
            {
                return false;
            }
        }
        else
        {
            float value = entry.FixedValue;
            switch (entry.Type)
            {
                case CharacterDungeonUpgradeType.AttackPower:
                    _dungeonAttackPowerBonus += value;
                    break;
                case CharacterDungeonUpgradeType.Speed:
                    _dungeonAttackCooldownBonus += value;
                    break;
                case CharacterDungeonUpgradeType.PassiveDamage:
                    _dungeonPassiveDamageBonus += value;
                    break;
                case CharacterDungeonUpgradeType.AttackDamage:
                    _dungeonAttackDamageBonus += value;
                    break;
                case CharacterDungeonUpgradeType.SkillDamage:
                    _dungeonSkillDamageBonus += value;
                    break;
                case CharacterDungeonUpgradeType.SkillCostReduction:
                    _dungeonSkillCostReduction = Mathf.Max(
                        0,
                        _dungeonSkillCostReduction +
                        Mathf.RoundToInt(-value));
                    break;
                default:
                    return false;
            }
        }

        string key = GetDungeonUpgradeKey(
            definitionIndex,
            entry.UpgradeId);
        _dungeonUpgradeCountsById[key] = nextCount;
        StatsChanged?.Invoke();
        return true;
    }

    public int GetDungeonUpgradeAppliedCount(
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        CharacterDungeonUpgradeEntry entry = definitionIndex >= 0 &&
            definitionIndex < DungeonUpgradeDefinitions.Count
                ? DungeonUpgradeDefinitions[definitionIndex]
                    ?.GetEntry(upgradeType)
                : null;
        return entry != null
            ? GetDungeonUpgradeAppliedCount(
                definitionIndex,
                entry.UpgradeId)
            : 0;
    }

    public int GetDungeonUpgradeAppliedCount(
        int definitionIndex,
        string upgradeId)
    {
        string key = GetDungeonUpgradeKey(definitionIndex, upgradeId);
        return _dungeonUpgradeCountsById.TryGetValue(key, out int count)
            ? count
            : 0;
    }

    public bool TryRollDungeonUpgrade(
        int definitionIndex,
        System.Random random,
        out CharacterDungeonUpgradeType upgradeType)
    {
        upgradeType = default;
        if (!TryRollDungeonUpgrade(
                definitionIndex,
                random,
                out string upgradeId))
        {
            return false;
        }

        CharacterDungeonUpgradeEntry entry =
            DungeonUpgradeDefinitions[definitionIndex]?.GetEntry(upgradeId);
        if (entry == null)
            return false;

        upgradeType = entry.Type;
        return true;
    }

    public bool TryRollDungeonUpgrade(
        int definitionIndex,
        System.Random random,
        out string upgradeId)
    {
        upgradeId = string.Empty;
        if (random == null || definitionIndex < 0 ||
            definitionIndex >= DungeonUpgradeDefinitions.Count)
        {
            return false;
        }

        CharacterDungeonUpgradeDefinition definition =
            DungeonUpgradeDefinitions[definitionIndex];
        if (definition == null ||
            (definition.UsesLegacyProbabilityMode &&
             !definition.HasValidProbabilityTotal))
            return false;

        float totalWeight = 0f;
        foreach (CharacterDungeonUpgradeEntry entry in definition.Entries)
        {
            if (entry != null &&
                CanApplyDungeonUpgrade(definitionIndex, entry.UpgradeId))
            {
                totalWeight += entry.Probability;
            }
        }

        if (totalWeight <= 0f)
            return false;

        double roll = random.NextDouble() * totalWeight;
        foreach (CharacterDungeonUpgradeEntry entry in definition.Entries)
        {
            if (entry == null ||
                !CanApplyDungeonUpgrade(definitionIndex, entry.UpgradeId))
            {
                continue;
            }

            roll -= entry.Probability;
            if (roll > 0d)
                continue;

            upgradeId = entry.UpgradeId;
            return true;
        }

        return false;
    }

    public string GetDungeonUpgradeLabel(
        CharacterDungeonUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            CharacterDungeonUpgradeType.AttackPower =>
                $"ATTACK POWER {AttackPower:0.##} > " +
                $"{AttackPower + 0.5f:0.##}",
            CharacterDungeonUpgradeType.Speed =>
                $"COOLDOWN {AttackCooldown:0.##}s > " +
                $"{Mathf.Max(TimePrecision.Step, AttackCooldown - 0.1f):0.##}s",
            CharacterDungeonUpgradeType.PassiveDamage =>
                $"PASSIVE AMOUNT +0.5 " +
                $"(TOTAL +{PassiveDamageAmountBonus + 0.5f:0.##})",
            CharacterDungeonUpgradeType.AttackDamage =>
                $"ATTACK DAMAGE +0.5 " +
                $"(TOTAL +{AttackDamageFlatBonus + 0.5f:0.##})",
            CharacterDungeonUpgradeType.SkillDamage =>
                $"SKILL DAMAGE +1 " +
                $"(TOTAL +{SkillDamageFlatBonus + 1f:0.##})",
            CharacterDungeonUpgradeType.SkillCostReduction =>
                $"SKILL COST {ActiveSkillCost} > " +
                $"{Mathf.Max(1, ActiveSkillCost - 1)}",
            _ => upgradeType.ToString(),
        };
    }

    private static int GetDungeonUpgradeKey(
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        return definitionIndex * DungeonUpgradeTypeCount + (int)upgradeType;
    }

    private static string GetDungeonUpgradeKey(
        int definitionIndex,
        string upgradeId)
    {
        _ = definitionIndex;
        return (upgradeId ?? string.Empty).Trim();
    }
}
