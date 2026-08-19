#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the public 100-card catalog described by
/// PS260714_CARD_CATALOG_100.docx.  New assets are built and validated in a
/// staging folder before the live Cards folder is touched.
/// </summary>
public static class BattleCardCatalog100Generator
{
    private const string MenuPath =
        "Tools/PS260714/Cards/Rebuild Public 100-Card Catalog";
    private const string CardsFolder =
        "Assets/06_Runtime/Resources/Cards";
    private const string StagingFolder =
        "Assets/06_Runtime/Resources/Cards.__Catalog100Staging";
    private const string BackupFolder =
        "Assets/06_Runtime/Resources/Cards.__Catalog100Backup";

    private static readonly HashSet<int> StartingCardNumbers = new()
    {
        1, 4, 6, 31, 35, 36, 46, 71, 72, 74,
    };

    // number | Korean name | cost | rarity | recycle | Korean description |
    // stable English asset-name suffix.  The dagger editorial marker from the
    // source document is deliberately not part of the stored card name.
    private static readonly string[] RawCards =
    {
        "1|정밀 사격|1|Common|Discard|적 1명에게 피해 18|PrecisionShot",
        "2|이중 사격|1|Common|Exhaust|적 1명에게 피해 9를 2회|DoubleShot",
        "3|관통 사격|2|Common|Discard|지정 적에게 16, 뒤의 적에게 8 피해|PiercingShot",
        "4|산탄 돌파|2|Common|Exhaust|지정 지점 반지름 1.5에 피해 15|ShotgunBreakthrough",
        "5|폭발 탄두|2|Rare|Exhaust|반지름 2에 피해 12, Opening 1스택|ExplosiveWarhead",
        "6|연쇄 사격|2|Common|Exhaust|무작위 적 3명에게 각각 피해 10|ChainFire",
        "7|사냥 표식|1|Common|Exhaust|대상에게 Focus 8초, 다음 공격 피해 +12|HuntersMark",
        "8|약점 관측|1|Rare|Exhaust|대상에게 Opening 1스택, 카드 1장 드로우|WeaknessObservation",
        "9|처형선|3|Rare|Exhaust|적 1명에게 피해 25, 체력 35% 이하이면 추가 20|ExecutionLine",
        "10|중력탄|2|Rare|Discard|피해 10, Stun 1.5초|GravityRound",
        "11|화염탄|1|Common|Exhaust|피해 5, Fire 2스택|IncendiaryRound",
        "12|출혈탄|1|Common|Exhaust|피해 5, Blood 2스택|BleedingRound",
        "13|독성탄|1|Common|Exhaust|피해 5, Poison 2스택|ToxicRound",
        "14|방어 관통|2|Rare|Discard|적 1명에게 피해 20, 보호 효과 무시|ArmorPiercing",
        "15|최후의 일격|4|Epic|Exhaust|체력 40% 이하인 모든 적에게 피해 30. 처치 시 2장 드로우|FinalBlow",
        "16|원형 폭격|3|Rare|Exhaust|지정 지점 반지름 2에 피해 18|CircularBombardment",
        "17|낙뢰 지점|2|Rare|Exhaust|2초 후 지정 지점 반지름 1.5에 피해 22|LightningPoint",
        "18|지뢰 설치|1|Common|Discard|지정 위치에 설치. 최초 진입 적에게 피해 25와 Stun 1초|MinePlacement",
        "19|화염 확산|2|Rare|Exhaust|Fire 상태의 모든 적에게 피해 12, Fire 1스택 제거|FireSpread",
        "20|출혈 폭발|3|Rare|Exhaust|Blood 상태의 모든 적에게 피해 18, Blood 1스택 제거|BloodExplosion",
        "21|독성 전염|2|Rare|Exhaust|대상에게 Poison 2스택, 주변 적 2명에게 1스택 전염|ToxicContagion",
        "22|개방 파열|2|Common|Exhaust|대상에게 Opening 1스택, 주변 적에게 피해 10|OpeningRupture",
        "23|결박 고리|2|Common|Discard|무작위 적 최대 3명에게 Stun 1초|BindingRing",
        "24|방어선 교란|3|Rare|Exhaust|방어선에 도달한 적을 1.5초 기절|DefenseLineDisruption",
        "25|시간 정지|4|Epic|Exhaust|모든 적에게 Stun 2초|TimeStop",
        "26|유인 신호|1|Common|Discard|대상에게 Focus 8초. 자동 공격 우선 대상 지정|DecoySignal",
        "27|역류 충격|2|Rare|Exhaust|최근 5초 안에 코어를 공격한 적에게 피해 25|BackflowShock",
        "28|연막 교란|1|Common|Exhaust|지정 지점 주변 적에게 Stun 0.75초, Focus 제거|SmokeDisruption",
        "29|추적 폭발|2|Common|Discard|Focus 상태인 모든 적에게 피해 20|TrackingExplosion",
        "30|전장 소각|5|Epic|Exhaust|모든 적에게 피해 20과 Fire 2스택|BattlefieldIncineration",
        "31|응급 수리|1|Common|Discard|코어 보호막 25 회복|EmergencyRepair",
        "32|정밀 수리|2|Common|Discard|코어 보호막 45 회복. 코어가 30% 이하이면 추가 20|PrecisionRepair",
        "33|보호막 재기동|3|Rare|Exhaust|코어 보호막 80 회복|ShieldRestart",
        "34|방벽 재배치|2|Rare|Discard|대원 1명에게 보호막 35. 코어 체력이 30% 이하면 추가 15|BarrierRedeployment",
        "35|대원 보호막|1|Common|Discard|대원 1명에게 보호막 25|OperatorShield",
        "36|긴급 회복|2|Common|Exhaust|대원 1명의 체력 30 회복|EmergencyRecovery",
        "37|광역 응급 처치|3|Rare|Exhaust|모든 대원의 체력 18 회복|FieldFirstAid",
        "38|소생 프로토콜|4|Epic|Exhaust|사망한 대원 1명을 최대 체력 35%로 부활. 없으면 체력 35 회복|RevivalProtocol",
        "39|희생의 수리|2|Rare|Exhaust|대원 1명의 체력 15를 소모하고 코어 보호막 80 회복|SacrificialRepair",
        "40|방어 분담|1|Common|Discard|다음 코어 피해의 30%를 지정 대원이 대신 받음|DamageSharing",
        "41|최후 방어선|3|Rare|Exhaust|코어 보호막 50 회복, 방어선의 적을 1초 기절|LastDefenseLine",
        "42|긴급 방호|1|Common|Discard|대원 1명에게 보호막 20, 해로운 상태 1개 제거|EmergencyRetreat",
        "43|불굴|2|Rare|Exhaust|체력 30% 이하 대원에게 체력 20과 Power 1스택|Indomitable",
        "44|회복 순환|2|Common|Discard|대원 1명 체력 20 회복, 카드 1장 드로우|RecoveryCycle",
        "45|코어 봉인|4|Epic|Exhaust|3초 동안 코어가 피해를 받지 않음|CoreSeal",
        "46|빠른 전개|0|Common|Exhaust|카드 1장 드로우|RapidDeployment",
        "47|정찰 드로우|1|Common|Discard|카드 2장 드로우 후 손패 1장 버림|ReconDraw",
        "48|전술 재정렬|1|Common|Discard|손패 1장을 버리고 카드 2장 드로우|TacticalReorder",
        "49|카드 회수|2|Rare|Exhaust|버림 더미의 카드 1장을 손패로 가져옴|CardRecovery",
        "50|재편성|1|Common|Discard|버림 더미를 뽑기 더미에 섞고 2장 드로우|Regroup",
        "51|손패 압축|1|Rare|Exhaust|손패 최대 2장을 소멸하고 그 수보다 1장 더 드로우|HandCompression",
        "52|과감한 버리기|0|Rare|Exhaust|현재 손패를 모두 버리고 4장 드로우|BoldDiscard",
        "53|비축 에너지|2|Rare|Exhaust|에너지 1 회복. 손패가 2장 이하이면 1장 드로우|StoredEnergy",
        "54|비용 절감|2|Rare|Exhaust|다음 카드 3장의 비용 -1|CostReduction",
        "55|전투 지휘|3|Rare|Discard|카드 2장 드로우. 다음 카드 비용 0|BattleCommand",
        "56|완벽한 순환|3|Epic|Exhaust|뽑기·버림 더미를 합쳐 섞고 5장 드로우|PerfectCycle",
        "57|손패 보호|1|Common|Discard|다음 자동 패 교체를 무시하고 카드 1장 드로우|HandProtection",
        "58|순간 판단|1|Rare|Exhaust|다음 카드 비용 -1, 카드 1장 드로우|SnapDecision",
        "59|소멸의 대가|1|Rare|Exhaust|손패 1장을 소멸하고 에너지 1 회복|PriceOfExhaustion",
        "60|무리한 주문|4|Epic|Exhaust|현재 손패를 버리고 에너지 2 회복, 카드 2장 드로우|RecklessSpell",
        "61|진격 명령|0|Common|Discard|대원 1명의 다음 기본 공격 피해 +10|AdvanceCommand",
        "62|방어 명령|0|Common|Discard|대원 1명에게 보호막 15|RetreatCommand",
        "63|즉응 명령|1|Common|Discard|대원 1명의 기본 공격 대기시간을 즉시 완료|EmergencyRelocation",
        "64|교차 전개|2|Rare|Exhaust|대원 2명의 기본 공격 대기시간을 즉시 완료하고 다음 기본 공격 피해 +10|FormationSwap",
        "65|전원 집결|1|Common|Discard|카드 1장 드로우. 모든 대원의 다음 기본 공격 피해 +4|CentralRally",
        "66|일제 공세|2|Rare|Exhaust|모든 대원의 다음 기본 공격 피해 +12|OuterEncirclement",
        "67|측면 협공|1|Rare|Exhaust|대원 1명의 다음 기본 공격이 50% 위력으로 한 번 더 판정|FlankingManeuver",
        "68|봉쇄 지점|2|Rare|Discard|지정 지점에 5초간 영역 생성. 진입한 적에게 피해 15와 Stun 1초|BlockadePoint",
        "69|유도 장벽|3|Rare|Exhaust|지정 지점으로 적을 끌어당기고 Stun 1초|GuidingBarrier",
        "70|완전 포위|4|Epic|Exhaust|모든 적에게 Opening 1스택. 모든 대원의 다음 기본 공격 피해 +10, 기본 공격 대기시간 즉시 완료|CircularEncirclement",
        "71|공격 강화|1|Common|Discard|대원 1명에게 Power 1스택, 10초|PowerBoost",
        "72|신속 강화|1|Common|Discard|대원 1명에게 Speed 1스택, 10초|SpeedBoost",
        "73|집중 사격|2|Common|Exhaust|지정 대원은 8초 동안 지정 적을 우선 공격|FocusedFire",
        "74|약점 노출|1|Common|Exhaust|적 1명에게 Opening 1스택, 10초|ExposeWeakness",
        "75|불꽃의 맹세|2|Rare|Exhaust|다음 3회 공격이 Fire 1스택 부여|VowOfFlame",
        "76|출혈의 맹세|2|Rare|Exhaust|다음 3회 공격이 Blood 1스택 부여|VowOfBlood",
        "77|독성 탄두|2|Rare|Exhaust|다음 3회 공격이 Poison 1스택 부여|ToxicWarhead",
        "78|이중 전개|2|Rare|Discard|대원 1명에게 DualSide 8초|DualDeployment",
        "79|연속 공격|3|Epic|Exhaust|다음 3회 기본 공격이 50% 위력으로 2회 판정|RepeatedAttack",
        "80|파워 전환|1|Common|Discard|Speed 1스택을 제거하고 Power 2스택|PowerConversion",
        "81|속도 전환|1|Common|Discard|Power 1스택을 제거하고 Speed 2스택|SpeedConversion",
        "82|정화 명령|1|Common|Discard|대원의 해로운 상태 최대 2개 제거|PurgeCommand",
        "83|상태 역전|2|Rare|Exhaust|적의 Fire/Blood/Poison을 제거하고 스택당 피해 10|StatusReversal",
        "84|개방의 순간|2|Rare|Exhaust|Opening 상태 적의 지속시간 +5초, 피해 15|OpeningMoment",
        "85|기절 확산|3|Rare|Exhaust|대상 Stun 1.5초, 주변 적 Stun 0.75초|StunSpread",
        "86|전위의 방패|1|Common|Discard|전위 대원에게 보호막 30. 대상 체력이 50% 이하면 추가 15|VanguardShield",
        "87|전위 돌파|2|Rare|Exhaust|전위 대원에게 Power 1스택. 다음 공격이 Stun 1초|VanguardBreakthrough",
        "88|사수 관측|1|Common|Discard|사수 대원의 다음 공격 피해 +20, 대상에게 Opening 1스택|ShooterObservation",
        "89|사수 일제|3|Rare|Exhaust|모든 사수 대원에게 Speed 1스택, 8초, 1장 드로우|ShooterVolley",
        "90|술사의 공명|2|Rare|Exhaust|술사의 다음 상태 부여가 추가로 1스택 적용되고 1장 드로우|MageResonance",
        "91|술식 과부하|3|Epic|Exhaust|술사 대원에게 Power 2스택, 8초. 체력 10 소모|SpellOverload",
        "92|척후 급습|1|Common|Exhaust|척후 대원의 Opening 대상 공격 피해 +30, 1장 드로우|ScoutAmbush",
        "93|척후 연계|2|Rare|Discard|척후 대원에게 Speed 1스택, 8초. 해당 시간 내 처치 시 에너지 1 회복|ScoutLink",
        "94|지원의 응급망|1|Common|Discard|지원 대원이 있을 때 모든 대원 체력 10 회복, 코어 보호막 15|SupportEmergencyNetwork",
        "95|지원 지휘망|3|Epic|Exhaust|지원 대원이 있으면 3장 드로우, 모든 대원에게 보호막 15|SupportCommandNetwork",
        "96|콤보 시동|1|Rare|Exhaust|Combo 자원을 사용하는 대원에게 2스택. 대상이 없으면 1장 드로우|ComboStarter",
        "97|준비 완료|1|Rare|Exhaust|Ready 자원을 사용하는 대원에게 1스택. 이미 있으면 다음 액티브 비용 -1|ReadyComplete",
        "98|별가루 분광|2|Rare|Exhaust|StarPowder 자원 2 획득. 다음 관련 액티브의 자원 비용 -1|StardustPrism",
        "99|비상 키트|1|Common|Exhaust|EmergencyKit 1개 획득. 체력 30% 이하가 되면 체력 30 회복 및 해로운 상태 1개 제거|EmergencyKit",
        "100|최종 방어 명령|5|Epic|Exhaust|코어 보호막 100 회복, 모든 적 Stun 2초, 카드 3장 드로우|FinalDefenseCommand",
    };

    [MenuItem(MenuPath)]
    public static void GenerateFromMenu()
    {
        GenerateCatalog100();
    }

    /// <summary>Unity -executeMethod entry point.</summary>
    public static void GenerateForBatchMode()
    {
        try
        {
            GenerateCatalog100();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    public static void GenerateCatalog100()
    {
        CardSpec[] specs = BuildAndValidateSpecs();
        ReferenceCatalog references = ResolveAndValidateReferences(specs);

        EnsureFolder("Assets/06_Runtime");
        EnsureFolder("Assets/06_Runtime/Resources");
        EnsureFolder(CardsFolder);
        DeleteScratchFolderIfPresent(StagingFolder);
        DeleteScratchFolderIfPresent(BackupFolder);
        EnsureFolder(StagingFolder);

        List<string> stagingPaths = new(100);
        try
        {
            foreach (CardSpec spec in specs)
            {
                string path = $"{StagingFolder}/{spec.FileName}";
                BattleCardSO card = ScriptableObject.CreateInstance<BattleCardSO>();
                card.name = Path.GetFileNameWithoutExtension(spec.FileName);
                PopulateCard(card, spec, references);
                AssetDatabase.CreateAsset(card, path);
                stagingPaths.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateStagedAssets(specs, stagingPaths);
            CommitStagedCatalog(specs, stagingPaths);

            BattleCardCatalog.Invalidate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Rebuilt and validated 100 BattleCardSO assets in " +
                $"'{CardsFolder}'.");
        }
        catch
        {
            DeleteScratchFolderIfPresent(StagingFolder);
            AssetDatabase.Refresh();
            throw;
        }
    }

    private static CardSpec[] BuildAndValidateSpecs()
    {
        CardSpec[] specs = RawCards.Select(ParseCard).ToArray();
        foreach (CardSpec spec in specs)
            ConfigureGameplay(spec);

        if (specs.Length != 100)
            throw new InvalidOperationException(
                $"Catalog must contain exactly 100 cards, got {specs.Length}.");

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> fileNames = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < specs.Length; index++)
        {
            CardSpec spec = specs[index];
            int expectedNumber = index + 1;
            if (spec.Number != expectedNumber)
                throw new InvalidOperationException(
                    $"Catalog row {index + 1} has number {spec.Number}; " +
                    $"expected {expectedNumber}.");
            if (string.IsNullOrWhiteSpace(spec.KoreanName) ||
                string.IsNullOrWhiteSpace(spec.KoreanDescription) ||
                spec.KoreanName.Contains('†') ||
                spec.Cost < 0 || spec.Cost > 5)
            {
                throw new InvalidOperationException(
                    $"Card {spec.Number} has invalid document metadata.");
            }
            if (!ids.Add(spec.CardId) || !fileNames.Add(spec.FileName))
                throw new InvalidOperationException(
                    $"Card {spec.Number} duplicates an ID or asset filename.");
            if (spec.Effects.Count == 0 && spec.Operations.Count == 0)
                throw new InvalidOperationException(
                    $"Card {spec.Number} has no executable definition.");
            ValidateSpecReferences(spec);
        }

        ValidateCount(specs, s => s.Rarity == ItemRarity.Common, 43,
            "Common rarity");
        ValidateCount(specs, s => s.Rarity == ItemRarity.Rare, 45,
            "Rare rarity");
        ValidateCount(specs, s => s.Rarity == ItemRarity.Epic, 12,
            "Epic rarity");
        ValidateCount(specs,
            s => s.RecyclePolicy == BattleCardRecyclePolicy.Discard,
            35,
            "discard policy");
        ValidateCount(specs,
            s => s.RecyclePolicy == BattleCardRecyclePolicy.Exhaust,
            65,
            "exhaust policy");

        HashSet<int> actualStarting = specs
            .Where(s => s.AvailableAsStartingCard)
            .Select(s => s.Number)
            .ToHashSet();
        if (!actualStarting.SetEquals(StartingCardNumbers))
            throw new InvalidOperationException(
                "The ten starting-card flags do not match the configured " +
                "vertical-slice starting set.");

        return specs;
    }

    private static CardSpec ParseCard(string raw)
    {
        string[] columns = raw.Split('|');
        if (columns.Length != 7 ||
            !int.TryParse(columns[0], out int number) ||
            !int.TryParse(columns[2], out int cost) ||
            !Enum.TryParse(columns[3], out ItemRarity rarity) ||
            !Enum.TryParse(
                columns[4],
                out BattleCardRecyclePolicy recyclePolicy))
        {
            throw new InvalidOperationException(
                $"Invalid raw card row: '{raw}'.");
        }

        return new CardSpec
        {
            Number = number,
            KoreanName = columns[1],
            Cost = cost,
            Rarity = rarity,
            RecyclePolicy = recyclePolicy,
            KoreanDescription = columns[5],
            Slug = columns[6],
            AvailableAsStartingCard = StartingCardNumbers.Contains(number),
        };
    }

    private static void ValidateCount(
        IEnumerable<CardSpec> specs,
        Func<CardSpec, bool> predicate,
        int expected,
        string label)
    {
        int actual = specs.Count(predicate);
        if (actual != expected)
            throw new InvalidOperationException(
                $"Expected {expected} cards for {label}, got {actual}.");
    }

    private static void ConfigureGameplay(CardSpec s)
    {
        switch (s.Number)
        {
            case 1:
                TargetEnemy(s);
                s.Effects.Add(Damage(18));
                break;
            case 2:
                TargetEnemy(s);
                s.Effects.Add(Damage(9, "hit1"));
                s.Effects.Add(Damage(9, "hit2"));
                break;
            case 3:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "front",
                    BattleCardTargetScope.Primary,
                    Damage(16)));
                s.Operations.Add(Shared(
                    "behind",
                    BattleCardTargetScope.BehindPrimaryEnemy,
                    Damage(8)));
                break;
            case 4:
                TargetWorld(s, 1.5f);
                s.Effects.Add(Damage(15));
                break;
            case 5:
                TargetWorld(s, 2f);
                s.Effects.Add(Damage(12));
                s.Effects.Add(ApplyStatus("Opening", 1f));
                break;
            case 6:
                TargetEnemy(s, CharacterAttackSubject.Random, 3);
                s.Effects.Add(Damage(10));
                break;
            case 7:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "focus_target",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Focus", 1f, 8f)));
                s.Operations.Add(Operation(
                    "next_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.AllAllies,
                    amount: 12,
                    count: 1,
                    requiredStatus: "Focus"));
                break;
            case 8:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "opening",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Opening", 1f)));
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 9:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "base_damage",
                    BattleCardTargetScope.Primary,
                    Damage(25)));
                s.Operations.Add(Shared(
                    "low_health_bonus",
                    BattleCardTargetScope.Primary,
                    Damage(20),
                    Condition(
                        BattleCardConditionType.TargetHealthPercentage,
                        CharacterNumericComparison.LessThanOrEqual,
                        35f)));
                break;
            case 10:
                TargetEnemy(s);
                s.Effects.Add(Damage(10));
                s.Effects.Add(ApplyStatus("Stun", 1f, 1.5f));
                break;
            case 11:
                TargetEnemy(s);
                s.Effects.Add(Damage(5));
                s.Effects.Add(ApplyStatus("Fire", 2f));
                break;
            case 12:
                TargetEnemy(s);
                s.Effects.Add(Damage(5));
                s.Effects.Add(ApplyStatus("Blood", 2f));
                break;
            case 13:
                TargetEnemy(s);
                s.Effects.Add(Damage(5));
                s.Effects.Add(ApplyStatus("Poison", 2f));
                break;
            case 14:
                TargetEnemy(s);
                // Fixed is the shared damage channel that bypasses ordinary
                // physical/magical protection modifiers.
                s.Effects.Add(Damage(
                    20,
                    damageType: CharacterAttackDamageType.Fixed));
                break;
            case 15:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "execute_low_health",
                    BattleCardTargetScope.AllEnemies,
                    Damage(30),
                    Condition(
                        BattleCardConditionType.TargetHealthPercentage,
                        CharacterNumericComparison.LessThanOrEqual,
                        40f)));
                s.Operations.Add(Operation(
                    "draw_on_defeat",
                    BattleCardOperationType.Draw,
                    count: 2,
                    condition: Condition(
                        BattleCardConditionType.PreviousOperationDefeatedAny)));
                break;
            case 16:
                TargetWorld(s, 2f);
                s.Effects.Add(Damage(18));
                break;
            case 17:
                TargetWorld(s, 1.5f);
                s.Operations.Add(Operation(
                    "delayed_lightning",
                    BattleCardOperationType.CreateZone,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    amount: 22,
                    delay: 2f,
                    radius: 1.5f,
                    zoneTrigger: BattleCardZoneTrigger.AfterDelay));
                s.Operations[^1].SharedEffect = Damage(22);
                break;
            case 18:
                TargetWorld(s, 1f);
                s.Operations.Add(Operation(
                    "mine",
                    BattleCardOperationType.CreateZone,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    amount: 25,
                    radius: 1f,
                    status: "Stun",
                    statusDuration: 1f,
                    zoneTrigger: BattleCardZoneTrigger.OnEnemyEnter,
                    oncePerTarget: true));
                s.Operations[^1].SharedEffect = Damage(25);
                s.Operations.Add(Operation(
                    "mine_stun",
                    BattleCardOperationType.CreateZone,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    radius: 1f,
                    status: "Stun",
                    statusDuration: 1f,
                    zoneTrigger: BattleCardZoneTrigger.OnEnemyEnter,
                    oncePerTarget: true));
                s.Operations[^1].SharedEffect =
                    ApplyStatus("Stun", 1f, 1f);
                break;
            case 19:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "fire_damage",
                    BattleCardTargetScope.EnemiesWithStatus,
                    Damage(12),
                    requiredStatus: "Fire"));
                s.Operations.Add(Shared(
                    "consume_fire",
                    BattleCardTargetScope.EnemiesWithStatus,
                    RemoveStatus("Fire", 1),
                    requiredStatus: "Fire"));
                break;
            case 20:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "blood_damage",
                    BattleCardTargetScope.EnemiesWithStatus,
                    Damage(18),
                    requiredStatus: "Blood"));
                s.Operations.Add(Shared(
                    "consume_blood",
                    BattleCardTargetScope.EnemiesWithStatus,
                    RemoveStatus("Blood", 1),
                    requiredStatus: "Blood"));
                break;
            case 21:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "poison_primary",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Poison", 2f)));
                s.Operations.Add(Shared(
                    "poison_nearby",
                    BattleCardTargetScope.NearbyPrimaryEnemies,
                    ApplyStatus("Poison", 1f),
                    count: 2));
                break;
            case 22:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "opening_primary",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Opening", 1f)));
                s.Operations.Add(Shared(
                    "nearby_damage",
                    BattleCardTargetScope.NearbyPrimaryEnemies,
                    Damage(10),
                    count: 0));
                break;
            case 23:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "random_stun",
                    BattleCardTargetScope.RandomEnemies,
                    ApplyStatus("Stun", 1f, 1f),
                    count: 3));
                break;
            case 24:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "defense_line_stun",
                    BattleCardTargetScope.DefenseLineEnemies,
                    ApplyStatus("Stun", 1f, 1.5f)));
                break;
            case 25:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "time_stop",
                    BattleCardTargetScope.AllEnemies,
                    ApplyStatus("Stun", 1f, 2f)));
                break;
            case 26:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "focus",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Focus", 1f, 8f)));
                break;
            case 27:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "recent_core_attackers",
                    BattleCardTargetScope.RecentObjectiveAttackers,
                    Damage(25),
                    duration: 5f));
                break;
            case 28:
                TargetWorld(s, 1.5f);
                s.Operations.Add(Shared(
                    "smoke_stun",
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    ApplyStatus("Stun", 1f, 0.75f)));
                s.Operations.Add(Shared(
                    "remove_focus",
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    RemoveStatus("Focus", 0)));
                break;
            case 29:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "focused_damage",
                    BattleCardTargetScope.EnemiesWithStatus,
                    Damage(20),
                    requiredStatus: "Focus"));
                break;
            case 30:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "all_damage",
                    BattleCardTargetScope.AllEnemies,
                    Damage(20)));
                s.Operations.Add(Shared(
                    "all_fire",
                    BattleCardTargetScope.AllEnemies,
                    ApplyStatus("Fire", 2f)));
                break;
            case 31:
                TargetNone(s);
                s.Operations.Add(ObjectiveRestore("repair", 25));
                break;
            case 32:
                TargetNone(s);
                s.Operations.Add(ObjectiveRestore(
                    "critical_bonus",
                    20,
                    Condition(
                        BattleCardConditionType.ObjectiveHealthPercentage,
                        CharacterNumericComparison.LessThanOrEqual,
                        30f)));
                s.Operations.Add(ObjectiveRestore("repair", 45));
                break;
            case 33:
                TargetNone(s);
                s.Operations.Add(ObjectiveRestore("restart", 80));
                break;
            case 34:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "shield",
                    BattleCardTargetScope.Primary,
                    Shield(35)));
                s.Operations.Add(Shared(
                    "critical_core_bonus",
                    BattleCardTargetScope.Primary,
                    Shield(15),
                    Condition(
                        BattleCardConditionType.ObjectiveHealthPercentage,
                        CharacterNumericComparison.LessThanOrEqual,
                        30f)));
                break;
            case 35:
                TargetAlly(s);
                s.Effects.Add(Shield(25));
                break;
            case 36:
                TargetAlly(s);
                s.Effects.Add(Heal(30));
                break;
            case 37:
                TargetAlly(s, CharacterAttackSubject.All);
                s.Effects.Add(Heal(18));
                break;
            case 38:
                TargetAlly(s, includeDefeated: true);
                s.Operations.Add(Operation(
                    "revive_or_heal",
                    BattleCardOperationType.Revive,
                    BattleCardTargetScope.DeadOrLowestHealthAlly,
                    amount: 35,
                    ratio: 0.35f));
                break;
            case 39:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "health_cost",
                    BattleCardOperationType.SpendTargetHealth,
                    BattleCardTargetScope.Primary,
                    amount: 15));
                s.Operations.Add(ObjectiveRestore(
                    "sacrificial_repair",
                    80,
                    Condition(
                        BattleCardConditionType.PreviousOperationSucceeded)));
                break;
            case 40:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "redirect_next_core_damage",
                    BattleCardOperationType.ObjectiveDamageRedirect,
                    BattleCardTargetScope.Primary,
                    ratio: 0.3f,
                    count: 1));
                break;
            case 41:
                TargetNone(s);
                s.Operations.Add(ObjectiveRestore("last_line_repair", 50));
                s.Operations.Add(Shared(
                    "last_line_stun",
                    BattleCardTargetScope.DefenseLineEnemies,
                    ApplyStatus("Stun", 1f, 1f)));
                break;
            case 42:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "emergency_shield",
                    BattleCardTargetScope.Primary,
                    Shield(20)));
                s.Operations.Add(Shared(
                    "emergency_cleanse",
                    BattleCardTargetScope.Primary,
                    RemoveDebuffs(1)));
                break;
            case 43:
                TargetAlly(s);
                ConditionSpec lowHealth = Condition(
                    BattleCardConditionType.TargetHealthPercentage,
                    CharacterNumericComparison.LessThanOrEqual,
                    30f);
                s.Operations.Add(Shared(
                    "low_health_power",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Power", 1f),
                    lowHealth));
                s.Operations.Add(Shared(
                    "low_health_heal",
                    BattleCardTargetScope.Primary,
                    Heal(20),
                    lowHealth));
                break;
            case 44:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "heal",
                    BattleCardTargetScope.Primary,
                    Heal(20)));
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 45:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "core_invulnerability",
                    BattleCardOperationType.ObjectiveInvulnerability,
                    duration: 3f));
                break;
            case 46:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 47:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "draw_two",
                    BattleCardOperationType.Draw,
                    count: 2));
                s.Operations.Add(CardSelection(
                    "discard_one",
                    BattleCardOperationType.DiscardSelected,
                    1,
                    1));
                break;
            case 48:
                TargetNone(s);
                s.Operations.Add(CardSelection(
                    "discard_one",
                    BattleCardOperationType.DiscardSelected,
                    1,
                    1));
                s.Operations.Add(Operation(
                    "draw_two",
                    BattleCardOperationType.Draw,
                    count: 2));
                break;
            case 49:
                TargetNone(s);
                s.Operations.Add(CardSelection(
                    "return_discarded",
                    BattleCardOperationType.ReturnDiscarded,
                    1,
                    1));
                break;
            case 50:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "shuffle_discard",
                    BattleCardOperationType.ShuffleDiscardIntoDraw));
                s.Operations.Add(Operation(
                    "draw_two",
                    BattleCardOperationType.Draw,
                    count: 2));
                break;
            case 51:
                TargetNone(s);
                s.Operations.Add(CardSelection(
                    "exhaust_up_to_two",
                    BattleCardOperationType.ExhaustSelected,
                    0,
                    2));
                s.Operations.Add(Operation(
                    "draw_changed_plus_one",
                    BattleCardOperationType.Draw,
                    count: 1,
                    usePreviousChangedCount: true));
                break;
            case 52:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "discard_hand",
                    BattleCardOperationType.DiscardHand));
                s.Operations.Add(Operation(
                    "draw_four",
                    BattleCardOperationType.Draw,
                    count: 4));
                break;
            case 53:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "gain_energy",
                    BattleCardOperationType.GainEnergy,
                    amount: 1));
                s.Operations.Add(Operation(
                    "small_hand_draw",
                    BattleCardOperationType.Draw,
                    count: 1,
                    condition: Condition(
                        BattleCardConditionType.HandCount,
                        CharacterNumericComparison.LessThanOrEqual,
                        2f)));
                break;
            case 54:
                TargetNone(s);
                s.Operations.Add(CostModifier(
                    "reduce_next_three",
                    BattleCardCostModifierMode.Add,
                    1,
                    3));
                break;
            case 55:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "draw_two",
                    BattleCardOperationType.Draw,
                    count: 2));
                s.Operations.Add(CostModifier(
                    "next_cost_zero",
                    BattleCardCostModifierMode.Set,
                    0,
                    1));
                break;
            case 56:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "shuffle_all",
                    BattleCardOperationType.ShuffleDrawAndDiscard));
                s.Operations.Add(Operation(
                    "draw_five",
                    BattleCardOperationType.Draw,
                    count: 5));
                break;
            case 57:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "protect_next_redraw",
                    BattleCardOperationType.ProtectHand,
                    count: 1));
                s.Operations.Add(Operation(
                    "draw_one",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 58:
                TargetNone(s);
                s.Operations.Add(CostModifier(
                    "reduce_next",
                    BattleCardCostModifierMode.Add,
                    1,
                    1));
                s.Operations.Add(Operation(
                    "draw_one",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 59:
                TargetNone(s);
                s.Operations.Add(CardSelection(
                    "exhaust_one",
                    BattleCardOperationType.ExhaustSelected,
                    1,
                    1));
                s.Operations.Add(Operation(
                    "gain_energy",
                    BattleCardOperationType.GainEnergy,
                    amount: 1,
                    condition: Condition(
                        BattleCardConditionType.PreviousOperationSucceeded)));
                break;
            case 60:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "discard_hand",
                    BattleCardOperationType.DiscardHand));
                s.Operations.Add(Operation(
                    "gain_energy",
                    BattleCardOperationType.GainEnergy,
                    amount: 2));
                s.Operations.Add(Operation(
                    "draw_two",
                    BattleCardOperationType.Draw,
                    count: 2));
                break;
            case 61:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "next_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 10,
                    count: 1));
                break;
            case 62:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "shield",
                    BattleCardTargetScope.Primary,
                    Shield(15)));
                break;
            case 63:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "ready_basic_attack",
                    BattleCardOperationType.ReadyBasicAttack,
                    BattleCardTargetScope.Primary,
                    count: 1));
                break;
            case 64:
                TargetAlly(s, targetCount: 2);
                s.Operations.Add(Operation(
                    "ready_two_basic_attacks",
                    BattleCardOperationType.ReadyBasicAttack,
                    BattleCardTargetScope.Primary,
                    count: 2));
                s.Operations.Add(Operation(
                    "next_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 10,
                    count: 1));
                break;
            case 65:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "rally_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.AllAllies,
                    amount: 4,
                    count: 1));
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 66:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "next_basic_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.AllAllies,
                    amount: 12,
                    count: 1));
                break;
            case 67:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "repeat_half_power",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 0,
                    ratio: 0.5f,
                    count: 1));
                break;
            case 68:
                TargetWorld(s, 1.5f);
                s.Operations.Add(Operation(
                    "blockade_zone",
                    BattleCardOperationType.CreateZone,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    amount: 15,
                    duration: 5f,
                    radius: 1.5f,
                    status: "Stun",
                    statusDuration: 1f,
                    zoneTrigger: BattleCardZoneTrigger.OnEnemyEnter,
                    oncePerTarget: true));
                s.Operations[^1].SharedEffect = Damage(15);
                s.Operations.Add(Operation(
                    "blockade_stun",
                    BattleCardOperationType.CreateZone,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    duration: 5f,
                    radius: 1.5f,
                    status: "Stun",
                    statusDuration: 1f,
                    zoneTrigger: BattleCardZoneTrigger.OnEnemyEnter,
                    oncePerTarget: true));
                s.Operations[^1].SharedEffect =
                    ApplyStatus("Stun", 1f, 1f);
                break;
            case 69:
                TargetWorld(s, 2f);
                s.Operations.Add(Operation(
                    "pull",
                    BattleCardOperationType.PullEnemies,
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    radius: 2f));
                s.Operations.Add(Shared(
                    "pull_stun",
                    BattleCardTargetScope.EnemiesAtDesignatedPoint,
                    ApplyStatus("Stun", 1f, 1f)));
                break;
            case 70:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "encirclement_opening",
                    BattleCardTargetScope.AllEnemies,
                    ApplyStatus("Opening", 1f)));
                s.Operations.Add(Operation(
                    "encirclement_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.AllAllies,
                    amount: 10,
                    count: 1));
                s.Operations.Add(Operation(
                    "ready_all_basic_attacks",
                    BattleCardOperationType.ReadyBasicAttack,
                    BattleCardTargetScope.AllAllies));
                break;
            case 71:
                TargetAlly(s);
                s.Effects.Add(ApplyStatus("Power", 1f, 10f));
                break;
            case 72:
                TargetAlly(s);
                s.Effects.Add(ApplyStatus("Speed", 1f, 10f));
                break;
            case 73:
                TargetAlly(s);
                SetSecondaryCharacterTarget(s, CharacterTargetFaction.Enemy);
                s.Operations.Add(Operation(
                    "force_target_for_ally",
                    BattleCardOperationType.ForceTarget,
                    BattleCardTargetScope.Secondary,
                    duration: 8f));
                break;
            case 74:
                TargetEnemy(s);
                s.Effects.Add(ApplyStatus("Opening", 1f, 10f));
                break;
            case 75:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "fire_next_three",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    count: 3,
                    status: "Fire",
                    statusDuration: 3f,
                    statusStacks: 1f));
                break;
            case 76:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "blood_next_three",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    count: 3,
                    status: "Blood",
                    statusDuration: 3f,
                    statusStacks: 1f));
                break;
            case 77:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "poison_next_three",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    count: 3,
                    status: "Poison",
                    statusDuration: 3f,
                    statusStacks: 1f));
                break;
            case 78:
                TargetAlly(s);
                s.Effects.Add(ApplyStatus("DualSide", 1f, 8f));
                break;
            case 79:
                TargetAlly(s);
                s.Operations.Add(Operation(
                    "double_half_power",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 0,
                    ratio: 0.5f,
                    count: 3));
                break;
            case 80:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "remove_speed",
                    BattleCardTargetScope.Primary,
                    RemoveStatus("Speed", 1)));
                s.Operations.Add(Shared(
                    "gain_power",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Power", 2f),
                    Condition(
                        BattleCardConditionType.PreviousOperationSucceeded)));
                break;
            case 81:
                TargetAlly(s);
                s.Operations.Add(Shared(
                    "remove_power",
                    BattleCardTargetScope.Primary,
                    RemoveStatus("Power", 1)));
                s.Operations.Add(Shared(
                    "gain_speed",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Speed", 2f),
                    Condition(
                        BattleCardConditionType.PreviousOperationSucceeded)));
                break;
            case 82:
                TargetAlly(s);
                s.Effects.Add(RemoveDebuffs(2));
                break;
            case 83:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "remove_damage_statuses",
                    BattleCardTargetScope.Primary,
                    RemoveStatuses(new[] { "Fire", "Blood", "Poison" }, 0)));
                s.Operations.Add(Shared(
                    "damage_per_removed_stack",
                    BattleCardTargetScope.Primary,
                    Damage(10),
                    usePreviousChangedCount: true));
                break;
            case 84:
                TargetNone(s);
                s.Operations.Add(Operation(
                    "extend_opening",
                    BattleCardOperationType.ExtendStatusDuration,
                    BattleCardTargetScope.EnemiesWithStatus,
                    duration: 5f,
                    status: "Opening",
                    requiredStatus: "Opening"));
                s.Operations.Add(Shared(
                    "opening_damage",
                    BattleCardTargetScope.EnemiesWithStatus,
                    Damage(15),
                    requiredStatus: "Opening"));
                break;
            case 85:
                TargetEnemy(s);
                s.Operations.Add(Shared(
                    "primary_stun",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Stun", 1f, 1.5f)));
                s.Operations.Add(Shared(
                    "nearby_stun",
                    BattleCardTargetScope.NearbyPrimaryEnemies,
                    ApplyStatus("Stun", 1f, 0.75f),
                    count: 0));
                break;
            case 86:
                ConfigureRoleCard(s, "Vanguard", VanguardCharacters);
                s.Operations.Add(Shared(
                    "vanguard_shield",
                    BattleCardTargetScope.Primary,
                    Shield(30)));
                s.Operations.Add(Shared(
                    "low_health_bonus",
                    BattleCardTargetScope.Primary,
                    Shield(15),
                    Condition(
                        BattleCardConditionType.TargetHealthPercentage,
                        CharacterNumericComparison.LessThanOrEqual,
                        50f)));
                break;
            case 87:
                ConfigureRoleCard(s, "Vanguard", VanguardCharacters);
                s.Operations.Add(Shared(
                    "vanguard_power",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Power", 1f)));
                s.Operations.Add(Operation(
                    "next_attack_stun",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    count: 1,
                    status: "Stun",
                    statusDuration: 1f));
                break;
            case 88:
                ConfigureRoleCard(s, "Shooter", ShooterCharacters);
                SetSecondaryCharacterTarget(s, CharacterTargetFaction.Enemy);
                s.Operations.Add(Operation(
                    "shooter_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 20,
                    count: 1));
                s.Operations.Add(Shared(
                    "opening_target",
                    BattleCardTargetScope.Secondary,
                    ApplyStatus("Opening", 1f)));
                break;
            case 89:
                ConfigureRoleCard(s, "Shooter", ShooterCharacters, all: true);
                s.Operations.Add(Shared(
                    "shooter_speed",
                    BattleCardTargetScope.AlliesWithRole,
                    ApplyStatus("Speed", 1f, 8f),
                    requiredRole: "Shooter"));
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 90:
                ConfigureRoleCard(s, "Mage", MageCharacters);
                s.Operations.Add(Operation(
                    "extra_status_stack",
                    BattleCardOperationType.ApplySkillModifier,
                    BattleCardTargetScope.Primary,
                    amount: 1,
                    count: 1));
                s.Operations[^1].RequiredRole = "Mage";
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 91:
                ConfigureRoleCard(s, "Mage", MageCharacters);
                s.Operations.Add(Operation(
                    "health_cost",
                    BattleCardOperationType.SpendTargetHealth,
                    BattleCardTargetScope.Primary,
                    amount: 10));
                s.Operations.Add(Shared(
                    "mage_power",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Power", 2f, 8f),
                    Condition(
                        BattleCardConditionType.PreviousOperationSucceeded)));
                break;
            case 92:
                ConfigureRoleCard(s, "Scout", ScoutCharacters);
                SetSecondaryCharacterTarget(
                    s,
                    CharacterTargetFaction.Enemy,
                    requiredStatus: "Opening");
                s.Operations.Add(Operation(
                    "opening_attack_bonus",
                    BattleCardOperationType.ApplyAttackModifier,
                    BattleCardTargetScope.Primary,
                    amount: 30,
                    count: 1,
                    requiredStatus: "Opening"));
                s.Operations.Add(Operation(
                    "draw",
                    BattleCardOperationType.Draw,
                    count: 1));
                break;
            case 93:
                ConfigureRoleCard(s, "Scout", ScoutCharacters);
                s.Operations.Add(Shared(
                    "scout_speed",
                    BattleCardTargetScope.Primary,
                    ApplyStatus("Speed", 1f, 8f)));
                s.Operations.Add(Operation(
                    "kill_energy_window",
                    BattleCardOperationType.ApplyHealthTrigger,
                    BattleCardTargetScope.Primary,
                    amount: 1,
                    ratio: 0f,
                    duration: 8f,
                    count: 1));
                break;
            case 94:
                TargetNone(s);
                ConditionSpec hasSupport = Condition(
                    BattleCardConditionType.PartyRoleCount,
                    CharacterNumericComparison.GreaterThanOrEqual,
                    1f,
                    role: "Support");
                s.Operations.Add(Shared(
                    "support_heal_all",
                    BattleCardTargetScope.AllAllies,
                    Heal(10),
                    hasSupport));
                s.Operations.Add(ObjectiveRestore(
                    "support_core_repair",
                    15,
                    hasSupport));
                break;
            case 95:
                TargetNone(s);
                ConditionSpec supportPresent = Condition(
                    BattleCardConditionType.PartyRoleCount,
                    CharacterNumericComparison.GreaterThanOrEqual,
                    1f,
                    role: "Support");
                s.Operations.Add(Operation(
                    "support_draw",
                    BattleCardOperationType.Draw,
                    count: 3,
                    condition: supportPresent));
                s.Operations.Add(Shared(
                    "support_shield_all",
                    BattleCardTargetScope.AllAllies,
                    Shield(15),
                    supportPresent));
                break;
            case 96:
                TargetNone(s);
                s.Operations.Add(Shared(
                    "combo_gain",
                    BattleCardTargetScope.SpecificCharacter,
                    ApplyStatus("Combo", 2f),
                    Condition(
                        BattleCardConditionType.MatchingTargetCount,
                        CharacterNumericComparison.GreaterThanOrEqual,
                        1f),
                    requiredCharacter: "Byeolha",
                    status: "Combo"));
                OperationSpec comboFallback = Operation(
                    "fallback_draw",
                    BattleCardOperationType.Draw,
                    BattleCardTargetScope.SpecificCharacter,
                    count: 1,
                    condition: Condition(
                        BattleCardConditionType.MatchingTargetCount,
                        CharacterNumericComparison.LessThan,
                        1f));
                comboFallback.RequiredCharacter = "Byeolha";
                s.Operations.Add(comboFallback);
                break;
            case 97:
                TargetAlly(s);
                RequireCharacters(s, ReadyCharacters);
                AddReadyResourceBranch(s, "Isolde", "Ready");
                AddReadyResourceBranch(s, "Isana", "Ready_0");
                AddReadyResourceBranch(s, "Calista", "Ready_4");
                break;
            case 98:
                TargetNone(s);
                ConfigureExclusiveCharacter(s, "Mirinae");
                s.Operations.Add(Shared(
                    "star_powder_gain",
                    BattleCardTargetScope.Source,
                    ApplyStatus("StarPowder", 2f),
                    status: "StarPowder"));
                s.Operations.Add(Operation(
                    "related_skill_resource_reduction",
                    BattleCardOperationType.ApplySkillModifier,
                    BattleCardTargetScope.Source,
                    amount: 1,
                    count: 1,
                    status: "StarPowder"));
                break;
            case 99:
                TargetNone(s);
                ConfigureExclusiveCharacter(s, "Suiren");
                s.Operations.Add(Shared(
                    "emergency_kit_gain",
                    BattleCardTargetScope.Source,
                    ApplyStatus("EmergencyKit", 1f),
                    status: "EmergencyKit"));
                s.Operations.Add(Operation(
                    "emergency_kit_trigger",
                    BattleCardOperationType.ApplyHealthTrigger,
                    BattleCardTargetScope.Source,
                    amount: 30,
                    ratio: 0.3f,
                    count: 1,
                    status: "EmergencyKit"));
                break;
            case 100:
                TargetNone(s);
                s.Operations.Add(ObjectiveRestore("final_repair", 100));
                s.Operations.Add(Shared(
                    "final_stun",
                    BattleCardTargetScope.AllEnemies,
                    ApplyStatus("Stun", 1f, 2f)));
                s.Operations.Add(Operation(
                    "final_draw",
                    BattleCardOperationType.Draw,
                    count: 3));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(s.Number),
                    s.Number,
                    "Unexpected card number.");
        }
    }

    private static readonly string[] VanguardCharacters =
        { "Byeolha", "Calista", "Isana" };
    private static readonly string[] ShooterCharacters = { "Aisling" };
    private static readonly string[] MageCharacters =
        { "Isolde", "Mirinae" };
    private static readonly string[] ScoutCharacters = { "Saena" };
    private static readonly string[] ReadyCharacters =
        { "Isolde", "Isana", "Calista" };

    private static void TargetEnemy(
        CardSpec spec,
        CharacterAttackSubject subject = CharacterAttackSubject.Manual,
        int targetCount = 1)
    {
        spec.TargetFaction = CharacterTargetFaction.Enemy;
        spec.Subject = subject;
        spec.TargetCount = targetCount;
    }

    private static void TargetAlly(
        CardSpec spec,
        CharacterAttackSubject subject = CharacterAttackSubject.Manual,
        int targetCount = 1,
        bool includeDefeated = false)
    {
        spec.TargetFaction = CharacterTargetFaction.Ally;
        spec.Subject = subject;
        spec.TargetCount = targetCount;
        spec.PrimaryFilter.IncludeDefeated = includeDefeated;
    }

    private static void TargetNone(CardSpec spec)
    {
        spec.TargetFaction = CharacterTargetFaction.Enemy;
        spec.Subject = CharacterAttackSubject.None;
        spec.TargetCount = 1;
    }

    private static void TargetWorld(CardSpec spec, float radius)
    {
        TargetEnemy(spec, CharacterAttackSubject.Manual, 0);
        spec.Area = new AreaSpec
        {
            IsWorldArea = true,
            Radius = radius,
        };
    }

    private static void SetSecondaryWorldPoint(CardSpec spec)
    {
        spec.Secondary.Enabled = true;
        spec.Secondary.WorldPoint = true;
        spec.Secondary.TargetFaction = CharacterTargetFaction.Enemy;
        spec.Secondary.Subject = CharacterAttackSubject.Manual;
        spec.Secondary.TargetCount = 0;
        spec.Secondary.Area = new AreaSpec
        {
            IsWorldArea = true,
            Radius = 0.5f,
        };
    }

    private static void SetSecondaryCharacterTarget(
        CardSpec spec,
        CharacterTargetFaction faction,
        string requiredStatus = null)
    {
        spec.Secondary.Enabled = true;
        spec.Secondary.WorldPoint = false;
        spec.Secondary.TargetFaction = faction;
        spec.Secondary.Subject = CharacterAttackSubject.Manual;
        spec.Secondary.TargetCount = 1;
        spec.Secondary.Filter.RequiredStatus = requiredStatus;
    }

    private static void ConfigureRoleCard(
        CardSpec spec,
        string role,
        IEnumerable<string> characterNames,
        bool all = false)
    {
        if (all)
            TargetNone(spec);
        else
        {
            TargetAlly(spec);
            spec.PrimaryFilter.RequiredRole = role;
        }
        RequireCharacters(spec, characterNames);
    }

    private static void RequireCharacters(
        CardSpec spec,
        IEnumerable<string> characterNames)
    {
        spec.Affiliation = BattleCardAffiliation.CharacterDependent;
        spec.RequirementMode = BattleCardRequirementMatchMode.Any;
        spec.SourcePolicy = BattleCardSourcePolicy.FirstRequiredCharacter;
        spec.RequiredCharacters.Clear();
        spec.RequiredCharacters.AddRange(characterNames);
    }

    private static void ConfigureExclusiveCharacter(
        CardSpec spec,
        string characterName)
    {
        spec.Affiliation = BattleCardAffiliation.CharacterExclusive;
        spec.OwnerCharacter = characterName;
        spec.SourcePolicy = BattleCardSourcePolicy.FixedCharacter;
    }

    private static void AddReadyResourceBranch(
        CardSpec spec,
        string characterName,
        string readyStatus)
    {
        string branch = characterName.ToLowerInvariant();
        OperationSpec fullResourceModifier = Operation(
            $"{branch}_active_cost_reduction_when_full",
            BattleCardOperationType.ApplySkillModifier,
            BattleCardTargetScope.Primary,
            amount: 1,
            count: 1,
            status: readyStatus,
            condition: Condition(
                BattleCardConditionType.TargetHasStatus,
                status: readyStatus));
        fullResourceModifier.RequiredCharacter = characterName;
        spec.Operations.Add(fullResourceModifier);

        spec.Operations.Add(Shared(
            $"{branch}_ready_gain",
            BattleCardTargetScope.Primary,
            ApplyStatus(readyStatus, 1f),
            Condition(
                BattleCardConditionType.PreviousOperationFailed),
            requiredCharacter: characterName,
            status: readyStatus));
    }

    private static EffectSpec Damage(
        float amount,
        string id = null,
        CharacterAttackDamageType damageType =
            CharacterAttackDamageType.Physical)
    {
        return new EffectSpec
        {
            Id = id,
            Type = CharacterEffectType.Damage,
            DamageType = damageType,
            Amount = amount,
        };
    }

    private static EffectSpec Heal(float amount)
    {
        return new EffectSpec
        {
            Type = CharacterEffectType.Heal,
            Amount = amount,
        };
    }

    private static EffectSpec Shield(float amount)
    {
        return new EffectSpec
        {
            Type = CharacterEffectType.Shield,
            Amount = amount,
        };
    }

    private static EffectSpec ApplyStatus(
        string status,
        float stacks,
        float duration = 0f)
    {
        return new EffectSpec
        {
            Type = CharacterEffectType.ApplyStatus,
            Status = status,
            StatusStacks = stacks,
            StatusDuration = duration,
        };
    }

    private static EffectSpec RemoveStatus(string status, int stacks)
    {
        return RemoveStatuses(new[] { status }, stacks);
    }

    private static EffectSpec RemoveStatuses(
        IEnumerable<string> statuses,
        int stacks)
    {
        EffectSpec effect = new()
        {
            Type = CharacterEffectType.RemoveStatus,
            RemovalTarget = CharacterStatusRemovalTarget.Single,
            RemovalPickMode = CharacterStatusRemovalPickMode.AllMatches,
            RemovalPickCount = 1,
            RemovalStackCount = stacks,
        };
        effect.RemovalStatuses.AddRange(statuses);
        return effect;
    }

    private static EffectSpec RemoveDebuffs(int maximumStatusCount)
    {
        return new EffectSpec
        {
            Type = CharacterEffectType.RemoveStatus,
            RemovalTarget = CharacterStatusRemovalTarget.Debuff,
            RemovalPickMode = CharacterStatusRemovalPickMode.RandomCount,
            RemovalPickCount = maximumStatusCount,
            RemovalStackCount = 0,
        };
    }

    private static OperationSpec Shared(
        string id,
        BattleCardTargetScope scope,
        EffectSpec effect,
        ConditionSpec condition = null,
        int count = 1,
        float duration = 0f,
        string requiredRole = null,
        string requiredCharacter = null,
        string status = null,
        string requiredStatus = null,
        bool usePreviousChangedCount = false)
    {
        return new OperationSpec
        {
            Id = id,
            Type = BattleCardOperationType.SharedEffect,
            Scope = scope,
            SharedEffect = effect,
            Condition = condition,
            Count = count,
            Duration = duration,
            RequiredRole = requiredRole,
            RequiredCharacter = requiredCharacter,
            Status = status,
            RequiredStatus = requiredStatus,
            UsePreviousChangedCount = usePreviousChangedCount,
        };
    }

    private static OperationSpec Operation(
        string id,
        BattleCardOperationType type,
        BattleCardTargetScope scope = BattleCardTargetScope.None,
        int amount = 0,
        float ratio = 1f,
        int count = 1,
        float duration = 0f,
        float delay = 0f,
        float radius = 1.5f,
        float statusDuration = 1f,
        float statusStacks = 1f,
        string status = null,
        string requiredStatus = null,
        ConditionSpec condition = null,
        BattleCardZoneTrigger zoneTrigger =
            BattleCardZoneTrigger.AfterDelay,
        bool oncePerTarget = true,
        bool usePreviousChangedCount = false)
    {
        return new OperationSpec
        {
            Id = id,
            Type = type,
            Scope = scope,
            Amount = amount,
            Ratio = ratio,
            Count = count,
            Duration = duration,
            DelaySeconds = delay,
            Radius = radius,
            StatusDuration = statusDuration,
            StatusStacks = statusStacks,
            Status = status,
            RequiredStatus = requiredStatus,
            Condition = condition,
            ZoneTrigger = zoneTrigger,
            OncePerTarget = oncePerTarget,
            UsePreviousChangedCount = usePreviousChangedCount,
        };
    }

    private static OperationSpec ObjectiveRestore(
        string id,
        int amount,
        ConditionSpec condition = null)
    {
        return Operation(
            id,
            BattleCardOperationType.ObjectiveRestore,
            amount: amount,
            condition: condition);
    }

    private static OperationSpec Move(
        string id,
        BattleCardTargetScope scope,
        BattleCardMovementMode movementMode,
        int amount = 0)
    {
        OperationSpec operation = Operation(
            id,
            BattleCardOperationType.Move,
            scope,
            amount: amount);
        operation.MovementMode = movementMode;
        if (movementMode == BattleCardMovementMode.CorewardByDistance ||
            movementMode == BattleCardMovementMode.OutwardByDistance)
        {
            operation.Radius = amount > 0 ? amount : 1f;
        }
        return operation;
    }

    private static OperationSpec CardSelection(
        string id,
        BattleCardOperationType type,
        int minimum,
        int maximum)
    {
        OperationSpec operation = Operation(id, type);
        operation.MinimumSelectionCount = minimum;
        operation.MaximumSelectionCount = maximum;
        return operation;
    }

    private static OperationSpec CostModifier(
        string id,
        BattleCardCostModifierMode mode,
        int amount,
        int count)
    {
        OperationSpec operation = Operation(
            id,
            BattleCardOperationType.ModifyCardCost,
            amount: amount,
            count: count);
        operation.CostModifierMode = mode;
        return operation;
    }

    private static ConditionSpec Condition(
        BattleCardConditionType type,
        CharacterNumericComparison comparison =
            CharacterNumericComparison.GreaterThanOrEqual,
        float threshold = 0f,
        string role = null,
        string status = null,
        BattleCardSpatialZone zone = BattleCardSpatialZone.Core)
    {
        return new ConditionSpec
        {
            Type = type,
            Comparison = comparison,
            Threshold = threshold,
            Role = role,
            Status = status,
            Zone = zone,
        };
    }

    private static void ValidateSpecReferences(CardSpec spec)
    {
        if (!Enum.IsDefined(typeof(ItemRarity), spec.Rarity) ||
            !Enum.IsDefined(
                typeof(BattleCardRecyclePolicy),
                spec.RecyclePolicy) ||
            !Enum.IsDefined(
                typeof(BattleCardAffiliation),
                spec.Affiliation) ||
            !Enum.IsDefined(
                typeof(BattleCardSourcePolicy),
                spec.SourcePolicy))
        {
            throw new InvalidOperationException(
                $"Card {spec.Number} contains an undefined enum value.");
        }

        if (spec.Affiliation == BattleCardAffiliation.CharacterExclusive &&
            string.IsNullOrWhiteSpace(spec.OwnerCharacter))
        {
            throw new InvalidOperationException(
                $"Exclusive card {spec.Number} has no owner character.");
        }
        if (spec.Affiliation == BattleCardAffiliation.CharacterDependent &&
            spec.RequiredCharacters.Count == 0)
        {
            throw new InvalidOperationException(
                $"Dependent card {spec.Number} has no required character.");
        }

        HashSet<string> operationIds = new(StringComparer.Ordinal);
        foreach (EffectSpec effect in spec.Effects)
            ValidateEffectSpec(spec.Number, effect);
        foreach (OperationSpec operation in spec.Operations)
        {
            if (operation == null ||
                string.IsNullOrWhiteSpace(operation.Id) ||
                !operationIds.Add(operation.Id) ||
                !Enum.IsDefined(
                    typeof(BattleCardOperationType),
                    operation.Type) ||
                !Enum.IsDefined(
                    typeof(BattleCardTargetScope),
                    operation.Scope))
            {
                throw new InvalidOperationException(
                    $"Card {spec.Number} has an invalid or duplicate " +
                    "operation definition.");
            }
            if (operation.Type == BattleCardOperationType.SharedEffect)
            {
                if (operation.SharedEffect == null)
                    throw new InvalidOperationException(
                        $"Card {spec.Number}/{operation.Id} has no shared " +
                        "effect.");
            }
            if (operation.Type == BattleCardOperationType.CreateZone &&
                operation.SharedEffect == null)
            {
                throw new InvalidOperationException(
                    $"Card {spec.Number}/{operation.Id} has no zone " +
                    "effect.");
            }
            if (operation.SharedEffect != null)
                ValidateEffectSpec(spec.Number, operation.SharedEffect);
            if (operation.MinimumSelectionCount < 0 ||
                operation.MaximumSelectionCount <
                    operation.MinimumSelectionCount ||
                operation.Count < 0 || operation.Amount < 0 ||
                operation.Ratio < 0f || operation.Duration < 0f ||
                operation.DelaySeconds < 0f || operation.Radius < 0f)
            {
                throw new InvalidOperationException(
                    $"Card {spec.Number}/{operation.Id} has an invalid " +
                    "numeric operation value.");
            }
        }
    }

    private static void ValidateEffectSpec(int number, EffectSpec effect)
    {
        if (effect == null ||
            !Enum.IsDefined(typeof(CharacterEffectType), effect.Type) ||
            effect.Amount < 0f || effect.StatusDuration < 0f ||
            effect.StatusStacks <= 0f)
        {
            throw new InvalidOperationException(
                $"Card {number} has an invalid shared-effect definition.");
        }
        if (effect.Type == CharacterEffectType.ApplyStatus &&
            string.IsNullOrWhiteSpace(effect.Status))
        {
            throw new InvalidOperationException(
                $"Card {number} applies a missing status reference.");
        }
        if (effect.Type == CharacterEffectType.RemoveStatus &&
            effect.RemovalTarget == CharacterStatusRemovalTarget.Single &&
            effect.RemovalStatuses.Count == 0)
        {
            throw new InvalidOperationException(
                $"Card {number} removes an unspecified status.");
        }
    }

    private static ReferenceCatalog ResolveAndValidateReferences(
        IEnumerable<CardSpec> specs)
    {
        HashSet<string> statusNames = new(StringComparer.Ordinal);
        HashSet<string> roleNames = new(StringComparer.Ordinal);
        HashSet<string> characterNames = new(StringComparer.Ordinal);

        foreach (CardSpec spec in specs)
        {
            AddIfPresent(characterNames, spec.OwnerCharacter);
            foreach (string character in spec.RequiredCharacters)
                AddIfPresent(characterNames, character);
            CollectFilterReferences(
                spec.PrimaryFilter,
                statusNames,
                roleNames,
                characterNames);
            CollectFilterReferences(
                spec.Secondary.Filter,
                statusNames,
                roleNames,
                characterNames);
            foreach (EffectSpec effect in spec.Effects)
                CollectEffectReferences(effect, statusNames);
            foreach (OperationSpec operation in spec.Operations)
            {
                AddIfPresent(roleNames, operation.RequiredRole);
                AddIfPresent(characterNames, operation.RequiredCharacter);
                AddIfPresent(statusNames, operation.Status);
                AddIfPresent(statusNames, operation.RequiredStatus);
                AddIfPresent(roleNames, operation.Condition?.Role);
                AddIfPresent(statusNames, operation.Condition?.Status);
                CollectEffectReferences(
                    operation.SharedEffect,
                    statusNames);
            }
        }

        // Ready is implemented by three character-specific resource assets.
        // The runtime operation resolves the selected user's resource, while
        // the generator still requires every concrete definition to exist.
        statusNames.Add("Ready");
        statusNames.Add("Ready_0");
        statusNames.Add("Ready_4");

        ReferenceCatalog catalog = new();
        foreach (string statusName in statusNames)
        {
            catalog.Statuses.Add(
                statusName,
                FindUniqueAsset<StatusEffectSO>(statusName));
        }
        foreach (string roleName in roleNames)
        {
            string assetName = roleName == "Vanguard"
                ? "RoleVangaurd"
                : $"Role{roleName}";
            catalog.Roles.Add(
                roleName,
                FindUniqueAsset<CharacterRoleSO>(assetName));
        }
        foreach (string characterName in characterNames)
        {
            catalog.Characters.Add(
                characterName,
                FindUniqueAsset<CharacterSO>($"2_{characterName}"));
        }

        return catalog;
    }

    private static void CollectFilterReferences(
        TargetFilterSpec filter,
        ISet<string> statusNames,
        ISet<string> roleNames,
        ISet<string> characterNames)
    {
        if (filter == null)
            return;
        AddIfPresent(roleNames, filter.RequiredRole);
        AddIfPresent(characterNames, filter.RequiredCharacter);
        AddIfPresent(statusNames, filter.RequiredStatus);
    }

    private static void CollectEffectReferences(
        EffectSpec effect,
        ISet<string> statusNames)
    {
        if (effect == null)
            return;
        AddIfPresent(statusNames, effect.Status);
        foreach (string status in effect.RemovalStatuses)
            AddIfPresent(statusNames, status);
    }

    private static void AddIfPresent(ISet<string> set, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            set.Add(value);
    }

    private static T FindUniqueAsset<T>(string expectedAssetName)
        where T : UnityEngine.Object
    {
        List<T> matches = new();
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && string.Equals(
                    asset.name,
                    expectedAssetName,
                    StringComparison.Ordinal))
            {
                matches.Add(asset);
            }
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {typeof(T).Name} named " +
                $"'{expectedAssetName}', found {matches.Count}. Existing " +
                "card assets were not touched.");
        }
        return matches[0];
    }

    private static void PopulateCard(
        BattleCardSO card,
        CardSpec spec,
        ReferenceCatalog references)
    {
        SerializedObject serialized = new(card);
        SetString(serialized, "cardId", spec.CardId);
        SetEnum(serialized, "rarity", spec.Rarity);
        SetInt(serialized, "sortOrder", spec.Number);
        SetString(serialized, "nameLocalizationKey", spec.NameKey);
        SetString(
            serialized,
            "descriptionLocalizationKey",
            spec.DescriptionKey);
        SetString(serialized, "fallbackName", spec.KoreanName);
        SetString(
            serialized,
            "fallbackDescription",
            spec.KoreanDescription);
        SetEnum(serialized, "affiliation", spec.Affiliation);
        SetObject(
            serialized,
            "ownerCharacter",
            references.GetCharacter(spec.OwnerCharacter));

        SerializedProperty requiredCharacters =
            RequireProperty(serialized, "requiredCharacters");
        requiredCharacters.arraySize = spec.RequiredCharacters.Count;
        for (int index = 0; index < spec.RequiredCharacters.Count; index++)
        {
            requiredCharacters.GetArrayElementAtIndex(index)
                .objectReferenceValue = references.GetCharacter(
                    spec.RequiredCharacters[index]);
        }

        SetEnum(serialized, "requirementMode", spec.RequirementMode);
        SetEnum(serialized, "sourcePolicy", spec.SourcePolicy);
        SetInt(serialized, "energyCost", spec.Cost);
        SetEnum(serialized, "recyclePolicy", spec.RecyclePolicy);
        SetBool(
            serialized,
            "availableAsStartingCard",
            spec.AvailableAsStartingCard);
        SetBool(serialized, "availableAsDungeonReward", true);
        SetInt(
            serialized,
            "minimumMaximumEnergy",
            Mathf.Max(1, spec.Cost));

        SetEnum(serialized, "targetFaction", spec.TargetFaction);
        SetEnum(serialized, "subject", spec.Subject);
        SetEnum(serialized, "subjectMetric", spec.SubjectMetric);
        SetInt(serialized, "targetCount", spec.TargetCount);
        PopulateArea(
            RequireProperty(serialized, "areaDefinition"),
            spec.Area);
        PopulateFilter(
            RequireProperty(serialized, "primaryTargetFilter"),
            spec.PrimaryFilter,
            references);
        PopulateSecondary(
            RequireProperty(serialized, "secondaryTarget"),
            spec.Secondary,
            references);

        SerializedProperty effects =
            RequireProperty(serialized, "abilityEffects");
        effects.arraySize = spec.Effects.Count;
        for (int index = 0; index < spec.Effects.Count; index++)
        {
            PopulateEffect(
                effects.GetArrayElementAtIndex(index),
                spec.Effects[index],
                references,
                spec.TargetFaction,
                spec.Area);
        }

        SerializedProperty operations =
            RequireProperty(serialized, "operations");
        operations.arraySize = spec.Operations.Count;
        for (int index = 0; index < spec.Operations.Count; index++)
        {
            PopulateOperation(
                operations.GetArrayElementAtIndex(index),
                spec.Operations[index],
                references,
                spec.TargetFaction,
                spec.Area);
        }

        if (!serialized.ApplyModifiedPropertiesWithoutUndo())
        {
            throw new InvalidOperationException(
                $"No serialized values were applied to card {spec.Number}.");
        }
        EditorUtility.SetDirty(card);
    }

    private static void PopulateArea(
        SerializedProperty property,
        AreaSpec area)
    {
        area ??= new AreaSpec();
        SetEnum(
            property,
            "shapeType",
            area.IsWorldArea
                ? CharacterAreaShapeType.CircleSector
                : CharacterAreaShapeType.Target);
        SetEnum(
            property,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        SetFloat(property, "radius", Mathf.Max(0.1f, area.Radius));
        SetFloat(property, "angle", 360f);
        SetFloat(property, "maxCastDistance", 4.25f);
    }

    private static void PopulateFilter(
        SerializedProperty property,
        TargetFilterSpec filter,
        ReferenceCatalog references)
    {
        filter ??= new TargetFilterSpec();
        SetObject(
            property,
            "requiredRole",
            references.GetRole(filter.RequiredRole));
        SetObject(
            property,
            "requiredCharacter",
            references.GetCharacter(filter.RequiredCharacter));
        SetObject(
            property,
            "requiredStatus",
            references.GetStatus(filter.RequiredStatus));
        SetBool(property, "includeDefeated", filter.IncludeDefeated);
    }

    private static void PopulateSecondary(
        SerializedProperty property,
        SecondaryTargetSpec secondary,
        ReferenceCatalog references)
    {
        secondary ??= new SecondaryTargetSpec();
        SetBool(property, "enabled", secondary.Enabled);
        SetBool(property, "worldPoint", secondary.WorldPoint);
        SetEnum(property, "targetFaction", secondary.TargetFaction);
        SetEnum(property, "subject", secondary.Subject);
        SetEnum(property, "subjectMetric", secondary.SubjectMetric);
        SetInt(property, "targetCount", secondary.TargetCount);
        PopulateArea(
            RequireRelative(property, "areaDefinition"),
            secondary.Area);
        PopulateFilter(
            RequireRelative(property, "filter"),
            secondary.Filter,
            references);
    }

    private static void PopulateEffect(
        SerializedProperty property,
        EffectSpec effect,
        ReferenceCatalog references,
        CharacterTargetFaction fallbackFaction,
        AreaSpec fallbackArea)
    {
        SetString(property, "effectId", effect.Id ?? string.Empty);
        SetEnum(property, "type", effect.Type);
        SetEnum(
            property,
            "targetMode",
            CharacterEffectTargetMode.InheritAction);
        SetEnum(
            property,
            "preconditionFailurePolicy",
            CharacterEffectPreconditionFailurePolicy.AbortAction);
        SetEnum(
            property,
            "failurePolicy",
            CharacterEffectFailurePolicy.Continue);

        SerializedProperty selector =
            RequireRelative(property, "targetSelector");
        CharacterTargetFaction faction =
            effect.Type == CharacterEffectType.Heal ||
            effect.Type == CharacterEffectType.Shield
                ? CharacterTargetFaction.Ally
                : fallbackFaction;
        SetEnum(selector, "targetFaction", faction);
        SetEnum(selector, "subject", CharacterAttackSubject.Random);
        SetEnum(
            selector,
            "subjectMetric",
            CharacterAttackSubjectMetric.Health);
        SetInt(selector, "subjectCount", 1);
        SetEnum(
            selector,
            "conditionMatchMode",
            CharacterConditionMatchMode.Any);
        RequireRelative(selector, "numericConditions").arraySize = 0;
        RequireRelative(selector, "areaOffsets").arraySize = 0;
        PopulateArea(
            RequireRelative(selector, "areaDefinition"),
            fallbackArea);

        SetEnum(property, "damageType", effect.DamageType);
        SetEnum(
            property,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetFloat(property, "damageAmount", effect.Amount);
        SetFloat(property, "sourceResourceScale", 0f);
        SetFloat(property, "sourceCurrentHealthScale", 0f);
        SetFloat(property, "sourceMaxHealthScale", 0f);
        SetFloat(property, "targetCurrentHealthScale", 0f);
        SetFloat(property, "targetMaxHealthScale", 0f);
        SetObject(property, "sourceStatusScalingEffect", null);
        SetFloat(property, "sourceStatusStacksScale", 0f);
        SetObject(property, "targetStatusScalingEffect", null);
        SetFloat(property, "targetStatusStacksScale", 0f);
        RequireRelative(property, "statusContributionMultipliers")
            .arraySize = 0;

        StatusEffectSO appliedStatus = references.GetStatus(effect.Status);
        float statusDuration = effect.StatusDuration;
        if (statusDuration <= 0f && appliedStatus != null)
        {
            statusDuration = appliedStatus.DurationMode ==
                             StatusEffectDurationMode.Timed
                ? appliedStatus.ConfiguredDefaultDuration
                : 1f;
        }
        SetFloat(property, "statusDuration", Mathf.Max(0.1f, statusDuration));
        SetFloat(property, "statusStacks", Mathf.Max(0.1f, effect.StatusStacks));
        SetObject(property, "statusEffect", appliedStatus);

        SerializedProperty removalEffects =
            RequireRelative(property, "statusRemovalEffects");
        removalEffects.arraySize = effect.RemovalStatuses.Count;
        for (int index = 0; index < effect.RemovalStatuses.Count; index++)
        {
            removalEffects.GetArrayElementAtIndex(index)
                .objectReferenceValue = references.GetStatus(
                    effect.RemovalStatuses[index]);
        }
        SetEnum(property, "statusRemovalTarget", effect.RemovalTarget);
        SetEnum(property, "statusRemovalPickMode", effect.RemovalPickMode);
        SetInt(
            property,
            "statusRemovalPickCount",
            Mathf.Max(1, effect.RemovalPickCount));
        SetEnum(
            property,
            "statusRemovalAmountMode",
            CharacterStatusRemovalAmountMode.FixedStacks);
        SetInt(
            property,
            "statusRemovalCount",
            Mathf.Max(0, effect.RemovalStackCount));
        SetFloat(property, "statusRemovalRatio", 0.5f);
    }

    private static void PopulateOperation(
        SerializedProperty property,
        OperationSpec operation,
        ReferenceCatalog references,
        CharacterTargetFaction fallbackFaction,
        AreaSpec fallbackArea)
    {
        SetString(property, "operationId", operation.Id);
        SetEnum(property, "type", operation.Type);
        SetEnum(property, "targetScope", operation.Scope);
        if (operation.SharedEffect != null)
        {
            PopulateEffect(
                RequireRelative(property, "sharedEffect"),
                operation.SharedEffect,
                references,
                fallbackFaction,
                fallbackArea);
        }
        PopulateCondition(
            RequireRelative(property, "condition"),
            operation.Condition,
            references);
        SetObject(
            property,
            "requiredRole",
            references.GetRole(operation.RequiredRole));
        SetObject(
            property,
            "requiredCharacter",
            references.GetCharacter(operation.RequiredCharacter));
        SetObject(
            property,
            "statusEffect",
            references.GetStatus(operation.Status));
        SetObject(
            property,
            "requiredStatus",
            references.GetStatus(operation.RequiredStatus));
        SetInt(property, "amount", operation.Amount);
        SetFloat(property, "ratio", operation.Ratio);
        SetInt(property, "count", operation.Count);
        SetInt(
            property,
            "minimumSelectionCount",
            operation.MinimumSelectionCount);
        SetInt(
            property,
            "maximumSelectionCount",
            operation.MaximumSelectionCount);
        SetFloat(property, "duration", operation.Duration);
        SetFloat(property, "delaySeconds", operation.DelaySeconds);
        SetFloat(property, "radius", operation.Radius);
        SetFloat(property, "statusDuration", operation.StatusDuration);
        SetFloat(property, "statusStacks", operation.StatusStacks);
        SetBool(
            property,
            "usePreviousChangedCount",
            operation.UsePreviousChangedCount);
        SetBool(property, "oncePerTarget", operation.OncePerTarget);
        SetEnum(property, "movementMode", operation.MovementMode);
        SetEnum(property, "zoneTrigger", operation.ZoneTrigger);
        SetEnum(property, "costModifierMode", operation.CostModifierMode);
        SetEnum(property, "spatialZone", operation.SpatialZone);
    }

    private static void PopulateCondition(
        SerializedProperty property,
        ConditionSpec condition,
        ReferenceCatalog references)
    {
        condition ??= new ConditionSpec();
        SetEnum(property, "type", condition.Type);
        SetEnum(property, "comparison", condition.Comparison);
        SetFloat(property, "threshold", condition.Threshold);
        SetObject(
            property,
            "role",
            references.GetRole(condition.Role));
        SetObject(
            property,
            "statusEffect",
            references.GetStatus(condition.Status));
        SetEnum(property, "zone", condition.Zone);
    }

    private static void ValidateStagedAssets(
        IReadOnlyList<CardSpec> specs,
        IReadOnlyList<string> paths)
    {
        if (paths.Count != 100)
            throw new InvalidOperationException(
                $"Expected 100 staged asset paths, got {paths.Count}.");

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < paths.Count; index++)
        {
            BattleCardSO card =
                AssetDatabase.LoadAssetAtPath<BattleCardSO>(paths[index]);
            if (card == null)
                throw new InvalidOperationException(
                    $"Staged card {index + 1} could not be loaded.");

            CardSpec spec = specs[index];
            if (card.CardId != spec.CardId ||
                card.SortOrder != spec.Number ||
                card.Rarity != spec.Rarity ||
                card.EnergyCost != spec.Cost ||
                card.RecyclePolicy != spec.RecyclePolicy ||
                card.AvailableAsStartingCard !=
                    spec.AvailableAsStartingCard ||
                !card.AvailableAsDungeonReward ||
                card.MinimumMaximumEnergy != Mathf.Max(1, spec.Cost) ||
                card.AbilityEffects.Count != spec.Effects.Count ||
                card.Operations.Count != spec.Operations.Count ||
                !ids.Add(card.CardId))
            {
                throw new InvalidOperationException(
                    $"Staged card {spec.Number} does not match its source " +
                    "specification.");
            }

            if (!BattleCardDefinitionValidator.TryValidate(
                    card,
                    out string error))
            {
                throw new InvalidOperationException(
                    $"Staged card {spec.Number} failed runtime " +
                    $"validation: {error}");
            }
        }
    }

    private static void CommitStagedCatalog(
        IReadOnlyList<CardSpec> specs,
        IReadOnlyList<string> stagingPaths)
    {
        List<string> existingCardPaths = FindDirectCardAssets(CardsFolder);
        HashSet<string> existingCards =
            existingCardPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (CardSpec spec in specs)
        {
            string destination = $"{CardsFolder}/{spec.FileName}";
            UnityEngine.Object occupied =
                AssetDatabase.LoadMainAssetAtPath(destination);
            if (occupied != null && !existingCards.Contains(destination))
            {
                throw new InvalidOperationException(
                    $"Cannot replace catalog: '{destination}' is occupied " +
                    "by a non-BattleCardSO asset. Existing cards were not " +
                    "touched.");
            }
        }

        EnsureFolder(BackupFolder);
        List<MoveRecord> oldMoves = new(existingCardPaths.Count);
        List<MoveRecord> newMoves = new(stagingPaths.Count);
        try
        {
            foreach (string oldPath in existingCardPaths)
            {
                string backupPath =
                    $"{BackupFolder}/{Path.GetFileName(oldPath)}";
                MoveAssetChecked(oldPath, backupPath);
                oldMoves.Add(new MoveRecord(oldPath, backupPath));
            }

            for (int index = 0; index < stagingPaths.Count; index++)
            {
                string destination =
                    $"{CardsFolder}/{specs[index].FileName}";
                MoveAssetChecked(stagingPaths[index], destination);
                newMoves.Add(new MoveRecord(stagingPaths[index], destination));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateStagedAssets(
                specs,
                specs.Select(s => $"{CardsFolder}/{s.FileName}").ToArray());

            if (AssetDatabase.IsValidFolder(StagingFolder) &&
                !AssetDatabase.DeleteAsset(StagingFolder))
            {
                Debug.LogWarning(
                    $"The empty staging folder '{StagingFolder}' could not " +
                    "be removed. The live catalog is valid.");
            }
            if (!AssetDatabase.DeleteAsset(BackupFolder))
                throw new InvalidOperationException(
                    "The verified old-card backup folder could not be " +
                    "removed.");
        }
        catch (Exception commitException)
        {
            Exception rollbackException = null;
            try
            {
                EnsureFolder(StagingFolder);
                for (int index = newMoves.Count - 1; index >= 0; index--)
                {
                    MoveRecord move = newMoves[index];
                    if (AssetDatabase.LoadMainAssetAtPath(move.To) != null)
                        MoveAssetChecked(move.To, move.From);
                }
                for (int index = oldMoves.Count - 1; index >= 0; index--)
                {
                    MoveRecord move = oldMoves[index];
                    if (AssetDatabase.LoadMainAssetAtPath(move.To) != null)
                        MoveAssetChecked(move.To, move.From);
                }
                if (AssetDatabase.IsValidFolder(BackupFolder))
                    AssetDatabase.DeleteAsset(BackupFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                rollbackException = exception;
            }

            if (rollbackException != null)
            {
                throw new AggregateException(
                    "Catalog commit failed and rollback also failed. The " +
                    $"backup folder is '{BackupFolder}'.",
                    commitException,
                    rollbackException);
            }
            throw;
        }
    }

    private static List<string> FindDirectCardAssets(string folder)
    {
        List<string> paths = new();
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:BattleCardSO",
                     new[] { folder }))
        {
            string path = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
            string parent = NormalizePath(Path.GetDirectoryName(path));
            if (string.Equals(
                    parent,
                    folder,
                    StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }
        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static void MoveAssetChecked(string from, string to)
    {
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(
                $"Could not move '{from}' to '{to}': {error}");
    }

    private static void EnsureFolder(string folder)
    {
        folder = NormalizePath(folder);
        if (AssetDatabase.IsValidFolder(folder))
            return;

        int slash = folder.LastIndexOf('/');
        if (slash <= 0)
            throw new InvalidOperationException(
                $"Cannot create invalid asset folder '{folder}'.");

        string parent = folder.Substring(0, slash);
        string child = folder.Substring(slash + 1);
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, child);
        if (string.IsNullOrEmpty(guid) ||
            !AssetDatabase.IsValidFolder(folder))
        {
            throw new InvalidOperationException(
                $"Could not create asset folder '{folder}'.");
        }
    }

    private static void DeleteScratchFolderIfPresent(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;
        if (!AssetDatabase.DeleteAsset(folder))
            throw new InvalidOperationException(
                $"Could not clear scratch folder '{folder}'.");
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            throw new MissingFieldException(
                serialized.targetObject.GetType().Name,
                name);
        return property;
    }

    private static SerializedProperty RequireRelative(
        SerializedProperty parent,
        string name)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new MissingFieldException(parent.propertyPath, name);
        return property;
    }

    private static void SetString(
        SerializedObject serialized,
        string name,
        string value)
    {
        RequireProperty(serialized, name).stringValue = value ?? string.Empty;
    }

    private static void SetString(
        SerializedProperty parent,
        string name,
        string value)
    {
        RequireRelative(parent, name).stringValue = value ?? string.Empty;
    }

    private static void SetInt(
        SerializedObject serialized,
        string name,
        int value)
    {
        RequireProperty(serialized, name).intValue = value;
    }

    private static void SetInt(
        SerializedProperty parent,
        string name,
        int value)
    {
        RequireRelative(parent, name).intValue = value;
    }

    private static void SetFloat(
        SerializedProperty parent,
        string name,
        float value)
    {
        RequireRelative(parent, name).floatValue = value;
    }

    private static void SetBool(
        SerializedObject serialized,
        string name,
        bool value)
    {
        RequireProperty(serialized, name).boolValue = value;
    }

    private static void SetBool(
        SerializedProperty parent,
        string name,
        bool value)
    {
        RequireRelative(parent, name).boolValue = value;
    }

    private static void SetEnum(
        SerializedObject serialized,
        string name,
        Enum value)
    {
        RequireProperty(serialized, name).intValue = Convert.ToInt32(value);
    }

    private static void SetEnum(
        SerializedProperty parent,
        string name,
        Enum value)
    {
        RequireRelative(parent, name).intValue = Convert.ToInt32(value);
    }

    private static void SetObject(
        SerializedObject serialized,
        string name,
        UnityEngine.Object value)
    {
        RequireProperty(serialized, name).objectReferenceValue = value;
    }

    private static void SetObject(
        SerializedProperty parent,
        string name,
        UnityEngine.Object value)
    {
        RequireRelative(parent, name).objectReferenceValue = value;
    }

    private sealed class CardSpec
    {
        public int Number;
        public string KoreanName;
        public string KoreanDescription;
        public string Slug;
        public int Cost;
        public ItemRarity Rarity;
        public BattleCardRecyclePolicy RecyclePolicy;
        public bool AvailableAsStartingCard;
        public BattleCardAffiliation Affiliation;
        public string OwnerCharacter;
        public readonly List<string> RequiredCharacters = new();
        public BattleCardRequirementMatchMode RequirementMode;
        public BattleCardSourcePolicy SourcePolicy;
        public CharacterTargetFaction TargetFaction;
        public CharacterAttackSubject Subject = CharacterAttackSubject.Manual;
        public CharacterAttackSubjectMetric SubjectMetric = default;
        public int TargetCount = 1;
        public AreaSpec Area = new();
        public TargetFilterSpec PrimaryFilter = new();
        public SecondaryTargetSpec Secondary = new();
        public readonly List<EffectSpec> Effects = new();
        public readonly List<OperationSpec> Operations = new();

        public string CardId => $"card.catalog.c{Number:000}";
        public string NameKey => $"card.public.catalog.c{Number:000}.name";
        public string DescriptionKey =>
            $"card.public.catalog.c{Number:000}.description";
        public string FileName => $"Card{Number:000}_{Slug}.asset";
    }

    private sealed class AreaSpec
    {
        public bool IsWorldArea;
        public float Radius = 1.5f;
    }

    private sealed class TargetFilterSpec
    {
        public string RequiredRole;
        public string RequiredCharacter = null;
        public string RequiredStatus;
        public bool IncludeDefeated;
    }

    private sealed class SecondaryTargetSpec
    {
        public bool Enabled;
        public bool WorldPoint;
        public CharacterTargetFaction TargetFaction;
        public CharacterAttackSubject Subject = CharacterAttackSubject.Manual;
        public CharacterAttackSubjectMetric SubjectMetric = default;
        public int TargetCount = 1;
        public AreaSpec Area = new();
        public TargetFilterSpec Filter = new();
    }

    private sealed class EffectSpec
    {
        public string Id;
        public CharacterEffectType Type;
        public CharacterAttackDamageType DamageType =
            CharacterAttackDamageType.Physical;
        public float Amount;
        public string Status;
        public float StatusDuration;
        public float StatusStacks = 1f;
        public CharacterStatusRemovalTarget RemovalTarget =
            CharacterStatusRemovalTarget.Single;
        public CharacterStatusRemovalPickMode RemovalPickMode =
            CharacterStatusRemovalPickMode.AllMatches;
        public int RemovalPickCount = 1;
        public int RemovalStackCount;
        public readonly List<string> RemovalStatuses = new();
    }

    private sealed class ConditionSpec
    {
        public BattleCardConditionType Type;
        public CharacterNumericComparison Comparison =
            CharacterNumericComparison.GreaterThanOrEqual;
        public float Threshold;
        public string Role;
        public string Status;
        public BattleCardSpatialZone Zone;
    }

    private sealed class OperationSpec
    {
        public string Id;
        public BattleCardOperationType Type;
        public BattleCardTargetScope Scope;
        public EffectSpec SharedEffect;
        public ConditionSpec Condition;
        public string RequiredRole;
        public string RequiredCharacter;
        public string Status;
        public string RequiredStatus;
        public int Amount;
        public float Ratio = 1f;
        public int Count = 1;
        public int MinimumSelectionCount = 1;
        public int MaximumSelectionCount = 1;
        public float Duration;
        public float DelaySeconds;
        public float Radius = 1.5f;
        public float StatusDuration = 1f;
        public float StatusStacks = 1f;
        public bool UsePreviousChangedCount;
        public bool OncePerTarget = true;
        public BattleCardMovementMode MovementMode;
        public BattleCardZoneTrigger ZoneTrigger;
        public BattleCardCostModifierMode CostModifierMode;
        public BattleCardSpatialZone SpatialZone = default;
    }

    private sealed class ReferenceCatalog
    {
        public readonly Dictionary<string, StatusEffectSO> Statuses =
            new(StringComparer.Ordinal);
        public readonly Dictionary<string, CharacterRoleSO> Roles =
            new(StringComparer.Ordinal);
        public readonly Dictionary<string, CharacterSO> Characters =
            new(StringComparer.Ordinal);

        public StatusEffectSO GetStatus(string name)
        {
            return GetOptional(Statuses, name, "status");
        }

        public CharacterRoleSO GetRole(string name)
        {
            return GetOptional(Roles, name, "role");
        }

        public CharacterSO GetCharacter(string name)
        {
            return GetOptional(Characters, name, "character");
        }

        private static T GetOptional<T>(
            IReadOnlyDictionary<string, T> dictionary,
            string name,
            string label)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            if (dictionary.TryGetValue(name, out T value) && value != null)
                return value;
            throw new KeyNotFoundException(
                $"The resolved {label} reference '{name}' is missing.");
        }
    }

    private readonly struct MoveRecord
    {
        public string From { get; }
        public string To { get; }

        public MoveRecord(string from, string to)
        {
            From = from;
            To = to;
        }
    }
}
#endif
