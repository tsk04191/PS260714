#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the 46-enemy public roster from GAME_ENEMY_ROSTER.json v2.0.0.
/// The uploaded JSON is source data, not an instruction stream. The catalog
/// below is deliberately normalized and embedded so generation is repeatable
/// without depending on a file in a user's Downloads folder.
/// </summary>
public static class EnemyRosterCatalogGenerator
{
    public const int ExpectedEnemyCount = 46;
    public const int ExpectedGeneralCount = 30;
    public const int ExpectedSpecialCount = 10;
    public const int ExpectedEliteCount = 5;
    public const int ExpectedBossCount = 1;
    public const int SimultaneousSummonCap = 12;

    private const string MenuPath =
        "Tools/PS260714/Enemies/Rebuild Public 46-Enemy Roster";
    private const string EnemyFolder =
        "Assets/06_Runtime/Resources/Enemies";
    private const string StagingFolder =
        "Assets/06_Runtime/Resources/Enemies.__RosterStaging";
    private const string BackupFolder =
        "Assets/06_Runtime/Resources/Enemies.__RosterBackup";
    private const string YamlStagingFolder =
        "Temp/EnemyRosterYamlStaging";
    private const string YamlBackupFolder =
        "Temp/EnemyRosterYamlBackup";
    private const string EnemyScriptGuid =
        "8bd5b29c734a4dc78153b50872da6d2e";
    private const string StunStatusGuid =
        "d55e7fb1b3874a62b66e9ad68b3cb8fe";
    private static readonly UTF8Encoding Utf8WithoutBom =
        new(false);

    // id | ko | en | tier | legacy archetype | role | ability type |
    // hp x | core damage x | period x | speed x | unlock | authored cap |
    // priority | Korean ability text | English ability text |
    // typed parameters | counter tags | assumption IDs
    private static readonly string[] RawEnemies =
    {
        "G001|기본 잔재|Basic Remnant|General|Basic|baseline|none|1|1|1|1|0|-1|P0|추가 기믹이 없는 기준 적|A baseline enemy with no additional mechanic.||singleTarget,basicDamage|A02,A03,A04,A12",
        "G002|돌입 잔재|Assault Remnant|General|Assault|arrival_pressure|first_core_hit_bonus|0.8|1|0.9|1.1|10|-1|P0|첫 방어막 공격의 피해량이 25% 증가한다.|Its first attack against the core deals 25% more damage.|i:firstCoreHitDamagePercent=25|singleTarget,stun,burstDamage|A01,A02,A03,A04,A12",
        "G003|중장 잔재|Heavy Remnant|General|Heavy|durable_threat|stagger_resistance|2.2|1.5|1.8|0.6|20|-1|P0|기절과 행동 방해의 지속시간이 50% 감소한다.|Stun and control durations applied to it are reduced by 50%.|i:selfControlResistancePercent=50|armorBreak,singleTarget,debuff|A01,A02,A03,A04,A12",
        "G004|충각 잔재|Rammer Remnant|General|Assault|telegraphed_burst|charge_core_strike|0.9|1.2|1|1|15|-1|P0|1.2초 충전 후 다음 방어막 공격이 80% 증가한다. 충전 중 기절이나 지정된 방해 효과를 받으면 취소된다.|After charging for 1.2 seconds, its next core attack deals 80% more damage. Stun or forced control interrupts the charge.|f:chargeDurationSec=1.2;f:damageMultiplier=1.8;f:cooldownSec=8|interrupt,stun,burstDamage|A01,A02,A03,A04,A12",
        "G005|돌파 잔재|Breacher Remnant|General|Infiltrator|shield_piercer|first_hit_guard_pierce|1.1|1|1.2|0.9|25|-1|P1|첫 공격은 방어막 피해 경감 효과를 20% 무시한다.|Its first core attack ignores 20% of core damage mitigation.|i:guardPiercePercent=20|singleTarget,shield,burstDamage|A02,A03,A04,A12,A16",
        "G006|포식 잔재|Predator Remnant|General|Assault|enrage|health_threshold_enrage|1|1|1|0.95|25|-1|P0|체력 50% 이하에서 방어막 공격력이 25% 증가한다.|At or below 50% health, its core attack damage increases by 25%.|i:thresholdPercent=50;f:coreAttackMultiplier=1.25|burstDamage,focus,singleTarget|A02,A03,A04,A12",
        "G007|분열 잔재|Splitter Remnant|General|Basic|death_spawn|split_on_death|0.85|0.9|1|1|20|-1|P0|처치 시 소형 잔재 2개를 생성한다. 분열체는 다시 분열하지 않는다.|On death, it creates two small remnants that cannot split again.|i:spawnCount=2;f:childHpMultiplier=0.35;f:childCoreAttackMultiplier=0.35;e:childEnemy=G001|aoe,deathTrigger,focus|A01,A02,A03,A04,A06,A12",
        "G008|군집 잔재|Swarm Remnant|General|Basic|quantity_pressure|group_spawn|0.35|0.35|1|1.05|0|-1|P0|한 번의 생성 이벤트로 총 3~5개의 군집 잔재가 등장한다.|A single spawn event produces a group of three to five swarm remnants.|i:spawnCountMin=3;i:spawnCountMax=5;e:spawnEnemy=G008|aoe,pierce,quantity|A01,A02,A03,A04,A06,A12",
        "G009|장갑 잔재|Armored Remnant|General|Heavy|damage_gate|periodic_damage_reduction|1.4|1|1.1|0.8|25|-1|P1|3초마다 받는 첫 직접 피해를 35% 감소시킨다.|Every three seconds, the first direct hit against it is reduced by 35%.|f:cooldownSec=3;i:damageReductionPercent=35|singleTarget,damageTiming,debuff|A02,A03,A04,A12",
        "G010|재생 잔재|Regenerator Remnant|General|Medic|sustain|out_of_combat_regeneration|1.3|0.9|1|0.9|30|-1|P0|3초 동안 피해를 받지 않으면 2초마다 최대 체력의 3%를 회복한다.|After taking no damage for three seconds, it heals 3% of maximum health every two seconds.|f:noDamageDurationSec=3;f:tickSec=2;i:healPercentMaxHp=3|dot,focus,damageOverTime|A01,A02,A03,A04,A12",
        "G011|방화 잔재|Burner Remnant|General|Assault|core_dot|core_hit_burn|1|1|1|0.9|30|-1|P0|방어막에 4초 화상 1중첩을 부여한다. 최대 3중첩이다.|Core hits apply one four-second burn stack, up to three stacks.|f:durationSec=4;i:maxStacks=3;f:tickSec=1;i:damagePerStack=1|purge,shieldRepair,focus|A02,A03,A04,A07,A12",
        "G012|출혈 잔재|Bleeder Remnant|General|Assault|stacking_core_damage|repeated_core_hit_stack|1|1|1|0.95|35|-1|P1|방어막 공격마다 중첩을 얻고 중첩당 이후 피해가 10% 증가한다. 기절되면 1중첩이 감소한다.|Each core hit grants a stack that increases later core damage by 10%. Stun removes one stack.|i:damagePerStackPercent=10;i:maxStacks=3;i:stacksRemovedOnStun=1|stun,interrupt,focus|A02,A03,A04,A12",
        "G013|독성 잔재|Toxic Remnant|General|Infiltrator|death_zone|death_zone|0.85|1|1|0.95|35|-1|P1|처치되면 4초 동안 방어막 회복량을 25% 감소시키는 독성 지대를 남긴다.|On death, it leaves a four-second toxic zone that reduces core recovery by 25%.|f:durationSec=4;f:shieldRecoveryMultiplier=0.75;f:zoneRadius=2.5|shieldRepair,positioning,deathTrigger|A01,A02,A03,A04,A08,A12",
        "G014|파동 잔재|Pulsar Remnant|General|Pointman|ally_accelerator|enemy_attack_aura|1.1|0.8|1|0.85|40|-1|P1|반경 3 안의 적 방어막 공격 주기를 15% 단축한다.|Enemy core attack intervals within radius 3 are shortened by 15%.|f:enemyAttackPeriodMultiplier=0.85;f:auraRange=3|focus,aoe,priorityTarget|A01,A02,A03,A04,A05,A12",
        "G015|침식 잔재|Eroder Remnant|General|Heavy|shield_max_reduction|temporary_shield_max_reduction|1.2|1|1.15|0.8|45|-1|P1|방어막 공격이 최대치를 5% 감소시킨다. 최대 2중첩이며 6초 후 하나씩 사라진다.|Core hits reduce maximum core health by 5%, up to two stacks, with each stack expiring after six seconds.|i:maxShieldReductionPercent=5;i:maxStacks=2;f:durationSec=6|focus,shieldRepair,purge|A02,A03,A04,A12",
        "G016|흡수 잔재|Absorber Remnant|General|Medic|death_consumer|ally_death_absorption|1.3|1.1|1.2|0.8|45|-1|P1|반경 2.5 안의 적이 죽으면 최대 체력 10%의 보호막과 체력 5%를 얻는다. 최대 3회다.|When a nearby enemy dies, it gains a shield equal to 10% of maximum health and heals 5%, up to three times.|i:shieldPercentMaxHp=10;i:healPercentMaxHp=5;i:maxTriggers=3;f:absorptionRange=2.5|focus,singleTarget,deathTrigger|A01,A02,A03,A04,A12",
        "G017|닻 잔재|Anchor Remnant|General|Heavy|crowd_control_resist|control_resistance_aura|1.5|1|1.2|0.75|50|-1|P1|자신의 기절 지속시간은 50%, 반경 2.5 안 아군은 25% 감소한다.|Its stun duration is reduced by 50%; nearby allies receive a 25% reduction.|i:selfStunResistancePercent=50;i:allyStunResistancePercent=25;f:auraRange=2.5|focus,purge,burstDamage|A02,A03,A04,A05,A12",
        "G018|결속 잔재|Binder Remnant|General|ShieldBearer|damage_link|two_unit_damage_share|0.9|0.9|1|0.95|50|-1|P2|가장 가까운 적 하나와 연결되어 받은 피해의 20%를 공유한다.|It links to the nearest enemy and shares 20% of incoming damage.|i:sharedDamagePercent=20;i:maxLinks=1;f:linkRange=3|aoe,purge,damageLink|A01,A02,A03,A04,A08,A12",
        "G019|미끼 잔재|Decoy Remnant|General|Infiltrator|target_disruption|target_decoy|0.75|0.8|1|1|55|-1|P2|10초마다 3초 동안 자동 공격 우선순위를 자신에게 유도한다.|Every ten seconds, it draws automatic target priority to itself for three seconds.|f:cooldownSec=10;f:durationSec=3|targeting,aoe,focus|A02,A03,A04,A12,A19",
        "G020|흡혈 잔재|Siphon Remnant|General|Medic|core_damage_conversion|core_damage_to_heal|1.2|1|1.2|0.85|55|-1|P1|방어막에 실제로 준 피해의 25%를 체력으로 회복한다.|It heals for 25% of the damage actually dealt to the core.|i:healFromCoreDamagePercent=25|focus,burstDamage,shieldRepair|A02,A03,A04,A12",
        "G021|분쇄 잔재|Crusher Remnant|General|Heavy|charged_heavy_hit|repeating_charged_attack|1.1|1.4|2.3|0.75|60|-1|P0|3초 충전 후 방어막에 2.2배 피해를 준다. 충전 중에는 공격하지 않는다.|After charging for three seconds, it deals 2.2 times core damage and does not attack while charging.|f:chargeDurationSec=3;f:damageMultiplier=2.2;f:cooldownSec=10|interrupt,stun,burstDamage|A02,A03,A04,A12",
        "G022|잔향 잔재|Echo Remnant|General|Mechanic|ability_replay|death_ability_replay|0.9|0.9|1|0.9|60|-1|P2|처치 시 반경 3 안 일반 적의 마지막 능력을 50% 효과로 한 번 재생한다.|On death, it replays one nearby non-elite ability at 50% effectiveness.|i:replayEffectPercent=50;i:maxReplays=1;f:replayRange=3|deathTrigger,timing,focus|A01,A02,A03,A04,A08,A12",
        "G023|운반 잔재|Carrier Remnant|General|Basic|contact_spawn|one_time_spawn|1.1|0.8|1.2|0.8|65|-1|P1|체력 40% 이하 또는 방어선 첫 도착 시 전투당 한 번 기본 잔재 2개를 생성한다.|Once per battle, at 40% health or first core contact, it summons two basic remnants.|i:triggerHpPercent=40;i:spawnCount=2;i:maxTriggers=1;e:spawnEnemy=G001;t:sharedTriggerGroup=carrier_once|focus,aoe,spawn|A01,A02,A03,A04,A06,A12",
        "G024|방해 잔재|Disruptor Remnant|General|Mechanic|energy_disruption|energy_regeneration_debuff|1|0.9|1|0.9|65|-1|P1|활성 중 플레이어의 에너지 회복량을 20% 감소시킨다.|While active, it reduces player energy recovery by 20%.|f:energyRegenMultiplier=0.8;f:auraRange=3|focus,energy,aoe|A01,A02,A03,A04,A05,A12",
        "G025|반사 잔재|Reflector Remnant|General|ShieldBearer|damage_reflection|next_hit_reflect|1.1|1|1|0.85|70|-1|P1|8초마다 다음 직접 피해의 35%를 공격자에게 반사한다.|Every eight seconds, it reflects 35% of the next direct hit to its source.|f:cooldownSec=8;i:reflectedDamagePercent=35|damageTiming,singleTarget,telegraph|A02,A03,A04,A12",
        "G026|분광 잔재|Prism Remnant|General|Infiltrator|repetition_resistance|repeated_source_resistance|1|1|1|0.85|70|-1|P2|2초 안에 같은 공격원으로 반복 피격되면 그 공격원의 피해를 25% 감소시킨다.|Repeated damage from the same source within two seconds is reduced by 25%; changing source resets the resistance.|f:windowSec=2;i:damageReductionPercent=25|deckRotation,singleTarget,timing|A02,A03,A04,A12,A20",
        "G027|포자 잔재|Spore Remnant|General|Basic|delayed_spawn|delayed_egg_spawn|0.9|0.8|1.1|0.9|75|-1|P2|처치 시 6초 뒤 기본 잔재 2개를 만드는 파괴 가능한 알을 남긴다.|On death, it leaves a destructible egg that summons two basic remnants after six seconds.|f:eggLifetimeSec=6;i:spawnCount=2;b:eggCanBeDestroyed=true;e:spawnEnemy=G001|aoe,focus,spawn|A01,A02,A03,A04,A06,A12",
        "G028|냉각 잔재|Chiller Remnant|General|Mechanic|action_slow|player_action_slow|1|0.8|1|0.85|75|-1|P1|반경 3 안에서 플레이어 자동 행동 주기를 15% 증가시킨다.|It increases designated player automatic action periods by 15% within radius 3.|f:playerActionPeriodMultiplier=1.15;f:auraRange=3|focus,actionSpeed,aoe|A01,A02,A03,A04,A05,A12",
        "G029|전환 잔재|Switchback Remnant|General|Heavy|stance_change|health_threshold_stance|1|1|1|0.9|80|-1|P1|체력 50% 이하에서 받는 피해가 20% 감소하고 공격 주기가 20% 단축된다.|At or below 50% health, incoming damage is reduced by 20% and its core attack interval is shortened by 20%.|i:thresholdPercent=50;i:damageReductionPercent=20;f:attackPeriodMultiplier=0.8|burstDamage,debuff,stun|A02,A03,A04,A11,A12",
        "G030|공명 잔재|Resonant Remnant|General|Pointman|same_tag_support|same_tag_aura|1.1|1|1|0.85|85|-1|P2|반경 2.5 안에서 잔재 태그를 공유하는 적의 공격력이 10% 증가하고 주기가 10% 단축된다.|Nearby enemies sharing the remnant tag gain 10% core damage and 10% shorter attack intervals.|f:coreAttackMultiplier=1.1;f:attackPeriodMultiplier=0.9;f:auraRange=2.5;t:requiredRoleTag=remnant|focus,targeting,aoe|A02,A03,A04,A05,A12,A18",
        "S001|치유 담당|Medic Remnant|Special|Medic|healer|periodic_ally_heal|1|0.8|1|0.9|30|1|P0|4초마다 반경 3 안에서 체력 비율이 가장 낮은 아군을 최대 체력의 8% 회복한다.|Every four seconds, it heals the nearby ally with the lowest health percentage for 8% of maximum health.|f:cooldownSec=4;i:healPercentMaxHp=8;i:targetCount=1;f:healRange=3|focus,stun,purge|A01,A02,A03,A05,A12",
        "S002|정비 담당|Mechanic Remnant|Special|Mechanic|player_disruption|highest_threat_stun|1.1|0.8|1|0.85|45|1|P0|10초마다 누적 피해량이 가장 높은 플레이어 대상을 5초 기절시킨다.|Every ten seconds, it stuns the player character with the highest total damage for five seconds.|f:cooldownSec=10;f:stunDurationSec=5|focus,purge,stunResist|A01,A02,A03,A12",
        "S003|침투자|Infiltrator Remnant|Special|Infiltrator|defense_bypass|first_guard_bypass|0.7|1|0.9|1.1|40|1|P0|첫 방어막 공격에 한해 방어막 경감과 일부 보호 효과를 30% 무시한다.|Its first core attack bypasses 30% of core mitigation and compatible protection effects.|i:bypassPercent=30|focus,shield,burstDamage|A02,A03,A12,A16",
        "S004|선봉|Pointman Remnant|Special|Pointman|offensive_leader|offensive_aura|1.1|0.9|1|0.9|55|1|P0|반경 3 안 적의 방어막 공격력은 10% 증가하고 공격 주기는 15% 단축된다.|Nearby enemies gain 10% core damage and 15% shorter core attack intervals.|f:coreAttackMultiplier=1.1;f:attackPeriodMultiplier=0.85;f:auraRange=3|focus,aoe,priorityTarget|A01,A02,A03,A05,A12",
        "S005|방패 운반자|Shield Bearer Remnant|Special|ShieldBearer|ally_shield|ally_shield_aura|1.4|0.9|1.1|0.75|70|1|P0|6초마다 반경 2.5 안 아군 2명에게 최대 체력 15%의 보호막을 부여한다.|Every six seconds, it shields two nearby allies for 15% of their maximum health.|i:shieldPercentMaxHp=15;f:refreshSec=6;i:targetCount=2;f:auraRange=2.5|focus,armorBreak,aoe|A01,A02,A03,A05,A12",
        "S006|무효화자|Nullifier Remnant|Special|Mechanic|status_control|ally_cleanse|1|0.8|1|0.85|60|1|P1|8초마다 주변 아군의 약화 효과를 제거하고 3초 동안 상태 이상 면역을 부여한다.|Every eight seconds, it cleanses nearby allies and grants three seconds of status immunity.|f:cooldownSec=8;f:immunityDurationSec=3;f:cleanseRange=3|focus,debuff,timing|A01,A02,A03,A05,A12",
        "S007|소환자|Summoner Remnant|Special|Mechanic|wave_expansion|periodic_spawn|1.1|0.8|1.1|0.8|65|1|P1|12초마다 기본 잔재 1개 또는 군집 잔재 2개를 생성한다. 최대 3회다.|Every twelve seconds, it summons one basic remnant or two swarm remnants, up to three activations.|f:cooldownSec=12;i:maxTriggers=3;e:spawnOptionA=G001;e:spawnOptionB=G008|focus,stun,aoe|A01,A02,A03,A05,A06,A12",
        "S008|위상 보행자|Phase Walker Remnant|Special|Infiltrator|target_window|telegraphed_invulnerability|1|1|1|1|75|1|P2|10초마다 0.8초 전조 후 1초 동안 타깃 불가가 된다.|Every ten seconds, a 0.8-second telegraph precedes one second of untargetability.|f:cooldownSec=10;f:invulnerableDurationSec=1;f:telegraphDurationSec=0.8|timing,singleTarget,telegraph|A02,A03,A12",
        "S009|공작자|Saboteur Remnant|Special|Infiltrator|core_system_disruption|core_system_jam|0.9|0.9|1|0.9|80|1|P1|방어선 첫 도착 시 6초 동안 에너지와 방어막 회복량을 각각 30% 감소시킨다.|On first core contact, it reduces energy and core recovery by 30% for six seconds.|f:durationSec=6;f:energyRegenMultiplier=0.7;f:shieldRecoveryMultiplier=0.7|focus,purge,shieldRepair,energy|A01,A02,A03,A12",
        "S010|연결자|Linker Remnant|Special|ShieldBearer|group_rule_change|multi_unit_link|1.2|1|1.1|0.8|85|1|P2|반경 3 안 최대 3명을 연결해 피해 30%를 공유하고, 사망 시 생존자의 공격력을 5초간 20% 높인다.|It links up to three nearby enemies to share 30% damage; a linked death grants survivors 20% core damage for five seconds.|i:maxLinkedUnits=3;i:sharedDamagePercent=30;f:survivorDamageMultiplier=1.2;f:survivorBuffDurationSec=5;f:linkRange=3|aoe,focus,purge|A01,A02,A03,A05,A08,A12",
        "E001|철벽 지휘관|Bastion Commander|Elite|ShieldBearer|defensive_elite|barrier_and_defense_aura|4|1.6|1.3|0.6|45|1|P1|최대 체력 15%의 보호막 2겹과 주변 적 피해 감소 20%를 제공하며, 한 겹 파괴 후 감소율은 10%가 된다.|It has two barrier layers worth 15% maximum health each and reduces nearby ally damage taken by 20%, falling to 10% after one layer breaks.|i:barrierLayers=2;i:barrierPercentMaxHpPerLayer=15;i:allyDamageReductionPercent=20;i:reductionAfterLayerBreakPercent=10;f:auraRange=3|armorBreak,focus,aoe|A02,A03,A05,A08,A09,A12,A21",
        "E002|시간 수확자|Time Harvester|Elite|Mechanic|tempo_elite|tempo_acceleration|3|1.2|1|0.8|60|1|P2|12초마다 5초 동안 모든 적 공격 및 소환 주기를 20% 단축한다.|Every twelve seconds, it shortens all enemy attack and summon intervals by 20% for five seconds.|f:cooldownSec=12;f:durationSec=5;f:attackPeriodMultiplier=0.8;f:spawnCooldownMultiplier=0.8|stun,timing,focus,shieldRepair|A01,A02,A03,A05,A12",
        "E003|군체 여왕|Brood Matriarch|Elite|Medic|spawn_elite|egg_production|4.5|1.4|1.4|0.7|70|1|P2|10초마다 군집 잔재 알을 만들며 체력 50% 이하에서는 6초마다 생성한다. 활성 알은 최대 3개다.|It creates a swarm egg every ten seconds, or every six seconds below 50% health, with at most three active eggs.|f:normalCooldownSec=10;f:enragedCooldownSec=6;i:thresholdPercent=50;i:maxActiveEggs=3;f:eggLifetimeSec=6;e:spawnEnemy=G008|focus,aoe,spawn,interrupt|A01,A02,A03,A06,A12",
        "E004|코어 사냥꾼|Core Hunter|Elite|Heavy|shield_hunter|shield_conversion_and_charge|4|1.8|1.6|0.75|80|1|P2|방어막 피해의 30%를 보호막으로 전환한다. 12초마다 5초 충전 후 최대 방어막을 8% 감소시킨다.|It converts 30% of core damage dealt into shield and, every twelve seconds, charges for five seconds before reducing maximum core health by 8%.|i:shieldConversionPercent=30;f:chargeDurationSec=5;i:maxShieldReductionPercent=8;f:cooldownSec=12|interrupt,stun,focus,shieldRepair|A01,A02,A03,A12",
        "E005|기억 재판관|Memory Judge|Elite|Mechanic|deck_control_elite|card_lock_and_cost_tax|3.5|1.2|1.2|0.8|90|1|P3|12초마다 무작위 손패 1장을 4초 잠근다. 체력 40% 이하에서는 5초 동안 모든 카드 비용이 1 증가한다.|Every twelve seconds, it locks a random hand card for four seconds. Below 40% health, all cards cost one more for five seconds.|f:lockCooldownSec=12;f:lockDurationSec=4;i:costTax=1;f:costTaxDurationSec=5;i:thresholdPercent=40|energy,deckRotation,focus,interrupt|A01,A02,A03,A12",
        "B001|숲의 관문자|Gatekeeper of the Forest|Boss|Heavy|final_boss|boss_phases|28|4|1.25|0.45|100|1|P3|P1은 12초 소환, P2는 침식 지대와 회복·최대치 감소, P3는 5초 충전 후 4배 공격을 사용한다.|Phase 1 summons escorts every twelve seconds; phase 2 creates erosion zones and suppresses core recovery; phase 3 charges for five seconds before a four-times core attack.|t:phase1HpRange=100-66;t:phase2HpRange=65-31;t:phase3HpRange=30-0;f:summonCooldownSec=12;e:summonOptionA=G001;e:summonOptionB=G002;f:erosionCooldownSec=8;i:erosionZoneCount=2;f:erosionRadius=2.5;f:shieldRecoveryMultiplier=0.7;i:maxShieldReductionPercent=10;f:phase3ChargeDurationSec=5;f:phase3DamageMultiplier=4;f:phase3AttackPeriodMultiplier=0.8;i:recommendedEscortCap=6|boss,stun,interrupt,aoe,shieldRepair,focus|A01,A02,A03,A06,A08,A10,A12,A13",
    };

    private static readonly AssumptionDefinition[] AssumptionTable =
    {
        new("A01", "Fractional core damage uses AccumulateFraction; the legacy inspector integer is rounded half-up."),
        new("A02", "Formation radius is inferred from roster tier, health multiplier, and legacy presentation archetype."),
        new("A03", "Core attack range is inferred from role: ranged support can attack from one rear layer; other entries are melee."),
        new("A04", "Missing general-enemy wave caps resolve to 2 for inferred support roles and 0 (composer default) otherwise."),
        new("A05", "The support tag is inferred for ally, recovery, tempo, summon, and global-control roles."),
        new("A06", "Unspecified small summons use G001, swarm/egg summons use G008, recursion is disabled, and active summons are capped at 12."),
        new("A07", "G011 burn deals 1 damage per stack on a one-second tick because the source omitted tick damage."),
        new("A08", "A missing aura, link, replay, egg, or boss-zone radius resolves to the value authored in the normalized parameters."),
        new("A09", "Each E001 barrier layer is 15% of maximum health because the source omitted layer durability."),
        new("A10", "Boss phases advance monotonically; reaching the core can advance P1 to P2 early and HP thresholds can also advance phases."),
        new("A11", "G029 uses its entry priority P1; the improvement-plan P3 placement is treated as stale."),
        new("A12", "Spawn budget is derived deterministically from health, damage, period, and speed multipliers."),
        new("A13", "B001 uses a 2x2 exclusive footprint; all other roster entries use 1x1."),
        new("A16", "Protection bypass percentage applies only to compatible core mitigation and never bypasses objective immunity."),
        new("A18", "All General and Special remnants share the remnant role tag for G030 resonance."),
        new("A19", "G019's unspecified decoy is normalized as a visible temporary force-focus state on G019 itself."),
        new("A20", "G026 requires direct-damage source identity and a two-second per-source hit history before damage resolution."),
        new("A21", "E001's two unspecified barrier layers are authored as 30% combined maximum-health armor; the aura is dynamically scoped but layer-break weakening requires layer-state support."),
    };

    private static readonly Dictionary<string, string> PreservedAssetFileNames =
        new(StringComparer.Ordinal)
        {
            { "G001", "G001_BasicRemnant.asset" },
            { "G002", "G002_AssaultRemnant.asset" },
            { "G003", "G003_HeavyRemnant.asset" },
            { "S001", "S001_MedicRemnant.asset" },
            { "S002", "S002_MechanicRemnant.asset" },
            { "S003", "S003_InfiltratorRemnant.asset" },
            { "S004", "S004_PointmanRemnant.asset" },
            { "S005", "S005_ShieldBearerRemnant.asset" },
        };

    private static readonly Dictionary<string, string> LegacyAssetGuids =
        new(StringComparer.Ordinal)
        {
            { "G001", "a4cc83968a6844d89f4087133b25ee3e" },
            { "G002", "fdbb9b76bc2747528b12a02ed1174696" },
            { "G003", "273cd6a369fc4f04a91f700675cbff67" },
            { "S001", "bb549f894fe9441caafb1499d2b3d6dc" },
            { "S002", "c1cc343b600a41c5a4131a5ddf257a87" },
            { "S003", "8ea554dd9a134ef8b3a9a88b227a4971" },
            { "S004", "7df2e18bb63f4108a85bdce82e508f53" },
            { "S005", "273cc0a824e845bda2fcf898627236a7" },
        };

    [MenuItem(MenuPath)]
    public static void GenerateFromMenu()
    {
        GenerateCatalog();
    }

    /// <summary>Unity -executeMethod entry point.</summary>
    public static void GenerateForBatchMode()
    {
        try
        {
            GenerateCatalog();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    public static EnemyRosterCatalogAudit AuditCatalogSource()
    {
        EnemySpec[] specs = BuildAndValidateSpecs();
        return new EnemyRosterCatalogAudit(
            specs.Length,
            specs.Count(s => s.Tier == EnemyRosterTier.General),
            specs.Count(s => s.Tier == EnemyRosterTier.Special),
            specs.Count(s => s.Tier == EnemyRosterTier.Elite),
            specs.Count(s => s.Tier == EnemyRosterTier.Boss),
            specs.Count(s => s.UsesFractionalCoreDamage),
            AssumptionTable.Length,
            specs.Count(s => s.EncounterOnly));
    }

    public static bool TryGetSpecSummary(
        string enemyId,
        out EnemyRosterSpecSummary summary)
    {
        EnemySpec spec = BuildAndValidateSpecs().FirstOrDefault(
            candidate => string.Equals(
                candidate.Id,
                enemyId,
                StringComparison.Ordinal));
        if (spec == null)
        {
            summary = default;
            return false;
        }

        summary = new EnemyRosterSpecSummary(
            spec.Id,
            spec.Tier,
            spec.BaseHealth,
            spec.PreciseCoreAttackDamage,
            spec.LegacyCoreAttackDamage,
            spec.CoreAttackInterval,
            spec.ApproachSpeed,
            spec.FormationRadius,
            spec.ForwardSearchAngle,
            spec.CoreAttackRange,
            spec.RecommendedMaxPerWave,
            spec.EncounterOnly,
            spec.Ability?.TypeId ?? string.Empty,
            spec.Abilities
                .SelectMany(item => item.Operations)
                .FirstOrDefault(item => item.Summon != null)?
                .Summon.AllowRecursiveSummon ?? false,
            spec.Abilities
                .SelectMany(item => item.Operations)
                .Where(item => item.Summon != null)
                .Select(item => item.Summon.MaximumActive)
                .DefaultIfEmpty(0)
                .Max());
        return true;
    }

    public static IReadOnlyList<string> GetAssumptionIds()
    {
        return AssumptionTable.Select(item => item.Id).ToArray();
    }

    public static void GenerateCatalog()
    {
        EnemySpec[] specs = BuildAndValidateSpecs();
        Dictionary<EEnemyType, EnemySO> templates =
            ResolvePresentationTemplates();

        EnsureFolder("Assets/06_Runtime");
        EnsureFolder("Assets/06_Runtime/Resources");
        EnsureFolder(EnemyFolder);
        DeleteScratchFolderIfPresent(StagingFolder);
        if (AssetDatabase.IsValidFolder(BackupFolder))
        {
            throw new InvalidOperationException(
                $"Recovery folder '{BackupFolder}' already exists. " +
                "Inspect and restore or remove it before rebuilding; its " +
                "contents are never discarded automatically.");
        }
        EnsureFolder(StagingFolder);

        List<string> stagedPaths = new(specs.Length);
        try
        {
            foreach (EnemySpec spec in specs)
            {
                string path = $"{StagingFolder}/{spec.FileName}";
                EnemySO enemy = ScriptableObject.CreateInstance<EnemySO>();
                enemy.name = Path.GetFileNameWithoutExtension(spec.FileName);
                PopulateEnemy(enemy, spec, templates[spec.LegacyType]);
                AssetDatabase.CreateAsset(enemy, path);
                stagedPaths.Add(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateAssets(specs, stagedPaths, "staged");
            CommitCatalog(specs, stagedPaths);

            EnemyDefinitionCatalog.Invalidate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Rebuilt and validated the 46-enemy public roster. " +
                "The eight legacy EnemySO GUIDs were preserved.");
        }
        catch
        {
            DeleteScratchFolderIfPresent(StagingFolder);
            AssetDatabase.Refresh();
            throw;
        }
    }

    /// <summary>
    /// Deterministic fallback for a project whose active Unity Editor cannot
    /// execute menu commands. It renders the same normalized specification to
    /// Unity text assets, validates all staged files, preserves every existing
    /// meta GUID, then commits with a recoverable Temp backup.
    /// </summary>
    public static void GenerateDeterministicYamlForLockedEditor()
    {
        EnemySpec[] specs = BuildAndValidateSpecs();
        string workspace = Path.GetFullPath(Environment.CurrentDirectory);
        string enemyFolder = ResolveWorkspacePath(workspace, EnemyFolder);
        string stagingFolder = ResolveWorkspacePath(
            workspace,
            YamlStagingFolder);
        string backupFolder = ResolveWorkspacePath(
            workspace,
            YamlBackupFolder);
        if (!Directory.Exists(enemyFolder))
        {
            throw new DirectoryNotFoundException(
                $"Enemy asset folder '{enemyFolder}' does not exist.");
        }
        if (Directory.Exists(backupFolder))
        {
            throw new InvalidOperationException(
                $"Recovery folder '{backupFolder}' already exists. " +
                "Inspect it before another deterministic YAML export.");
        }

        DeleteVerifiedTemporaryDirectory(workspace, stagingFolder);
        Directory.CreateDirectory(stagingFolder);
        Dictionary<EEnemyType, YamlPresentationTemplate> presentations =
            ReadYamlPresentationTemplates(enemyFolder);
        Dictionary<string, string> generatedGuids = new(
            StringComparer.Ordinal);
        foreach (EnemySpec spec in specs)
        {
            string stagedAsset = Path.Combine(
                stagingFolder,
                spec.FileName);
            File.WriteAllText(
                stagedAsset,
                RenderEnemyYaml(spec, presentations[spec.LegacyType]),
                Utf8WithoutBom);

            string liveMeta = Path.Combine(
                enemyFolder,
                spec.FileName + ".meta");
            string guid = File.Exists(liveMeta)
                ? ReadMetaGuid(liveMeta)
                : CreateDeterministicAssetGuid(spec.Id);
            generatedGuids.Add(spec.Id, guid);
            File.WriteAllText(
                stagedAsset + ".meta",
                RenderNativeAssetMeta(guid),
                Utf8WithoutBom);
        }

        ValidateLocalizationKeys(specs, workspace);
        ValidateYamlRoster(
            specs,
            stagingFolder,
            generatedGuids,
            requireExactDirectoryContents: true);
        foreach (KeyValuePair<string, string> pair in LegacyAssetGuids)
        {
            if (!string.Equals(
                    generatedGuids[pair.Key],
                    pair.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Legacy GUID preflight failed for {pair.Key}.");
            }
        }

        ValidateLiveAssetNamesBeforeYamlCommit(specs, enemyFolder);
        Directory.CreateDirectory(backupFolder);
        List<YamlCommitRecord> records = new(specs.Length);
        try
        {
            foreach (EnemySpec spec in specs)
            {
                string sourceAsset = Path.Combine(
                    stagingFolder,
                    spec.FileName);
                string sourceMeta = sourceAsset + ".meta";
                string destinationAsset = Path.Combine(
                    enemyFolder,
                    spec.FileName);
                string destinationMeta = destinationAsset + ".meta";
                bool assetExisted = File.Exists(destinationAsset);
                bool metaExisted = File.Exists(destinationMeta);
                if (assetExisted)
                {
                    File.Copy(
                        destinationAsset,
                        Path.Combine(backupFolder, spec.FileName),
                        overwrite: false);
                }
                if (metaExisted)
                {
                    File.Copy(
                        destinationMeta,
                        Path.Combine(
                            backupFolder,
                            spec.FileName + ".meta"),
                        overwrite: false);
                }

                records.Add(new YamlCommitRecord(
                    destinationAsset,
                    destinationMeta,
                    assetExisted,
                    metaExisted));
                File.Copy(sourceAsset, destinationAsset, overwrite: true);
                File.Copy(sourceMeta, destinationMeta, overwrite: true);
            }

            ValidateYamlRoster(
                specs,
                enemyFolder,
                generatedGuids,
                requireExactDirectoryContents: true);
        }
        catch
        {
            RollbackYamlCommit(records, backupFolder);
            throw;
        }
        finally
        {
            DeleteVerifiedTemporaryDirectory(workspace, stagingFolder);
        }
    }

    private static EnemySpec[] BuildAndValidateSpecs()
    {
        EnemySpec[] specs = RawEnemies.Select(ParseEnemy).ToArray();
        foreach (EnemySpec spec in specs)
        {
            ConfigureDerivedData(spec);
            ConfigureAbility(spec);
        }

        ValidateSpecs(specs);
        return specs;
    }

    private static EnemySpec ParseEnemy(string raw)
    {
        string[] columns = raw.Split('|');
        if (columns.Length != 19 ||
            !Enum.TryParse(columns[3], out EnemyRosterTier tier) ||
            !Enum.TryParse(columns[4], out EEnemyType legacyType) ||
            !TryFloat(columns[7], out float healthMultiplier) ||
            !TryFloat(columns[8], out float coreMultiplier) ||
            !TryFloat(columns[9], out float periodMultiplier) ||
            !TryFloat(columns[10], out float speedMultiplier) ||
            !int.TryParse(columns[11], out int unlockDifficulty) ||
            !int.TryParse(columns[12], out int authoredCap))
        {
            throw new InvalidOperationException(
                $"Invalid normalized enemy row: '{raw}'.");
        }

        return new EnemySpec
        {
            Id = columns[0],
            KoreanName = columns[1],
            EnglishName = columns[2],
            Tier = tier,
            LegacyType = legacyType,
            Role = columns[5],
            AbilityType = columns[6],
            HealthMultiplier = healthMultiplier,
            CoreAttackMultiplier = coreMultiplier,
            AttackPeriodMultiplier = periodMultiplier,
            MoveSpeedMultiplier = speedMultiplier,
            UnlockDifficulty = unlockDifficulty,
            AuthoredRecommendedMaxPerWave = authoredCap,
            Priority = columns[13],
            KoreanAbilityDescription = columns[14],
            EnglishAbilityDescription = columns[15],
            Parameters = ParseParameters(columns[16]),
            CounterTags = SplitCsv(columns[17]),
            AssumptionIds = SplitCsv(columns[18]),
        };
    }

    private static void ConfigureDerivedData(EnemySpec spec)
    {
        spec.Grade = spec.Tier switch
        {
            EnemyRosterTier.Special => EEnemyGrade.Special,
            EnemyRosterTier.Elite => EEnemyGrade.Elite,
            EnemyRosterTier.Boss => EEnemyGrade.Boss,
            _ => EEnemyGrade.Normal,
        };
        spec.BaseHealth = RoundHalfUp(20f * spec.HealthMultiplier);
        spec.PreciseCoreAttackDamage = 5f * spec.CoreAttackMultiplier;
        spec.LegacyCoreAttackDamage = RoundHalfUp(
            spec.PreciseCoreAttackDamage);
        spec.CoreAttackInterval = 2f * spec.AttackPeriodMultiplier;
        spec.ApproachSpeed = 0.08f * spec.MoveSpeedMultiplier;
        spec.UsesFractionalCoreDamage =
            !Mathf.Approximately(
                spec.PreciseCoreAttackDamage,
                Mathf.Round(spec.PreciseCoreAttackDamage));
        spec.FormationRadius = ResolveFormationRadius(spec);
        spec.CoreAttackRange = ResolveCoreAttackRange(spec);
        spec.RecommendedMaxPerWave =
            ResolveRecommendedMaxPerWave(spec);
        spec.EncounterOnly = spec.Tier == EnemyRosterTier.Boss;
        spec.SpawnBudget = ResolveSpawnBudget(spec);
        spec.FileName = ResolveFileName(spec);

        spec.RoleTags.Add(spec.Role);
        spec.RoleTags.Add(spec.Tier.ToString().ToLowerInvariant());
        if (spec.Tier == EnemyRosterTier.General ||
            spec.Tier == EnemyRosterTier.Special)
        {
            spec.RoleTags.Add("remnant");
        }
        if (IsSupportRole(spec))
            spec.RoleTags.Add("support");
        if (spec.AbilityType.Contains("spawn", StringComparison.Ordinal) ||
            spec.AbilityType.Contains("summon", StringComparison.Ordinal) ||
            spec.AbilityType.Contains("egg", StringComparison.Ordinal))
        {
            spec.RoleTags.Add("summoner");
        }
        if (spec.AbilityType.Contains("charge", StringComparison.Ordinal))
            spec.RoleTags.Add("charger");
        spec.RoleTags = spec.RoleTags
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static void ConfigureAbility(EnemySpec spec)
    {
        if (spec.AbilityType == "none")
            return;

        AbilitySpec ability = new()
        {
            Id = $"roster_{spec.Id.ToLowerInvariant()}_{spec.AbilityType}",
            TypeId = spec.AbilityType,
            FallbackName = Humanize(spec.AbilityType),
            FallbackDescription = spec.EnglishAbilityDescription,
            Trigger = "AlwaysWhileActive",
            Target = TargetSpec.None(),
        };
        ability.Parameters.AddRange(spec.Parameters);
        ability.Parameters.Add(ParameterSpec.Text(
            "implementationPriority",
            spec.Priority));
        ability.Parameters.Add(ParameterSpec.Text(
            "assumptionIds",
            string.Join(",", spec.AssumptionIds)));
        List<AbilitySpec> supplemental = new();

        switch (spec.Id)
        {
            case "G002":
                ability.Trigger = "OnFirstCoreContact";
                ability.AddOperation("ModifyCoreAttackDamage")
                    .Percentage = 0.25f;
                break;
            case "G003":
                ability.Target = TargetSpec.Self();
                ability.AddOperation("ModifyStatusDuration")
                    .Multiplier = 0.5f;
                break;
            case "G004":
                ConfigureCharge(ability, 1.2f, 1.8f, 0.8f);
                ability.Cooldown = 8f;
                break;
            case "G005":
                ability.Trigger = "OnFirstCoreContact";
                ability.AddOperation("ApplyCoreEffect")
                    .Percentage = 0.2f;
                break;
            case "G006":
                ability.Trigger = "OnHealthThreshold";
                ability.HealthThresholdPercent = 50f;
                ability.Target = TargetSpec.Self();
                ability.AddOperation("ModifyCoreAttackDamage")
                    .Multiplier = 1.25f;
                break;
            case "G007":
                ability.Trigger = "OnDeath";
                ability.AddSummon(
                    new[] { "G001" }, 2, 2, 0.35f, 0.35f);
                break;
            case "G008":
                ability.Trigger = "OnSpawn";
                ability.AddSummon(
                    new[] { "G008" }, 2, 4, 1f, 1f);
                break;
            case "G009":
                ability.Trigger = "BeforeSelfDamage";
                ability.Cooldown = 3f;
                ability.Target = TargetSpec.Self();
                OperationSpec armor =
                    ability.AddOperation("ModifyIncomingDamage");
                armor.Multiplier = 0.65f;
                break;
            case "G010":
                ability.Trigger = "AfterNoDamage";
                ability.NoDamageDuration = 3f;
                ability.Target = TargetSpec.Self();
                ability.AddEffect(EffectSpec.PercentMaxHealth(
                    "regenerate",
                    "Heal",
                    0.03f));
                ability.Operations[0].Interval = 2f;
                break;
            case "G011":
                ability.Trigger = "OnCoreHit";
                OperationSpec burn = ability.AddOperation("ApplyCoreEffect");
                burn.Amount = 1;
                burn.Duration = 4f;
                burn.Interval = 1f;
                burn.MaximumStacks = 3;
                break;
            case "G012":
                ability.Trigger = "OnCoreHit";
                OperationSpec bleed =
                    ability.AddOperation("ModifyCoreAttackDamage");
                bleed.Percentage = 0.1f;
                bleed.MaximumStacks = 3;
                AbilitySpec bleedDecay = CreateSupplementalAbility(
                    spec,
                    "stun_decay",
                    "repeated_core_hit_stack_decay",
                    "OnStatusApplied");
                bleedDecay.Target = TargetSpec.Self();
                bleedDecay.Conditions.Add(new ConditionSpec
                {
                    Type = "SourceHasStatus",
                    StatusAssetName = "Stun",
                    Expected = true,
                });
                bleedDecay.Parameters.Add(
                    ParameterSpec.Text("requiredStatusId", "stun"));
                bleedDecay.Parameters.Add(
                    ParameterSpec.Integer("stackDelta", -1));
                bleedDecay.AddOperation("ModifyCoreAttackDamage")
                    .Percentage = -0.1f;
                supplemental.Add(bleedDecay);
                break;
            case "G013":
                ability.Trigger = "OnDeath";
                OperationSpec toxicZone =
                    ability.AddOperation("CreateWorldZone");
                toxicZone.Duration = 4f;
                toxicZone.WorldRadius = 2.5f;
                toxicZone.Multiplier = 0.75f;
                break;
            case "G014":
                ConfigureAura(ability, 3f, false);
                ability.AddOperation("ModifyCoreAttackInterval")
                    .Multiplier = 0.85f;
                break;
            case "G015":
                ability.Trigger = "OnCoreHit";
                OperationSpec erosion =
                    ability.AddOperation("ModifyCoreMaximumHealth");
                erosion.Percentage = -0.05f;
                erosion.Duration = 6f;
                erosion.MaximumStacks = 2;
                break;
            case "G016":
                ability.Trigger = "OnNearbyEnemyDeath";
                ability.InitialCharges = 3;
                ability.Target = TargetSpec.Self();
                ability.AddEffect(EffectSpec.PercentMaxHealth(
                    "absorb_shield",
                    "Shield",
                    0.10f));
                ability.Operations[0].Effects.Add(
                    EffectSpec.PercentMaxHealth(
                        "absorb_heal",
                        "Heal",
                        0.05f));
                ability.Operations[0].WorldRadius = 2.5f;
                break;
            case "G017":
                ConfigureAura(ability, 2.5f, false);
                ability.AddOperation("ModifyStatusDuration")
                    .Multiplier = 0.75f;
                AbilitySpec anchorSelf = CreateSupplementalAbility(
                    spec,
                    "self_resistance",
                    "self_control_resistance",
                    "AlwaysWhileActive");
                anchorSelf.Target = TargetSpec.Self();
                anchorSelf.AddOperation("ModifyStatusDuration")
                    .Multiplier = 0.5f;
                supplemental.Add(anchorSelf);
                break;
            case "G018":
                ability.Trigger = "OnSpawn";
                ability.TriggerEvents.Add("OnAllyEnteredRadius");
                ability.TriggerEvents.Add("OnNearbyEnemyDeath");
                ability.Target = TargetSpec.Allies(3f, 1, false);
                OperationSpec bind = ability.AddOperation("LinkTargets");
                bind.Percentage = 0.2f;
                bind.WorldRadius = 3f;
                break;
            case "G019":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 10f;
                ability.Target = TargetSpec.Self();
                OperationSpec decoy =
                    ability.AddOperation("ModifyTargetPriority");
                decoy.Duration = 3f;
                decoy.TargetPriorityMode = "ForceFocus";
                break;
            case "G020":
                ability.Trigger = "OnCoreHit";
                ability.Target = TargetSpec.Self();
                ability.AddOperation("ApplyCoreEffect")
                    .Percentage = 0.25f;
                break;
            case "G021":
                ConfigureCharge(ability, 3f, 2.2f, 0.8f);
                ability.Cooldown = 10f;
                break;
            case "G022":
                ability.Trigger = "OnDeath";
                ability.Target = TargetSpec.Allies(3f, 1, false);
                OperationSpec replay = ability.AddOperation("ReplayAbility");
                replay.Percentage = 0.5f;
                replay.WorldRadius = 3f;
                ability.Parameters.Add(ParameterSpec.Text(
                    "excludedEnemyTiers",
                    "Elite,Boss"));
                break;
            case "G023":
                ability.Trigger = "OnHealthThreshold";
                ability.TriggerEvents.Add("OnCoreContact");
                ability.HealthThresholdPercent = 40f;
                ability.InitialCharges = 1;
                ability.AddSummon(
                    new[] { "G001" }, 2, 2, 1f, 1f);
                break;
            case "G024":
                ability.Target = TargetSpec.PlayersInRadius(3f);
                ability.AddOperation("ModifyResourceRecovery")
                    .Multiplier = 0.8f;
                break;
            case "G025":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 8f;
                ability.Target = TargetSpec.Self();
                ability.AddOperation("ReflectDamage")
                    .Percentage = 0.35f;
                ability.Telegraph = TelegraphSpec.Create(
                    0f,
                    "enemy.reflect.ready",
                    0f);
                break;
            case "G026":
                ability.Trigger = "BeforeSelfDamage";
                ability.Target = TargetSpec.Self();
                ability.Conditions.Add(new ConditionSpec
                {
                    Type = "RepeatedDamageSource",
                    WindowDuration = 2f,
                    Expected = true,
                });
                OperationSpec prism =
                    ability.AddOperation("ModifyIncomingDamage");
                prism.Multiplier = 0.75f;
                prism.Duration = 2f;
                break;
            case "G027":
                ability.Trigger = "OnDeath";
                ability.AddSummon(
                    new[] { "G001" }, 2, 2, 1f, 1f);
                ability.Operations[0].Duration = 6f;
                break;
            case "G028":
                ability.Target = TargetSpec.PlayersInRadius(3f);
                ability.AddOperation("ModifyPlayerActionInterval")
                    .Multiplier = 1.15f;
                break;
            case "G029":
                ability.Trigger = "OnHealthThreshold";
                ability.HealthThresholdPercent = 50f;
                ability.Target = TargetSpec.Self();
                ability.AddOperation("ModifyIncomingDamage")
                    .Multiplier = 0.8f;
                ability.AddOperation("ModifyCoreAttackInterval")
                    .Multiplier = 0.8f;
                break;
            case "G030":
                ConfigureAura(ability, 2.5f, false);
                ability.AddOperation("ModifyCoreAttackDamage")
                    .Multiplier = 1.1f;
                ability.AddOperation("ModifyCoreAttackInterval")
                    .Multiplier = 0.9f;
                break;
            case "S001":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 4f;
                ability.Target = TargetSpec.AlliesLowest(3f, 1);
                ability.AddEffect(EffectSpec.PercentMaxHealth(
                    "field_treatment",
                    "Heal",
                    0.08f));
                break;
            case "S002":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 10f;
                ability.Target = TargetSpec.PlayersHighestDamage();
                ability.AddEffect(EffectSpec.Status(
                    "system_disruption",
                    "ApplyStatus",
                    5f,
                    "Stun"));
                break;
            case "S003":
                ability.Trigger = "OnFirstCoreContact";
                ability.AddOperation("ApplyCoreEffect")
                    .Percentage = 0.3f;
                break;
            case "S004":
                ConfigureAura(ability, 3f, false);
                ability.AddOperation("ModifyCoreAttackDamage")
                    .Multiplier = 1.1f;
                ability.AddOperation("ModifyCoreAttackInterval")
                    .Multiplier = 0.85f;
                break;
            case "S005":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 6f;
                ability.Target = TargetSpec.Allies(2.5f, 2, false);
                ability.AddEffect(EffectSpec.PercentMaxHealth(
                    "shield_refresh",
                    "Shield",
                    0.15f));
                break;
            case "S006":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 8f;
                ability.Target = TargetSpec.Allies(3f, int.MaxValue, true);
                OperationSpec immunity =
                    ability.AddOperation("GrantStatusImmunity");
                immunity.Duration = 3f;
                immunity.WorldRadius = 3f;
                AbilitySpec cleanse = CreateSupplementalAbility(
                    spec,
                    "cleanse",
                    "ally_debuff_cleanse",
                    "OnCooldown");
                cleanse.Cooldown = 8f;
                cleanse.Target = TargetSpec.Allies(
                    3f,
                    int.MaxValue,
                    true);
                cleanse.AddEffect(EffectSpec.RemoveDebuffs(
                    "nullifier_cleanse"));
                supplemental.Add(cleanse);
                break;
            case "S007":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 12f;
                ability.InitialCharges = 3;
                ability.AddSummon(
                    new[] { "G001", "G008" }, 1, 2, 1f, 1f);
                ability.Parameters.Add(ParameterSpec.Text(
                    "candidateCountMap",
                    "G001:1,G008:2"));
                break;
            case "S008":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 10f;
                ability.Target = TargetSpec.Self();
                ability.AddOperation("SetUntargetable")
                    .Duration = 1f;
                ability.Telegraph = TelegraphSpec.Create(
                    0.8f,
                    "enemy.phase_walker.shift",
                    0f);
                ability.Charge = ChargeSpec.Create(0.8f);
                break;
            case "S009":
                ability.Trigger = "OnFirstCoreContact";
                OperationSpec energyJam =
                    ability.AddOperation("ModifyResourceRecovery");
                energyJam.Multiplier = 0.7f;
                energyJam.Duration = 6f;
                OperationSpec coreJam =
                    ability.AddOperation("ModifyCoreRecovery");
                coreJam.Multiplier = 0.7f;
                coreJam.Duration = 6f;
                break;
            case "S010":
                ability.Trigger = "OnSpawn";
                ability.TriggerEvents.Add("OnAllyEnteredRadius");
                ability.Target = TargetSpec.Allies(3f, 3, true);
                OperationSpec link = ability.AddOperation("LinkTargets");
                link.Percentage = 0.3f;
                link.WorldRadius = 3f;
                AbilitySpec linkedDeath = CreateSupplementalAbility(
                    spec,
                    "survivor_buff",
                    "linked_death_survivor_buff",
                    "OnNearbyEnemyDeath");
                linkedDeath.Target = TargetSpec.Allies(3f, 3, false);
                linkedDeath.Parameters.Add(
                    ParameterSpec.Boolean("linkedOnly", true));
                OperationSpec survivorBuff =
                    linkedDeath.AddOperation("ModifyCoreAttackDamage");
                survivorBuff.Multiplier = 1.2f;
                survivorBuff.Duration = 5f;
                supplemental.Add(linkedDeath);
                break;
            case "E001":
                ability.Trigger = "OnSpawn";
                ability.Target = TargetSpec.Self();
                ability.Parameters.Add(ParameterSpec.Text(
                    "normalizationMode",
                    "combinedArmorThirtyPercent"));
                OperationSpec barrier = ability.AddOperation("GrantArmor");
                barrier.Multiplier = 0.15f;
                barrier.Count = 2;
                AbilitySpec bastionAura = CreateSupplementalAbility(
                    spec,
                    "defense_aura",
                    "barrier_defense_aura",
                    "AlwaysWhileActive");
                bastionAura.Target = TargetSpec.Allies(
                    3f,
                    int.MaxValue,
                    false);
                bastionAura.Parameters.Add(ParameterSpec.Float(
                    "postLayerBreakMultiplier",
                    0.9f));
                OperationSpec defenseAura =
                    bastionAura.AddOperation("ModifyIncomingDamage");
                defenseAura.Multiplier = 0.8f;
                defenseAura.WorldRadius = 3f;
                supplemental.Add(bastionAura);
                break;
            case "E002":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 12f;
                ability.Target = TargetSpec.AlliesAll(true);
                OperationSpec haste =
                    ability.AddOperation("ModifyCoreAttackInterval");
                haste.Multiplier = 0.8f;
                haste.Duration = 5f;
                OperationSpec spawnHaste =
                    ability.AddOperation("ModifySpawnInterval");
                spawnHaste.Multiplier = 0.8f;
                spawnHaste.Duration = 5f;
                break;
            case "E003":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 10f;
                ability.CooldownOverrides.Add(
                    new CooldownOverrideSpec(50f, 6f));
                ability.AddSummon(
                    new[] { "G008" }, 1, 1, 1f, 1f, 3);
                ability.Operations[0].Duration = 6f;
                break;
            case "E004":
                ConfigureCharge(ability, 5f, 1f, 1f);
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 12f;
                ability.AddOperation("ModifyCoreMaximumHealth")
                    .Percentage = -0.08f;
                AbilitySpec shieldConversion = CreateSupplementalAbility(
                    spec,
                    "shield_conversion",
                    "core_damage_to_self_shield",
                    "OnCoreHit");
                shieldConversion.Target = TargetSpec.Self();
                shieldConversion.AddOperation("ConvertCoreDamageToSelfShield")
                    .Percentage = 0.3f;
                supplemental.Add(shieldConversion);
                break;
            case "E005":
                ability.Trigger = "OnCooldown";
                ability.Cooldown = 12f;
                ability.AddOperation("LockCard").Duration = 4f;
                AbilitySpec costTax = CreateSupplementalAbility(
                    spec,
                    "cost_tax",
                    "health_threshold_card_cost_tax",
                    "OnHealthThreshold");
                costTax.HealthThresholdPercent = 40f;
                OperationSpec tax =
                    costTax.AddOperation("ModifyCardCost");
                tax.Amount = 1;
                tax.Duration = 5f;
                supplemental.Add(costTax);
                break;
            case "B001":
                ConfigureBossAbilities(spec, ability, supplemental);
                break;
            default:
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} has no normalized ability mapping.");
        }

        spec.Abilities.Add(ability);
        spec.Abilities.AddRange(supplemental);
        foreach (AbilitySpec configuredAbility in spec.Abilities)
        {
            foreach (OperationSpec operation in configuredAbility.Operations)
            {
                operation.SourceId =
                    $"{spec.Id}:{configuredAbility.Id}";
            }
        }
    }

    private static AbilitySpec CreateSupplementalAbility(
        EnemySpec spec,
        string suffix,
        string typeId,
        string trigger)
    {
        AbilitySpec ability = new()
        {
            Id = $"roster_{spec.Id.ToLowerInvariant()}_{suffix}",
            TypeId = typeId,
            FallbackName = Humanize(typeId),
            FallbackDescription = spec.EnglishAbilityDescription,
            Trigger = trigger,
            Target = TargetSpec.None(),
        };
        ability.Parameters.Add(ParameterSpec.Text(
            "implementationPriority",
            spec.Priority));
        ability.Parameters.Add(ParameterSpec.Text(
            "assumptionIds",
            string.Join(",", spec.AssumptionIds)));
        return ability;
    }

    private static void ConfigureAura(
        AbilitySpec ability,
        float radius,
        bool includeSource)
    {
        ability.Trigger = "AlwaysWhileActive";
        ability.Target = TargetSpec.Allies(
            radius,
            int.MaxValue,
            includeSource);
    }

    private static void ConfigureCharge(
        AbilitySpec ability,
        float duration,
        float damageMultiplier,
        float telegraphLead,
        bool addOperation = true)
    {
        ability.Trigger = "BeforeCoreAttack";
        ability.Target = TargetSpec.Self();
        ability.Charge = ChargeSpec.Create(duration);
        ability.Telegraph = TelegraphSpec.Create(
            telegraphLead,
            "enemy.core_charge",
            0f);
        if (addOperation)
        {
            OperationSpec operation =
                ability.AddOperation("ChargeCoreAttack");
            operation.Duration = duration;
            operation.Multiplier = damageMultiplier;
        }
    }

    private static void ConfigureBossAbilities(
        EnemySpec spec,
        AbilitySpec phaseOneSummon,
        ICollection<AbilitySpec> supplemental)
    {
        phaseOneSummon.Id = "b001.p1.summon";
        phaseOneSummon.TypeId = "boss_p1_periodic_summon";
        phaseOneSummon.Trigger = "OnCooldown";
        phaseOneSummon.Cooldown = 12f;
        phaseOneSummon.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P1"));
        phaseOneSummon.Parameters.Add(ParameterSpec.Text(
            "candidateCountMap",
            "G001:1,G002:1"));
        OperationSpec summons = phaseOneSummon.AddSummon(
            new[] { "G001", "G002" }, 1, 1, 1f, 1f, 6);
        summons.Interval = 12f;

        AbilitySpec phaseOneAura = CreateSupplementalAbility(
            spec,
            "p1.control_aura",
            "boss_p1_control_resistance_aura",
            "AlwaysWhileActive");
        phaseOneAura.Id = "b001.p1.control_aura";
        phaseOneAura.Target = TargetSpec.Allies(3f, int.MaxValue, false);
        phaseOneAura.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P1"));
        phaseOneAura.Parameters.Add(ParameterSpec.Boolean(
            "summonedOnly",
            true));
        phaseOneAura.AddOperation("ModifyStatusDuration")
            .Multiplier = 0.75f;
        supplemental.Add(phaseOneAura);

        AbilitySpec phaseTwoErosion = CreateSupplementalAbility(
            spec,
            "p2.erosion",
            "boss_p2_erosion_zones",
            "OnCooldown");
        phaseTwoErosion.Id = "b001.p2.erosion";
        phaseTwoErosion.Cooldown = 8f;
        phaseTwoErosion.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P2"));
        OperationSpec zone =
            phaseTwoErosion.AddOperation("CreateWorldZone");
        zone.Count = 2;
        zone.Interval = 8f;
        zone.WorldRadius = 2.5f;
        zone.Multiplier = 0.7f;
        zone.Duration = 8f;
        supplemental.Add(phaseTwoErosion);

        AbilitySpec phaseTwoHeal = CreateSupplementalAbility(
            spec,
            "p2.death_heal",
            "boss_p2_nearby_death_heal",
            "OnNearbyEnemyDeath");
        phaseTwoHeal.Id = "b001.p2.death_heal";
        phaseTwoHeal.Target = TargetSpec.Self();
        phaseTwoHeal.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P2"));
        phaseTwoHeal.Parameters.Add(ParameterSpec.Text(
            "requiredEnemyTier",
            "General"));
        phaseTwoHeal.AddEffect(EffectSpec.PercentMaxHealth(
            "gatekeeper_memory_absorption",
            "Heal",
            0.03f));
        phaseTwoHeal.Operations[0].WorldRadius = 3f;
        supplemental.Add(phaseTwoHeal);

        AbilitySpec phaseTwoMaximum = CreateSupplementalAbility(
            spec,
            "p2.maximum_health",
            "boss_p2_core_maximum_reduction",
            "OnPhaseChanged");
        phaseTwoMaximum.Id = "b001.p2.maximum_health";
        phaseTwoMaximum.InitialCharges = 1;
        phaseTwoMaximum.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P2"));
        phaseTwoMaximum.AddOperation("ModifyCoreMaximumHealth")
            .Percentage = -0.10f;
        supplemental.Add(phaseTwoMaximum);

        AbilitySpec phaseThreeCharge = CreateSupplementalAbility(
            spec,
            "p3.charge",
            "boss_p3_gate_opening_charge",
            "BeforeCoreAttack");
        phaseThreeCharge.Id = "b001.p3.charge";
        phaseThreeCharge.Target = TargetSpec.Self();
        phaseThreeCharge.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P3"));
        phaseThreeCharge.Charge = ChargeSpec.Create(5f);
        phaseThreeCharge.Telegraph = TelegraphSpec.Create(
            1f,
            "enemy.boss.gatekeeper.charge",
            2.5f);
        OperationSpec charge =
            phaseThreeCharge.AddOperation("ChargeCoreAttack");
        charge.Duration = 5f;
        charge.Multiplier = 4f;
        supplemental.Add(phaseThreeCharge);

        AbilitySpec phaseThreeAura = CreateSupplementalAbility(
            spec,
            "p3.aura",
            "boss_p3_global_attack_haste",
            "AlwaysWhileActive");
        phaseThreeAura.Id = "b001.p3.aura";
        phaseThreeAura.Target = TargetSpec.AlliesAll(false);
        phaseThreeAura.Parameters.Add(ParameterSpec.Text(
            "activePhaseId",
            "P3"));
        phaseThreeAura.Parameters.Add(ParameterSpec.Text(
            "requiredEnemyTier",
            "General"));
        phaseThreeAura.AddOperation("ModifyCoreAttackInterval")
            .Multiplier = 0.8f;
        supplemental.Add(phaseThreeAura);
    }

    private static void ValidateSpecs(IReadOnlyList<EnemySpec> specs)
    {
        if (specs.Count != ExpectedEnemyCount)
        {
            throw new InvalidOperationException(
                $"Roster must contain {ExpectedEnemyCount} enemies; " +
                $"found {specs.Count}.");
        }

        ValidateCount(
            specs,
            EnemyRosterTier.General,
            ExpectedGeneralCount);
        ValidateCount(
            specs,
            EnemyRosterTier.Special,
            ExpectedSpecialCount);
        ValidateCount(specs, EnemyRosterTier.Elite, ExpectedEliteCount);
        ValidateCount(specs, EnemyRosterTier.Boss, ExpectedBossCount);

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> koreanNames = new(StringComparer.Ordinal);
        HashSet<string> englishNames = new(StringComparer.Ordinal);
        HashSet<string> fileNames =
            new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> assumptions = AssumptionTable
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);

        for (int index = 0; index < specs.Count; index++)
        {
            EnemySpec spec = specs[index];
            ValidateExpectedId(spec, index);
            if (!ids.Add(spec.Id) ||
                !koreanNames.Add(spec.KoreanName) ||
                !englishNames.Add(spec.EnglishName) ||
                !fileNames.Add(spec.FileName))
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} duplicates an ID, name, or filename.");
            }
            if (string.IsNullOrWhiteSpace(spec.KoreanName) ||
                string.IsNullOrWhiteSpace(spec.EnglishName) ||
                string.IsNullOrWhiteSpace(spec.Role) ||
                spec.BaseHealth <= 0 ||
                spec.PreciseCoreAttackDamage <= 0f ||
                spec.LegacyCoreAttackDamage <= 0 ||
                spec.CoreAttackInterval <= 0f ||
                spec.ApproachSpeed <= 0f ||
                spec.FormationRadius <= 0f ||
                spec.CoreAttackRange < 0f ||
                spec.SpawnBudget <= 0f ||
                spec.UnlockDifficulty < 0 ||
                spec.UnlockDifficulty > 100 ||
                spec.RecommendedMaxPerWave < 0)
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} has invalid normalized data.");
            }
            if (spec.Tier == EnemyRosterTier.Boss != spec.EncounterOnly)
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} has an invalid encounter-only rule.");
            }
            if (spec.AssumptionIds.Count == 0 ||
                spec.AssumptionIds.Any(id => !assumptions.Contains(id)))
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} references an unknown assumption.");
            }
            if (spec.RoleTags.Count == 0 ||
                spec.RoleTags.Any(string.IsNullOrWhiteSpace) ||
                spec.CounterTags.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} has an invalid role or counter tag.");
            }
            if (spec.AbilityType == "none")
            {
                if (spec.Abilities.Count != 0)
                    throw new InvalidOperationException(
                        $"Baseline enemy {spec.Id} unexpectedly has an ability.");
                continue;
            }

            ValidateAbilitySpecs(spec, ids: null);
        }

        foreach (EnemySpec spec in specs)
            ValidateAbilitySpecs(spec, ids);

        if (specs.Count(spec => spec.UsesFractionalCoreDamage) != 11)
        {
            throw new InvalidOperationException(
                "The normalized catalog must retain exactly 11 fractional " +
                "core-damage definitions.");
        }

        EnemySpec boss = specs.Single(spec => spec.Id == "B001");
        if (!boss.EncounterOnly || boss.RecommendedMaxPerWave != 1)
        {
            throw new InvalidOperationException(
                "B001 must be encounter-only with a wave cap of one.");
        }

        AbilitySpec phaseWalker = specs.Single(spec => spec.Id == "S008")
            .Abilities.Single(ability =>
                ability.TypeId == "telegraphed_invulnerability");
        if (!phaseWalker.Charge.Enabled ||
            !phaseWalker.Telegraph.Enabled ||
            !Mathf.Approximately(
                phaseWalker.Charge.Duration,
                phaseWalker.Telegraph.LeadTime))
        {
            throw new InvalidOperationException(
                "S008 must charge for the complete telegraph lead time " +
                "before becoming untargetable.");
        }

        AbilitySpec linker = specs.Single(spec => spec.Id == "S010")
            .Abilities.Single(ability =>
                ability.TypeId == "multi_unit_link");
        if (!linker.TriggerEvents.Contains("OnAllyEnteredRadius"))
        {
            throw new InvalidOperationException(
                "S010 must refresh its link when an ally enters range.");
        }
    }

    private static void ValidateAbilitySpecs(
        EnemySpec spec,
        HashSet<string> ids)
    {
        if (spec.AbilityType == "none")
            return;
        if (spec.Abilities.Count == 0)
        {
            throw new InvalidOperationException(
                $"Enemy {spec.Id} has no normalized ability definition.");
        }

        HashSet<string> abilityIds = new(StringComparer.Ordinal);
        foreach (AbilitySpec ability in spec.Abilities)
        {
            if (string.IsNullOrWhiteSpace(ability.Id) ||
                !abilityIds.Add(ability.Id) ||
                string.IsNullOrWhiteSpace(ability.TypeId) ||
                string.IsNullOrWhiteSpace(ability.Trigger) ||
                ability.Operations.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Enemy {spec.Id} has an incomplete ability definition.");
            }

            HashSet<string> parameterKeys = new(StringComparer.Ordinal);
            foreach (ParameterSpec parameter in ability.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key) ||
                    !parameterKeys.Add(parameter.Key))
                {
                    throw new InvalidOperationException(
                        $"Enemy {spec.Id} has a duplicate ability parameter.");
                }
                if (ids != null &&
                    parameter.ValueType == "EnemyReference" &&
                    !ids.Contains(parameter.TextValue))
                {
                    throw new InvalidOperationException(
                        $"Enemy {spec.Id} references missing enemy " +
                        $"'{parameter.TextValue}'.");
                }
            }

            foreach (OperationSpec operation in ability.Operations)
            {
                if (string.IsNullOrWhiteSpace(operation.Type) ||
                    string.IsNullOrWhiteSpace(operation.SourceId))
                {
                    throw new InvalidOperationException(
                        $"Enemy {spec.Id} has an incomplete operation.");
                }
                EnemySummonSpec summon = operation.Summon;
                if (summon == null)
                    continue;
                if (summon.AllowRecursiveSummon ||
                    summon.MaximumActive <= 0 ||
                    summon.MaximumActive > SimultaneousSummonCap ||
                    summon.MinimumCount < 1 ||
                    summon.MaximumCount < summon.MinimumCount ||
                    summon.CandidateIds.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Enemy {spec.Id} violates summon safety limits.");
                }
                if (ids != null &&
                    summon.CandidateIds.Any(id => !ids.Contains(id)))
                {
                    throw new InvalidOperationException(
                        $"Enemy {spec.Id} has an unresolved summon candidate.");
                }
            }
        }
    }

    private static void ValidateExpectedId(EnemySpec spec, int index)
    {
        string expected = index switch
        {
            < ExpectedGeneralCount => $"G{index + 1:000}",
            < ExpectedGeneralCount + ExpectedSpecialCount =>
                $"S{index - ExpectedGeneralCount + 1:000}",
            < ExpectedGeneralCount + ExpectedSpecialCount +
                ExpectedEliteCount =>
                $"E{index - ExpectedGeneralCount - ExpectedSpecialCount + 1:000}",
            _ => "B001",
        };
        if (!string.Equals(spec.Id, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Roster row {index + 1} has ID {spec.Id}; " +
                $"expected {expected}.");
        }
    }

    private static void ValidateCount(
        IEnumerable<EnemySpec> specs,
        EnemyRosterTier tier,
        int expected)
    {
        int actual = specs.Count(spec => spec.Tier == tier);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Expected {expected} {tier} enemies, found {actual}.");
        }
    }

    private static Dictionary<EEnemyType, EnemySO>
        ResolvePresentationTemplates()
    {
        Dictionary<EEnemyType, EnemySO> templates = new();
        foreach (EEnemyType type in Enum.GetValues(typeof(EEnemyType)))
        {
            string path =
                $"{EnemyFolder}/{ResolvePresentationTemplateFileName(type)}";
            EnemySO template = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (template == null)
            {
                throw new InvalidOperationException(
                    $"Required preserved presentation template '{path}' " +
                    "is missing. No roster assets were changed.");
            }
            templates.Add(type, template);
        }
        return templates;
    }

    private static string ResolvePresentationTemplateFileName(
        EEnemyType type)
    {
        string enemyId = type switch
        {
            EEnemyType.Basic => "G001",
            EEnemyType.Assault => "G002",
            EEnemyType.Heavy => "G003",
            EEnemyType.Medic => "S001",
            EEnemyType.Mechanic => "S002",
            EEnemyType.Infiltrator => "S003",
            EEnemyType.Pointman => "S004",
            EEnemyType.ShieldBearer => "S005",
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Unsupported presentation-template enemy type."),
        };

        return PreservedAssetFileNames[enemyId];
    }

    private static void PopulateEnemy(
        EnemySO enemy,
        EnemySpec spec,
        EnemySO template)
    {
        SerializedObject serialized = new(enemy);
        SetString(serialized, "enemyId", spec.Id);
        SetString(serialized, "nameLocalizationKey", spec.NameKey);
        SetString(
            serialized,
            "descriptionLocalizationKey",
            spec.DescriptionKey);
        SetString(serialized, "displayName", spec.EnglishName);
        SetString(
            serialized,
            "description",
            spec.EnglishAbilityDescription);
        SetString(serialized, "cardCode", spec.Id);
        SetEnum(serialized, "grade", spec.Grade.ToString());
        SetEnum(serialized, "type", spec.LegacyType.ToString());

        SetInt(
            serialized,
            "rosterSchemaVersion",
            EnemySO.CurrentRosterSchemaVersion);
        SetEnum(serialized, "rosterTier", spec.Tier.ToString());
        PopulateStringList(
            RequireProperty(serialized, "roleTags"),
            spec.RoleTags);
        PopulateStringList(
            RequireProperty(serialized, "counterTags"),
            spec.CounterTags);
        SetInt(
            serialized,
            "recommendedMaxPerWave",
            spec.RecommendedMaxPerWave);
        SetFloat(serialized, "spawnBudget", spec.SpawnBudget);
        SetBool(serialized, "encounterOnly", spec.EncounterOnly);

        SetObject(serialized, "iconSprite", template.IconSprite);
        SetObject(serialized, "boardSprite", template.BoardSprite);
        SetBool(
            serialized,
            "boardSpriteFacesRight",
            template.BoardSpriteFacesRight);
        SetInt(serialized, "sortOrder", spec.SortOrder);
        SetObject(serialized, "spawnVfxCue", template.SpawnVfxCue);
        SetObject(serialized, "deathVfxCue", template.DeathVfxCue);

        SetInt(serialized, "baseHealth", spec.BaseHealth);
        SetFloat(serialized, "healthScale", 1f);
        SetInt(serialized, "initialArmor", 0);
        SetInt(serialized, "initialShield", 0);
        SetFloat(serialized, "spawnIntervalMultiplier", 1f);
        SetFloat(serialized, "approachSpeed", spec.ApproachSpeed);
        SetFloat(serialized, "formationRadius", spec.FormationRadius);
        SetFloat(
            serialized,
            "forwardSearchAngle",
            spec.ForwardSearchAngle);
        SetInt(
            serialized,
            "combatStatSchemaVersion",
            EnemySO.CurrentCombatStatSchemaVersion);
        SetFloat(
            serialized,
            "attackPower",
            spec.PreciseCoreAttackDamage);
        SetInt(
            serialized,
            "coreAttackDamage",
            spec.LegacyCoreAttackDamage);
        SetEnum(
            serialized,
            "coreAttackDamagePolicy",
            spec.UsesFractionalCoreDamage
                ? "AccumulateFraction"
                : "LegacyInteger");
        SetFloat(
            serialized,
            "preciseCoreAttackDamage",
            spec.PreciseCoreAttackDamage);
        SetFloat(
            serialized,
            "coreAttackInterval",
            spec.CoreAttackInterval);
        SetFloat(serialized, "coreAttackRange", spec.CoreAttackRange);
        SetFloat(serialized, "threatCost", spec.SpawnBudget);
        SetInt(serialized, "unlockDifficulty", spec.UnlockDifficulty);
        SetInt(serialized, "footprintWidth", spec.IsBoss ? 2 : 1);
        SetInt(serialized, "footprintHeight", spec.IsBoss ? 2 : 1);
        SetEnum(
            serialized,
            "stackingPolicy",
            spec.IsBoss ? "Exclusive" : "Stackable");

        SerializedProperty abilities =
            RequireProperty(serialized, "abilities");
        abilities.arraySize = spec.Abilities.Count;
        for (int index = 0; index < spec.Abilities.Count; index++)
        {
            PopulateAbility(
                abilities.GetArrayElementAtIndex(index),
                spec,
                spec.Abilities[index]);
        }
        PopulateBossPhases(
            RequireProperty(serialized, "phaseDefinitions"),
            spec);

        if (!serialized.ApplyModifiedPropertiesWithoutUndo())
        {
            throw new InvalidOperationException(
                $"No serialized values were applied to enemy {spec.Id}.");
        }
        EditorUtility.SetDirty(enemy);
    }

    private static void PopulateBossPhases(
        SerializedProperty phases,
        EnemySpec spec)
    {
        if (!spec.IsBoss)
        {
            phases.arraySize = 0;
            return;
        }

        phases.arraySize = 3;
        PopulateBossPhase(
            phases.GetArrayElementAtIndex(0),
            "P1",
            "enemy.catalog.b001.phase.p1.name",
            "March Before the Gate",
            66,
            100,
            true,
            new[]
            {
                "b001.p1.summon",
                "b001.p1.control_aura",
            });
        PopulateBossPhase(
            phases.GetArrayElementAtIndex(1),
            "P2",
            "enemy.catalog.b001.phase.p2.name",
            "Memory Erosion",
            31,
            65,
            false,
            new[]
            {
                "b001.p2.erosion",
                "b001.p2.death_heal",
                "b001.p2.maximum_health",
            });
        PopulateBossPhase(
            phases.GetArrayElementAtIndex(2),
            "P3",
            "enemy.catalog.b001.phase.p3.name",
            "Gate Opening",
            0,
            30,
            false,
            new[]
            {
                "b001.p3.charge",
                "b001.p3.aura",
            });
    }

    private static void PopulateBossPhase(
        SerializedProperty phase,
        string phaseId,
        string nameKey,
        string fallbackName,
        int minimumHealthPercent,
        int maximumHealthPercent,
        bool advanceOnCoreContact,
        IReadOnlyList<string> abilityIds)
    {
        SetString(phase, "phaseId", phaseId);
        SetString(phase, "nameLocalizationKey", nameKey);
        SetString(phase, "fallbackName", fallbackName);
        SetInt(phase, "minimumHealthPercent", minimumHealthPercent);
        SetInt(phase, "maximumHealthPercent", maximumHealthPercent);
        SetBool(phase, "advanceOnCoreContact", advanceOnCoreContact);
        PopulateStringList(
            RequireRelative(phase, "abilityIds"),
            abilityIds);
    }

    private static void PopulateAbility(
        SerializedProperty property,
        EnemySpec enemy,
        AbilitySpec ability)
    {
        SetString(property, "abilityId", ability.Id);
        SetString(property, "nameLocalizationKey", enemy.AbilityNameKey);
        SetString(
            property,
            "descriptionLocalizationKey",
            enemy.AbilityDescriptionKey);
        SetString(property, "abilityTypeId", ability.TypeId);
        SetString(property, "fallbackName", ability.FallbackName);
        SetString(
            property,
            "fallbackDescription",
            ability.FallbackDescription);
        SetEnum(property, "trigger", ability.Trigger);
        SerializedProperty triggerEvents =
            RequireRelative(property, "triggerEvents");
        triggerEvents.arraySize = ability.TriggerEvents.Count;
        for (int index = 0; index < ability.TriggerEvents.Count; index++)
        {
            SetEnumValue(
                triggerEvents.GetArrayElementAtIndex(index),
                ability.TriggerEvents[index]);
        }
        SetInt(property, "priority", 0);
        SetFloat(property, "cooldown", ability.Cooldown);
        SetEnum(
            property,
            "cooldownResetPolicy",
            "OnSuccessfulActivation");
        SetBool(property, "pauseCooldownWhileDisabled", true);
        SetInt(property, "initialCharges", ability.InitialCharges);
        SetEnum(
            property,
            "chargeConsumptionPolicy",
            "OnSuccessfulActivation");
        SetEnum(property, "conditionMatchMode", "All");
        PopulateConditions(
            RequireRelative(property, "conditions"),
            ability.Conditions);
        SetFloat(
            property,
            "healthThresholdPercent",
            ability.HealthThresholdPercent);
        SetFloat(
            property,
            "noDamageDuration",
            ability.NoDamageDuration);
        SerializedProperty cooldownOverrides =
            RequireRelative(property, "cooldownOverrides");
        cooldownOverrides.arraySize = ability.CooldownOverrides.Count;
        for (int index = 0;
             index < ability.CooldownOverrides.Count;
             index++)
        {
            CooldownOverrideSpec cooldownOverride =
                ability.CooldownOverrides[index];
            SerializedProperty item =
                cooldownOverrides.GetArrayElementAtIndex(index);
            SetFloat(
                item,
                "healthAtOrBelowPercent",
                cooldownOverride.HealthAtOrBelowPercent);
            SetFloat(item, "cooldown", cooldownOverride.Cooldown);
        }

        PopulateParameters(
            RequireRelative(property, "parameters"),
            ability.Parameters);
        PopulateTarget(
            RequireRelative(property, "target"),
            ability.Target);
        PopulateCharge(
            RequireRelative(property, "charge"),
            ability.Charge);
        PopulateTelegraph(
            RequireRelative(property, "telegraph"),
            ability.Telegraph);

        SerializedProperty operations =
            RequireRelative(property, "operations");
        operations.arraySize = ability.Operations.Count;
        for (int index = 0; index < ability.Operations.Count; index++)
        {
            PopulateOperation(
                operations.GetArrayElementAtIndex(index),
                ability.Operations[index]);
        }
    }

    private static void PopulateParameters(
        SerializedProperty property,
        IReadOnlyList<ParameterSpec> parameters)
    {
        property.arraySize = parameters.Count;
        for (int index = 0; index < parameters.Count; index++)
        {
            ParameterSpec parameter = parameters[index];
            SerializedProperty item = property.GetArrayElementAtIndex(index);
            SetString(item, "key", parameter.Key);
            SetEnum(item, "valueType", parameter.ValueType);
            SetFloat(item, "floatValue", parameter.FloatValue);
            SetInt(item, "intValue", parameter.IntValue);
            SetBool(item, "boolValue", parameter.BoolValue);
            SetString(item, "textValue", parameter.TextValue);
            SerializedProperty enemyReference =
                RequireRelative(item, "enemyReference");
            SetObject(enemyReference, "enemy", null);
            SetString(
                enemyReference,
                "enemyId",
                parameter.ValueType == "EnemyReference"
                    ? parameter.TextValue
                    : string.Empty);
        }
    }

    private static void PopulateConditions(
        SerializedProperty property,
        IReadOnlyList<ConditionSpec> conditions)
    {
        property.arraySize = conditions.Count;
        for (int index = 0; index < conditions.Count; index++)
        {
            ConditionSpec condition = conditions[index];
            SerializedProperty item = property.GetArrayElementAtIndex(index);
            SetEnum(item, "type", condition.Type);
            SetEnum(item, "comparison", "GreaterThanOrEqual");
            SetFloat(item, "threshold", 0f);
            StatusEffectSO status =
                string.IsNullOrEmpty(condition.StatusAssetName)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<StatusEffectSO>(
                        "Assets/06_Runtime/Resources/StatusEffects/" +
                        condition.StatusAssetName + ".asset");
            if (!string.IsNullOrEmpty(condition.StatusAssetName) &&
                status == null)
            {
                throw new InvalidOperationException(
                    $"Required status asset " +
                    $"'{condition.StatusAssetName}' is missing.");
            }
            SetObject(item, "statusEffect", status);
            RequireRelative(item, "statusEffects").arraySize = 0;
            SetEnum(item, "statusSelectionScope", "SelectedStatuses");
            SetEnum(item, "statusMatchMode", "Any");
            SetInt(item, "statusMatchCount", 1);
            SetEnum(item, "incomingDamageType", "Physical");
            SetBool(item, "expected", condition.Expected);
            SetFloat(item, "windowDuration", condition.WindowDuration);
        }
    }

    private static void PopulateTarget(
        SerializedProperty property,
        TargetSpec target)
    {
        target ??= TargetSpec.None();
        SetEnum(property, "faction", target.Faction);
        SetEnum(property, "subject", target.Subject);
        SetEnum(property, "metric", target.Metric);
        SetInt(property, "targetCount", Mathf.Max(1, target.Count));
        SetInt(property, "range", 1);
        SetBool(property, "includeDiagonals", false);
        SetFloat(property, "worldRadius", target.WorldRadius);
        SetBool(property, "includeSource", target.IncludeSource);
        SetEnum(property, "layerScope", target.LayerScope);
    }

    private static void PopulateCharge(
        SerializedProperty property,
        ChargeSpec charge)
    {
        charge ??= new ChargeSpec();
        SetBool(property, "enabled", charge.Enabled);
        SetFloat(property, "duration", charge.Duration);
        SetBool(property, "interruptible", charge.Interruptible);
        SetEnumFlags(property, "interrupts", charge.InterruptFlags);
    }

    private static void PopulateTelegraph(
        SerializedProperty property,
        TelegraphSpec telegraph)
    {
        telegraph ??= new TelegraphSpec();
        SetBool(property, "enabled", telegraph.Enabled);
        SetFloat(property, "leadTime", telegraph.LeadTime);
        SetString(property, "cueId", telegraph.CueId);
        SetFloat(property, "worldRadius", telegraph.WorldRadius);
    }

    private static void PopulateOperation(
        SerializedProperty property,
        OperationSpec operation)
    {
        SetEnum(property, "type", operation.Type);
        SetFloat(property, "multiplier", operation.Multiplier);
        SetInt(property, "amount", operation.Amount);
        SetInt(property, "count", operation.Count);
        SetInt(property, "range", 1);
        SetBool(property, "includeDiagonals", false);
        SetBool(property, "enabled", true);
        SetEnum(
            property,
            "targetPriorityMode",
            operation.TargetPriorityMode);
        SetInt(property, "targetPriorityAdjustment", 0);
        SetFloat(property, "duration", operation.Duration);
        SetFloat(property, "interval", operation.Interval);
        SetFloat(property, "worldRadius", operation.WorldRadius);
        SetFloat(property, "percentage", operation.Percentage);
        SetInt(property, "maximumStacks", operation.MaximumStacks);
        SetString(
            property,
            "referencedAbilityId",
            operation.ReferencedAbilityId);
        SetString(property, "sourceId", operation.SourceId);

        SerializedProperty effects = RequireRelative(property, "effects");
        effects.arraySize = operation.Effects.Count;
        for (int index = 0; index < operation.Effects.Count; index++)
        {
            PopulateEffect(
                effects.GetArrayElementAtIndex(index),
                operation.Effects[index]);
        }

        SerializedProperty reference =
            RequireRelative(property, "reference");
        SetObject(reference, "enemy", null);
        SetString(reference, "enemyId", operation.ReferenceEnemyId);
        PopulateSummon(
            RequireRelative(property, "summon"),
            operation.Summon);
    }

    private static void PopulateSummon(
        SerializedProperty property,
        EnemySummonSpec summon)
    {
        SerializedProperty candidates =
            RequireRelative(property, "candidates");
        if (summon == null)
        {
            candidates.arraySize = 0;
            SetInt(property, "minimumCount", 1);
            SetInt(property, "maximumCount", 1);
            SetInt(property, "maximumActive", 0);
            SetBool(property, "allowRecursiveSummon", false);
            SetBool(property, "inheritFormationLayer", true);
            SetFloat(property, "childHealthMultiplier", 1f);
            SetFloat(property, "childCoreAttackMultiplier", 1f);
            return;
        }

        candidates.arraySize = summon.CandidateIds.Count;
        for (int index = 0; index < summon.CandidateIds.Count; index++)
        {
            SerializedProperty candidate =
                candidates.GetArrayElementAtIndex(index);
            SetObject(candidate, "enemy", null);
            SetString(
                candidate,
                "enemyId",
                summon.CandidateIds[index]);
        }
        SetInt(property, "minimumCount", summon.MinimumCount);
        SetInt(property, "maximumCount", summon.MaximumCount);
        SetInt(property, "maximumActive", summon.MaximumActive);
        SetBool(
            property,
            "allowRecursiveSummon",
            summon.AllowRecursiveSummon);
        SetBool(
            property,
            "inheritFormationLayer",
            summon.InheritFormationLayer);
        SetFloat(
            property,
            "childHealthMultiplier",
            summon.ChildHealthMultiplier);
        SetFloat(
            property,
            "childCoreAttackMultiplier",
            summon.ChildCoreAttackMultiplier);
    }

    private static void PopulateEffect(
        SerializedProperty property,
        EffectSpec effect)
    {
        SetString(property, "effectId", effect.Id);
        SetEnum(property, "type", effect.Type);
        SetEnum(property, "targetMode", "InheritAction");
        SetEnum(
            property,
            "preconditionFailurePolicy",
            "AbortAction");
        SetEnum(property, "failurePolicy", "Continue");

        SerializedProperty selector =
            RequireRelative(property, "targetSelector");
        SetEnum(selector, "targetFaction", "Enemy");
        SetEnum(selector, "subject", "Random");
        SetEnum(selector, "subjectMetric", "Health");
        SetInt(selector, "subjectCount", 1);
        SetEnum(selector, "conditionMatchMode", "Any");
        RequireRelative(selector, "numericConditions").arraySize = 0;
        RequireRelative(selector, "areaOffsets").arraySize = 0;

        SetEnum(property, "damageType", "Physical");
        SetEnum(property, "damageAmountMode", "Fixed");
        SetFloat(property, "damageAmount", effect.FixedAmount);
        SetFloat(property, "sourceResourceScale", 0f);
        SetFloat(property, "sourceCurrentHealthScale", 0f);
        SetFloat(property, "sourceMaxHealthScale", 0f);
        SetFloat(property, "targetCurrentHealthScale", 0f);
        SetFloat(
            property,
            "targetMaxHealthScale",
            effect.TargetMaximumHealthScale);
        SetObject(property, "sourceStatusScalingEffect", null);
        SetFloat(property, "sourceStatusStacksScale", 0f);
        SetObject(property, "targetStatusScalingEffect", null);
        SetFloat(property, "targetStatusStacksScale", 0f);
        RequireRelative(property, "statusContributionMultipliers")
            .arraySize = 0;

        StatusEffectSO status = string.IsNullOrEmpty(effect.StatusAssetName)
            ? null
            : AssetDatabase.LoadAssetAtPath<StatusEffectSO>(
                "Assets/06_Runtime/Resources/StatusEffects/" +
                effect.StatusAssetName + ".asset");
        if (!string.IsNullOrEmpty(effect.StatusAssetName) && status == null)
        {
            throw new InvalidOperationException(
                $"Required status asset '{effect.StatusAssetName}' is " +
                "missing.");
        }
        SetFloat(property, "statusDuration", effect.StatusDuration);
        SetFloat(property, "statusStacks", effect.StatusStacks);
        SetObject(property, "statusEffect", status);
        RequireRelative(property, "statusRemovalEffects").arraySize = 0;
        SetEnum(
            property,
            "statusRemovalTarget",
            effect.RemovesDebuffs ? "Debuff" : "Single");
        SetEnum(property, "statusRemovalPickMode", "AllMatches");
        SetInt(property, "statusRemovalPickCount", 99);
        SetEnum(property, "statusRemovalAmountMode", "FixedStacks");
        SetInt(property, "statusRemovalCount", 999);
        SetFloat(property, "statusRemovalRatio", 1f);
    }

    private static void ValidateAssets(
        IReadOnlyList<EnemySpec> specs,
        IReadOnlyList<string> paths,
        string label)
    {
        if (paths.Count != specs.Count)
        {
            throw new InvalidOperationException(
                $"Expected {specs.Count} {label} paths, found " +
                $"{paths.Count}.");
        }

        List<EnemySO> definitions = new(paths.Count);
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < paths.Count; index++)
        {
            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(paths[index]);
            EnemySpec spec = specs[index];
            if (definition == null ||
                definition.EnemyId != spec.Id ||
                definition.RosterSchemaVersion !=
                    EnemySO.CurrentRosterSchemaVersion ||
                definition.RosterTier != spec.Tier ||
                definition.Grade != spec.Grade ||
                definition.BaseHealth != spec.BaseHealth ||
                !Mathf.Approximately(
                    definition.CoreAttackDamageValue,
                    spec.PreciseCoreAttackDamage) ||
                definition.CoreAttackDamage !=
                    spec.LegacyCoreAttackDamage ||
                !Mathf.Approximately(
                    definition.CoreAttackInterval,
                    TimePrecision.Normalize(
                        spec.CoreAttackInterval,
                        0.1f)) ||
                !Mathf.Approximately(
                    definition.FormationRadius,
                    spec.FormationRadius) ||
                !Mathf.Approximately(
                    definition.ForwardSearchAngle,
                    spec.ForwardSearchAngle) ||
                !Mathf.Approximately(
                    definition.CoreAttackRange,
                    spec.CoreAttackRange) ||
                definition.RecommendedMaxPerWave !=
                    spec.RecommendedMaxPerWave ||
                definition.EncounterOnly != spec.EncounterOnly ||
                definition.Abilities.Count != spec.Abilities.Count ||
                definition.PhaseDefinitions.Count !=
                    (spec.IsBoss ? 3 : 0) ||
                !ids.Add(definition.EnemyId))
            {
                throw new InvalidOperationException(
                    $"{label} enemy {spec.Id} does not match its " +
                    "normalized source specification.");
            }

            if (!definition.RoleTags.SequenceEqual(spec.RoleTags) ||
                !definition.CounterTags.SequenceEqual(spec.CounterTags))
            {
                throw new InvalidOperationException(
                    $"{label} enemy {spec.Id} has mismatched roster tags.");
            }
            definitions.Add(definition);
        }

        EnemyDefinitionValidationResult validation =
            EnemyDefinitionValidator.ValidateAll(definitions);
        if (!validation.IsValid)
        {
            string errors = string.Join(
                Environment.NewLine,
                validation.Diagnostics
                    .Where(item =>
                        item.Severity ==
                            EnemyDefinitionDiagnosticSeverity.Error)
                    .Select(item => item.ToString()));
            throw new InvalidOperationException(
                $"The {label} roster failed EnemyDefinitionValidator:" +
                Environment.NewLine + errors);
        }
    }

    private static void CommitCatalog(
        IReadOnlyList<EnemySpec> specs,
        IReadOnlyList<string> stagingPaths)
    {
        List<string> existingPaths = FindDirectEnemyAssets(EnemyFolder);
        Dictionary<string, string> existingById = new(
            StringComparer.Ordinal);
        foreach (string path in existingPaths)
        {
            EnemySO existing = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (existing != null &&
                !string.IsNullOrWhiteSpace(existing.EnemyId))
            {
                existingById.TryAdd(existing.EnemyId, path);
            }
        }

        Dictionary<string, string> selectedExisting =
            new(StringComparer.Ordinal);
        HashSet<string> claimedPaths =
            new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> legacyGuids =
            new(StringComparer.Ordinal);
        foreach (EnemySpec spec in specs)
        {
            string desiredPath = $"{EnemyFolder}/{spec.FileName}";
            string existingPath = null;
            if (PreservedAssetFileNames.ContainsKey(spec.Id) &&
                AssetDatabase.LoadAssetAtPath<EnemySO>(desiredPath) != null)
            {
                existingPath = desiredPath;
                legacyGuids.Add(
                    spec.Id,
                    AssetDatabase.AssetPathToGUID(existingPath));
            }
            else if (existingById.TryGetValue(spec.Id, out string idPath))
            {
                existingPath = idPath;
            }
            else if (AssetDatabase.LoadAssetAtPath<EnemySO>(desiredPath) !=
                     null)
            {
                existingPath = desiredPath;
            }

            if (!string.IsNullOrEmpty(existingPath))
            {
                if (!claimedPaths.Add(existingPath))
                {
                    throw new InvalidOperationException(
                        $"Existing enemy '{existingPath}' was matched more " +
                        "than once. Existing assets were not changed.");
                }
                selectedExisting.Add(spec.Id, existingPath);
            }
            else
            {
                UnityEngine.Object occupied =
                    AssetDatabase.LoadMainAssetAtPath(desiredPath);
                if (occupied != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot create '{desiredPath}' because it is " +
                        "occupied by a non-roster asset.");
                }
            }
        }

        EnsureFolder(BackupFolder);
        List<OriginalAssetState> modified = new();
        List<MoveRecord> newMoves = new();
        List<MoveRecord> obsoleteMoves = new();
        try
        {
            for (int index = 0; index < specs.Count; index++)
            {
                EnemySpec spec = specs[index];
                if (!selectedExisting.TryGetValue(
                        spec.Id,
                        out string existingPath))
                {
                    continue;
                }

                EnemySO existing =
                    AssetDatabase.LoadAssetAtPath<EnemySO>(existingPath);
                EnemySO staged =
                    AssetDatabase.LoadAssetAtPath<EnemySO>(stagingPaths[index]);
                if (existing == null || staged == null)
                {
                    throw new InvalidOperationException(
                        $"Could not load existing or staged enemy {spec.Id}.");
                }

                EnemySO backup = UnityEngine.Object.Instantiate(existing);
                backup.hideFlags = HideFlags.HideAndDontSave;
                modified.Add(new OriginalAssetState(
                    existing,
                    backup,
                    existing.name));
                EditorUtility.CopySerialized(staged, existing);
                existing.name = Path.GetFileNameWithoutExtension(existingPath);
                EditorUtility.SetDirty(existing);
            }

            foreach (string obsoletePath in existingPaths)
            {
                if (claimedPaths.Contains(obsoletePath))
                    continue;
                string backupPath =
                    $"{BackupFolder}/{Path.GetFileName(obsoletePath)}";
                MoveAssetChecked(obsoletePath, backupPath);
                obsoleteMoves.Add(new MoveRecord(obsoletePath, backupPath));
            }

            for (int index = 0; index < specs.Count; index++)
            {
                EnemySpec spec = specs[index];
                if (selectedExisting.ContainsKey(spec.Id))
                    continue;
                string destination = $"{EnemyFolder}/{spec.FileName}";
                MoveAssetChecked(stagingPaths[index], destination);
                newMoves.Add(new MoveRecord(stagingPaths[index], destination));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string[] livePaths = specs
                .Select(spec => selectedExisting.TryGetValue(
                    spec.Id,
                    out string existingPath)
                        ? existingPath
                        : $"{EnemyFolder}/{spec.FileName}")
                .ToArray();
            ValidateAssets(specs, livePaths, "live");

            foreach (KeyValuePair<string, string> pair in legacyGuids)
            {
                string livePath = selectedExisting[pair.Key];
                string currentGuid = AssetDatabase.AssetPathToGUID(livePath);
                if (!string.Equals(
                        pair.Value,
                        currentGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Legacy GUID changed for {pair.Key}.");
                }
            }

            if (AssetDatabase.IsValidFolder(StagingFolder) &&
                !AssetDatabase.DeleteAsset(StagingFolder))
            {
                throw new InvalidOperationException(
                    "Could not remove the verified staging folder.");
            }
            if (!AssetDatabase.DeleteAsset(BackupFolder))
            {
                throw new InvalidOperationException(
                    "Could not remove the verified obsolete-enemy backup.");
            }
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
                for (int index = obsoleteMoves.Count - 1;
                     index >= 0;
                     index--)
                {
                    MoveRecord move = obsoleteMoves[index];
                    if (AssetDatabase.LoadMainAssetAtPath(move.To) != null)
                        MoveAssetChecked(move.To, move.From);
                }
                foreach (OriginalAssetState original in modified)
                {
                    EditorUtility.CopySerialized(
                        original.Backup,
                        original.Asset);
                    original.Asset.name = original.Name;
                    EditorUtility.SetDirty(original.Asset);
                }
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
                    "Enemy roster commit and rollback both failed. " +
                    $"Recovery assets remain in '{BackupFolder}'.",
                    commitException,
                    rollbackException);
            }
            throw;
        }
        finally
        {
            foreach (OriginalAssetState original in modified)
            {
                if (original.Backup != null)
                    UnityEngine.Object.DestroyImmediate(original.Backup);
            }
        }
    }

    private static List<string> FindDirectEnemyAssets(string folder)
    {
        List<string> paths = new();
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:EnemySO",
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

    private static float ResolveFormationRadius(EnemySpec spec)
    {
        if (spec.IsBoss)
            return 0.9f;
        if (spec.Tier == EnemyRosterTier.Elite)
            return 0.55f;
        if (spec.Id == "G008")
            return 0.22f;
        if (spec.HealthMultiplier >= 2f)
            return 0.48f;
        if (spec.HealthMultiplier >= 1.4f ||
            spec.LegacyType == EEnemyType.Heavy ||
            spec.LegacyType == EEnemyType.ShieldBearer)
        {
            return 0.43f;
        }
        if (spec.MoveSpeedMultiplier >= 1.05f ||
            spec.HealthMultiplier <= 0.75f)
        {
            return 0.30f;
        }
        return 0.35f;
    }

    private static float ResolveCoreAttackRange(EnemySpec spec)
    {
        return spec.Id switch
        {
            "G014" or "G019" or "G024" or "G028" or "G030" or
            "S001" or "S002" or "S004" or "S005" or "S006" or
            "S007" or "E002" or "E003" or "E005" => 0.6f,
            _ => 0f,
        };
    }

    private static int ResolveRecommendedMaxPerWave(EnemySpec spec)
    {
        if (spec.AuthoredRecommendedMaxPerWave >= 0)
            return spec.AuthoredRecommendedMaxPerWave;
        if (spec.Tier == EnemyRosterTier.Boss ||
            spec.Tier == EnemyRosterTier.Elite ||
            spec.Tier == EnemyRosterTier.Special)
        {
            return 1;
        }
        return IsSupportRole(spec) ? 2 : 0;
    }

    private static bool IsSupportRole(EnemySpec spec)
    {
        string role = spec.Role;
        return role.Contains("support", StringComparison.Ordinal) ||
               role.Contains("healer", StringComparison.Ordinal) ||
               role.Contains("aura", StringComparison.Ordinal) ||
               role.Contains("accelerator", StringComparison.Ordinal) ||
               role.Contains("disruption", StringComparison.Ordinal) ||
               role.Contains("control", StringComparison.Ordinal) ||
               role.Contains("summon", StringComparison.Ordinal) ||
               role.Contains("expansion", StringComparison.Ordinal) ||
               role.Contains("leader", StringComparison.Ordinal) ||
               role.Contains("link", StringComparison.Ordinal) ||
               role.Contains("tempo", StringComparison.Ordinal);
    }

    private static float ResolveSpawnBudget(EnemySpec spec)
    {
        float budget =
            spec.HealthMultiplier * 0.45f +
            spec.CoreAttackMultiplier * 0.35f +
            (1f / spec.AttackPeriodMultiplier) * 0.10f +
            spec.MoveSpeedMultiplier * 0.10f;
        return Mathf.Max(0.25f, Mathf.Round(budget * 100f) / 100f);
    }

    private static string ResolveFileName(EnemySpec spec)
    {
        if (PreservedAssetFileNames.TryGetValue(
                spec.Id,
                out string preservedFileName))
        {
            return preservedFileName;
        }
        string safeName = new(
            spec.EnglishName.Where(char.IsLetterOrDigit).ToArray());
        return $"{spec.Id}_{safeName}.asset";
    }

    private static List<ParameterSpec> ParseParameters(string raw)
    {
        List<ParameterSpec> result = new();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (string token in raw.Split(';'))
        {
            int colon = token.IndexOf(':');
            int equals = token.IndexOf('=');
            if (colon != 1 || equals <= colon + 1)
            {
                throw new InvalidOperationException(
                    $"Invalid typed enemy parameter '{token}'.");
            }
            string key = token.Substring(colon + 1, equals - colon - 1);
            string value = token.Substring(equals + 1);
            ParameterSpec parameter = token[0] switch
            {
                'f' when TryFloat(value, out float floatValue) =>
                    ParameterSpec.Float(key, floatValue),
                'i' when int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int intValue) =>
                    ParameterSpec.Integer(key, intValue),
                'b' when bool.TryParse(value, out bool boolValue) =>
                    ParameterSpec.Boolean(key, boolValue),
                't' => ParameterSpec.Text(key, value),
                'e' => ParameterSpec.EnemyReference(key, value),
                _ => throw new InvalidOperationException(
                    $"Invalid typed enemy parameter '{token}'."),
            };
            result.Add(parameter);
        }
        return result;
    }

    private static List<string> SplitCsv(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
    }

    private static bool TryFloat(string value, out float result)
    {
        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static int RoundHalfUp(float value)
    {
        return Mathf.Max(1, Mathf.FloorToInt(value + 0.5f));
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string[] words = value.Split('_');
        for (int index = 0; index < words.Length; index++)
        {
            if (words[index].Length == 0)
                continue;
            words[index] = char.ToUpperInvariant(words[index][0]) +
                           words[index].Substring(1);
        }
        return string.Join(" ", words);
    }

    private static string RenderEnemyYaml(
        EnemySpec spec,
        YamlPresentationTemplate presentation)
    {
        StringBuilder yaml = new(16384);
        yaml.AppendLine("%YAML 1.1");
        yaml.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        yaml.AppendLine("--- !u!114 &11400000");
        yaml.AppendLine("MonoBehaviour:");
        AppendYaml(yaml, 2, "m_ObjectHideFlags: 0");
        AppendYaml(yaml, 2, "m_CorrespondingSourceObject: {fileID: 0}");
        AppendYaml(yaml, 2, "m_PrefabInstance: {fileID: 0}");
        AppendYaml(yaml, 2, "m_PrefabAsset: {fileID: 0}");
        AppendYaml(yaml, 2, "m_GameObject: {fileID: 0}");
        AppendYaml(yaml, 2, "m_Enabled: 1");
        AppendYaml(yaml, 2, "m_EditorHideFlags: 0");
        AppendYaml(
            yaml,
            2,
            $"m_Script: {{fileID: 11500000, guid: {EnemyScriptGuid}, " +
            "type: 3}");
        AppendYaml(
            yaml,
            2,
            $"m_Name: {YamlString(Path.GetFileNameWithoutExtension(spec.FileName))}");
        AppendYaml(
            yaml,
            2,
            "m_EditorClassIdentifier: Assembly-CSharp::EnemySO");
        AppendYaml(yaml, 2, $"enemyId: {YamlString(spec.Id)}");
        AppendYaml(
            yaml,
            2,
            $"nameLocalizationKey: {YamlString(spec.NameKey)}");
        AppendYaml(
            yaml,
            2,
            $"descriptionLocalizationKey: {YamlString(spec.DescriptionKey)}");
        AppendYaml(
            yaml,
            2,
            $"displayName: {YamlString(spec.EnglishName)}");
        AppendYaml(
            yaml,
            2,
            $"description: {YamlString(spec.EnglishAbilityDescription)}");
        AppendYaml(yaml, 2, $"cardCode: {YamlString(spec.Id)}");
        AppendYaml(yaml, 2, $"grade: {(int)spec.Grade}");
        AppendYaml(yaml, 2, $"type: {(int)spec.LegacyType}");
        AppendYaml(yaml, 2, "rosterSchemaVersion: 1");
        AppendYaml(yaml, 2, $"rosterTier: {(int)spec.Tier}");
        AppendYamlStringList(yaml, 2, "roleTags", spec.RoleTags);
        AppendYamlStringList(yaml, 2, "counterTags", spec.CounterTags);
        AppendYaml(
            yaml,
            2,
            $"recommendedMaxPerWave: {spec.RecommendedMaxPerWave}");
        AppendYaml(yaml, 2, $"spawnBudget: {YamlFloat(spec.SpawnBudget)}");
        AppendYaml(yaml, 2, $"encounterOnly: {YamlBool(spec.EncounterOnly)}");
        AppendYaml(yaml, 2, $"iconSprite: {presentation.IconSprite}");
        AppendYaml(yaml, 2, $"boardSprite: {presentation.BoardSprite}");
        AppendYaml(
            yaml,
            2,
            $"boardSpriteFacesRight: {presentation.BoardSpriteFacesRight}");
        AppendYaml(yaml, 2, $"sortOrder: {spec.SortOrder}");
        AppendYaml(yaml, 2, $"spawnVfxCue: {presentation.SpawnVfxCue}");
        AppendYaml(yaml, 2, $"deathVfxCue: {presentation.DeathVfxCue}");
        AppendYaml(yaml, 2, $"baseHealth: {spec.BaseHealth}");
        AppendYaml(yaml, 2, "healthScale: 1");
        AppendYaml(yaml, 2, "initialArmor: 0");
        AppendYaml(yaml, 2, "initialShield: 0");
        AppendYaml(yaml, 2, "spawnIntervalMultiplier: 1");
        AppendYaml(yaml, 2, $"approachSpeed: {YamlFloat(spec.ApproachSpeed)}");
        AppendYaml(
            yaml,
            2,
            $"formationRadius: {YamlFloat(spec.FormationRadius)}");
        AppendYaml(
            yaml,
            2,
            $"forwardSearchAngle: {YamlFloat(spec.ForwardSearchAngle)}");
        AppendYaml(
            yaml,
            2,
            $"combatStatSchemaVersion: " +
            $"{EnemySO.CurrentCombatStatSchemaVersion}");
        AppendYaml(
            yaml,
            2,
            $"attackPower: {YamlFloat(spec.PreciseCoreAttackDamage)}");
        AppendYaml(
            yaml,
            2,
            $"coreAttackDamage: {spec.LegacyCoreAttackDamage}");
        AppendYaml(
            yaml,
            2,
            $"coreAttackDamagePolicy: {(spec.UsesFractionalCoreDamage ? 1 : 0)}");
        AppendYaml(
            yaml,
            2,
            $"preciseCoreAttackDamage: {YamlFloat(spec.PreciseCoreAttackDamage)}");
        AppendYaml(
            yaml,
            2,
            $"coreAttackInterval: {YamlFloat(spec.CoreAttackInterval)}");
        AppendYaml(
            yaml,
            2,
            $"coreAttackRange: {YamlFloat(spec.CoreAttackRange)}");
        AppendYaml(yaml, 2, $"threatCost: {YamlFloat(spec.SpawnBudget)}");
        AppendYaml(yaml, 2, $"unlockDifficulty: {spec.UnlockDifficulty}");
        AppendYaml(yaml, 2, $"footprintWidth: {(spec.IsBoss ? 2 : 1)}");
        AppendYaml(yaml, 2, $"footprintHeight: {(spec.IsBoss ? 2 : 1)}");
        AppendYaml(yaml, 2, $"stackingPolicy: {(spec.IsBoss ? 1 : 0)}");
        AppendYamlAbilities(yaml, spec);
        AppendYamlBossPhases(yaml, spec);
        return yaml.ToString();
    }

    private static void AppendYamlAbilities(
        StringBuilder yaml,
        EnemySpec enemy)
    {
        if (enemy.Abilities.Count == 0)
        {
            AppendYaml(yaml, 2, "abilities: []");
            return;
        }
        AppendYaml(yaml, 2, "abilities:");
        foreach (AbilitySpec ability in enemy.Abilities)
        {
            AppendYaml(
                yaml,
                2,
                $"- abilityId: {YamlString(ability.Id)}");
            AppendYaml(
                yaml,
                4,
                $"nameLocalizationKey: {YamlString(enemy.AbilityNameKey)}");
            AppendYaml(
                yaml,
                4,
                "descriptionLocalizationKey: " +
                YamlString(enemy.AbilityDescriptionKey));
            AppendYaml(
                yaml,
                4,
                $"abilityTypeId: {YamlString(ability.TypeId)}");
            AppendYamlParameters(yaml, ability.Parameters, 4);
            AppendYaml(
                yaml,
                4,
                $"fallbackName: {YamlString(ability.FallbackName)}");
            AppendYaml(
                yaml,
                4,
                "fallbackDescription: " +
                YamlString(ability.FallbackDescription));
            AppendYaml(
                yaml,
                4,
                $"trigger: {EnumNumber<EnemyAbilityTrigger>(ability.Trigger)}");
            AppendYamlEnumList<EnemyAbilityTrigger>(
                yaml,
                4,
                "triggerEvents",
                ability.TriggerEvents);
            AppendYaml(yaml, 4, "priority: 0");
            AppendYaml(yaml, 4, $"cooldown: {YamlFloat(ability.Cooldown)}");
            AppendYamlCooldownOverrides(yaml, ability.CooldownOverrides);
            AppendYaml(yaml, 4, "cooldownResetPolicy: 0");
            AppendYaml(yaml, 4, "pauseCooldownWhileDisabled: 1");
            AppendYaml(yaml, 4, $"initialCharges: {ability.InitialCharges}");
            AppendYaml(yaml, 4, "chargeConsumptionPolicy: 0");
            AppendYaml(yaml, 4, "conditionMatchMode: 0");
            AppendYamlConditions(yaml, ability.Conditions);
            AppendYamlTarget(yaml, ability.Target, 4);
            AppendYamlOperations(yaml, ability.Operations);
            AppendYaml(
                yaml,
                4,
                "healthThresholdPercent: " +
                YamlFloat(ability.HealthThresholdPercent));
            AppendYaml(
                yaml,
                4,
                $"noDamageDuration: {YamlFloat(ability.NoDamageDuration)}");
            AppendYamlCharge(yaml, ability.Charge);
            AppendYamlTelegraph(yaml, ability.Telegraph);
        }
    }

    private static void AppendYamlParameters(
        StringBuilder yaml,
        IReadOnlyList<ParameterSpec> parameters,
        int indent)
    {
        if (parameters.Count == 0)
        {
            AppendYaml(yaml, indent, "parameters: []");
            return;
        }
        AppendYaml(yaml, indent, "parameters:");
        foreach (ParameterSpec parameter in parameters)
        {
            AppendYaml(yaml, indent, $"- key: {YamlString(parameter.Key)}");
            AppendYaml(
                yaml,
                indent + 2,
                "valueType: " +
                EnumNumber<EnemyAbilityParameterValueType>(
                    parameter.ValueType));
            AppendYaml(
                yaml,
                indent + 2,
                $"floatValue: {YamlFloat(parameter.FloatValue)}");
            AppendYaml(yaml, indent + 2, $"intValue: {parameter.IntValue}");
            AppendYaml(
                yaml,
                indent + 2,
                $"boolValue: {YamlBool(parameter.BoolValue)}");
            AppendYaml(
                yaml,
                indent + 2,
                $"textValue: {YamlString(parameter.TextValue)}");
            AppendYaml(yaml, indent + 2, "enemyReference:");
            AppendYaml(yaml, indent + 4, "enemy: {fileID: 0}");
            AppendYaml(
                yaml,
                indent + 4,
                "enemyId: " + YamlString(
                    parameter.ValueType == "EnemyReference"
                        ? parameter.TextValue
                        : string.Empty));
        }
    }

    private static void AppendYamlCooldownOverrides(
        StringBuilder yaml,
        IReadOnlyList<CooldownOverrideSpec> overrides)
    {
        if (overrides.Count == 0)
        {
            AppendYaml(yaml, 4, "cooldownOverrides: []");
            return;
        }
        AppendYaml(yaml, 4, "cooldownOverrides:");
        foreach (CooldownOverrideSpec item in overrides)
        {
            AppendYaml(
                yaml,
                4,
                "- healthAtOrBelowPercent: " +
                YamlFloat(item.HealthAtOrBelowPercent));
            AppendYaml(yaml, 6, $"cooldown: {YamlFloat(item.Cooldown)}");
        }
    }

    private static void AppendYamlConditions(
        StringBuilder yaml,
        IReadOnlyList<ConditionSpec> conditions)
    {
        if (conditions.Count == 0)
        {
            AppendYaml(yaml, 4, "conditions: []");
            return;
        }
        AppendYaml(yaml, 4, "conditions:");
        foreach (ConditionSpec condition in conditions)
        {
            AppendYaml(
                yaml,
                4,
                "- type: " +
                EnumNumber<EnemyAbilityConditionType>(condition.Type));
            AppendYaml(yaml, 6, "comparison: 0");
            AppendYaml(yaml, 6, "threshold: 0");
            string statusReference =
                string.IsNullOrEmpty(condition.StatusAssetName)
                    ? "{fileID: 0}"
                    : $"{{fileID: 11400000, guid: {StunStatusGuid}, type: 2}}";
            AppendYaml(yaml, 6, $"statusEffect: {statusReference}");
            AppendYaml(yaml, 6, "statusEffects: []");
            AppendYaml(yaml, 6, "statusSelectionScope: 0");
            AppendYaml(yaml, 6, "statusMatchMode: 0");
            AppendYaml(yaml, 6, "statusMatchCount: 1");
            AppendYaml(yaml, 6, "incomingDamageType: 0");
            AppendYaml(yaml, 6, $"expected: {YamlBool(condition.Expected)}");
            AppendYaml(
                yaml,
                6,
                $"windowDuration: {YamlFloat(condition.WindowDuration)}");
        }
    }

    private static void AppendYamlTarget(
        StringBuilder yaml,
        TargetSpec target,
        int indent)
    {
        target ??= TargetSpec.None();
        AppendYaml(yaml, indent, "target:");
        AppendYaml(
            yaml,
            indent + 2,
            $"faction: {EnumNumber<EnemyAbilityTargetFaction>(target.Faction)}");
        AppendYaml(
            yaml,
            indent + 2,
            $"subject: {EnumNumber<EnemyAbilityTargetSubject>(target.Subject)}");
        AppendYaml(
            yaml,
            indent + 2,
            $"metric: {EnumNumber<EnemyAbilityTargetMetric>(target.Metric)}");
        AppendYaml(yaml, indent + 2, $"targetCount: {Mathf.Max(1, target.Count)}");
        AppendYaml(yaml, indent + 2, "range: 1");
        AppendYaml(yaml, indent + 2, "includeDiagonals: 0");
        AppendYaml(
            yaml,
            indent + 2,
            $"worldRadius: {YamlFloat(target.WorldRadius)}");
        AppendYaml(
            yaml,
            indent + 2,
            $"includeSource: {YamlBool(target.IncludeSource)}");
        AppendYaml(
            yaml,
            indent + 2,
            $"layerScope: {EnumNumber<EnemyWorldLayerScope>(target.LayerScope)}");
        AppendYamlAreaDefinition(yaml, indent + 2);
    }

    private static void AppendYamlAreaDefinition(
        StringBuilder yaml,
        int indent)
    {
        AppendYaml(yaml, indent, "areaDefinition:");
        AppendYaml(yaml, indent + 2, "shapeType: 0");
        AppendYaml(yaml, indent + 2, "originMode: 1");
        AppendYaml(yaml, indent + 2, "radius: 1.5");
        AppendYaml(yaml, indent + 2, "angle: 360");
        AppendYaml(yaml, indent + 2, "maxCastDistance: 4.25");
    }

    private static void AppendYamlOperations(
        StringBuilder yaml,
        IReadOnlyList<OperationSpec> operations)
    {
        if (operations.Count == 0)
        {
            AppendYaml(yaml, 4, "operations: []");
            return;
        }
        AppendYaml(yaml, 4, "operations:");
        foreach (OperationSpec operation in operations)
        {
            AppendYaml(
                yaml,
                4,
                "- type: " +
                EnumNumber<EnemyAbilityOperationType>(operation.Type));
            AppendYamlEffects(yaml, operation.Effects);
            AppendYaml(
                yaml,
                6,
                $"multiplier: {YamlFloat(operation.Multiplier)}");
            AppendYaml(yaml, 6, $"amount: {operation.Amount}");
            AppendYaml(yaml, 6, $"count: {operation.Count}");
            AppendYaml(yaml, 6, "range: 1");
            AppendYaml(yaml, 6, "includeDiagonals: 0");
            AppendYaml(yaml, 6, "enabled: 1");
            AppendYaml(
                yaml,
                6,
                "targetPriorityMode: " +
                EnumNumber<EnemyTargetPriorityMode>(
                    operation.TargetPriorityMode));
            AppendYaml(yaml, 6, "targetPriorityAdjustment: 0");
            AppendYaml(yaml, 6, $"sourceId: {YamlString(operation.SourceId)}");
            AppendYaml(yaml, 6, $"duration: {YamlFloat(operation.Duration)}");
            AppendYaml(yaml, 6, $"interval: {YamlFloat(operation.Interval)}");
            AppendYaml(
                yaml,
                6,
                $"worldRadius: {YamlFloat(operation.WorldRadius)}");
            AppendYaml(
                yaml,
                6,
                $"percentage: {YamlFloat(operation.Percentage)}");
            AppendYaml(yaml, 6, $"maximumStacks: {operation.MaximumStacks}");
            AppendYaml(
                yaml,
                6,
                "referencedAbilityId: " +
                YamlString(operation.ReferencedAbilityId));
            AppendYaml(yaml, 6, "reference:");
            AppendYaml(yaml, 8, "enemy: {fileID: 0}");
            AppendYaml(
                yaml,
                8,
                $"enemyId: {YamlString(operation.ReferenceEnemyId)}");
            AppendYamlSummon(yaml, operation.Summon);
        }
    }

    private static void AppendYamlEffects(
        StringBuilder yaml,
        IReadOnlyList<EffectSpec> effects)
    {
        if (effects.Count == 0)
        {
            AppendYaml(yaml, 6, "effects: []");
            return;
        }
        AppendYaml(yaml, 6, "effects:");
        foreach (EffectSpec effect in effects)
        {
            AppendYaml(yaml, 6, $"- effectId: {YamlString(effect.Id)}");
            AppendYaml(
                yaml,
                8,
                $"type: {EnumNumber<CharacterEffectType>(effect.Type)}");
            AppendYaml(yaml, 8, "targetMode: 0");
            AppendYaml(yaml, 8, "preconditionFailurePolicy: 0");
            AppendYaml(yaml, 8, "failurePolicy: 0");
            AppendYaml(yaml, 8, "targetSelector:");
            AppendYaml(yaml, 10, "targetFaction: 0");
            AppendYaml(yaml, 10, "subject: 0");
            AppendYaml(yaml, 10, "subjectMetric: 0");
            AppendYaml(yaml, 10, "subjectCount: 1");
            AppendYaml(yaml, 10, "conditionMatchMode: 0");
            AppendYaml(yaml, 10, "numericConditions: []");
            AppendYaml(yaml, 10, "areaOffsets: []");
            AppendYamlAreaDefinition(yaml, 10);
            AppendYaml(yaml, 8, "damageType: 0");
            AppendYaml(yaml, 8, "damageAmountMode: 1");
            AppendYaml(
                yaml,
                8,
                $"damageAmount: {YamlFloat(effect.FixedAmount)}");
            AppendYaml(yaml, 8, "sourceResourceScale: 0");
            AppendYaml(yaml, 8, "sourceCurrentHealthScale: 0");
            AppendYaml(yaml, 8, "sourceMaxHealthScale: 0");
            AppendYaml(yaml, 8, "targetCurrentHealthScale: 0");
            AppendYaml(
                yaml,
                8,
                "targetMaxHealthScale: " +
                YamlFloat(effect.TargetMaximumHealthScale));
            AppendYaml(yaml, 8, "sourceStatusScalingEffect: {fileID: 0}");
            AppendYaml(yaml, 8, "sourceStatusStacksScale: 0");
            AppendYaml(yaml, 8, "targetStatusScalingEffect: {fileID: 0}");
            AppendYaml(yaml, 8, "targetStatusStacksScale: 0");
            AppendYaml(yaml, 8, "statusContributionMultipliers: []");
            AppendYaml(
                yaml,
                8,
                $"statusDuration: {YamlFloat(effect.StatusDuration)}");
            AppendYaml(
                yaml,
                8,
                $"statusStacks: {YamlFloat(effect.StatusStacks)}");
            string statusReference =
                string.IsNullOrEmpty(effect.StatusAssetName)
                    ? "{fileID: 0}"
                    : $"{{fileID: 11400000, guid: {StunStatusGuid}, type: 2}}";
            AppendYaml(yaml, 8, $"statusEffect: {statusReference}");
            AppendYaml(yaml, 8, "statusRemovalEffects: []");
            AppendYaml(
                yaml,
                8,
                $"statusRemovalTarget: {(effect.RemovesDebuffs ? 4 : 0)}");
            AppendYaml(yaml, 8, "statusRemovalPickMode: 0");
            AppendYaml(yaml, 8, "statusRemovalPickCount: 99");
            AppendYaml(yaml, 8, "statusRemovalAmountMode: 0");
            AppendYaml(yaml, 8, "statusRemovalCount: 999");
            AppendYaml(yaml, 8, "statusRemovalRatio: 1");
            AppendYaml(yaml, 8, "castVfxCue: {fileID: 0}");
            AppendYaml(yaml, 8, "projectileVfxCue: {fileID: 0}");
            AppendYaml(yaml, 8, "impactVfxCue: {fileID: 0}");
        }
    }

    private static void AppendYamlSummon(
        StringBuilder yaml,
        EnemySummonSpec summon)
    {
        AppendYaml(yaml, 6, "summon:");
        if (summon == null || summon.CandidateIds.Count == 0)
        {
            AppendYaml(yaml, 8, "candidates: []");
            AppendYaml(yaml, 8, "minimumCount: 1");
            AppendYaml(yaml, 8, "maximumCount: 1");
            AppendYaml(yaml, 8, "maximumActive: 0");
            AppendYaml(yaml, 8, "allowRecursiveSummon: 0");
            AppendYaml(yaml, 8, "inheritFormationLayer: 1");
            AppendYaml(yaml, 8, "childHealthMultiplier: 1");
            AppendYaml(yaml, 8, "childCoreAttackMultiplier: 1");
            return;
        }

        AppendYaml(yaml, 8, "candidates:");
        foreach (string candidate in summon.CandidateIds)
        {
            AppendYaml(yaml, 8, "- enemy: {fileID: 0}");
            AppendYaml(yaml, 10, $"enemyId: {YamlString(candidate)}");
        }
        AppendYaml(yaml, 8, $"minimumCount: {summon.MinimumCount}");
        AppendYaml(yaml, 8, $"maximumCount: {summon.MaximumCount}");
        AppendYaml(yaml, 8, $"maximumActive: {summon.MaximumActive}");
        AppendYaml(
            yaml,
            8,
            $"allowRecursiveSummon: {YamlBool(summon.AllowRecursiveSummon)}");
        AppendYaml(
            yaml,
            8,
            $"inheritFormationLayer: {YamlBool(summon.InheritFormationLayer)}");
        AppendYaml(
            yaml,
            8,
            "childHealthMultiplier: " +
            YamlFloat(summon.ChildHealthMultiplier));
        AppendYaml(
            yaml,
            8,
            "childCoreAttackMultiplier: " +
            YamlFloat(summon.ChildCoreAttackMultiplier));
    }

    private static void AppendYamlCharge(
        StringBuilder yaml,
        ChargeSpec charge)
    {
        charge ??= new ChargeSpec();
        AppendYaml(yaml, 4, "charge:");
        AppendYaml(yaml, 6, $"enabled: {YamlBool(charge.Enabled)}");
        AppendYaml(yaml, 6, $"duration: {YamlFloat(charge.Duration)}");
        AppendYaml(
            yaml,
            6,
            $"interruptible: {YamlBool(charge.Interruptible)}");
        AppendYaml(yaml, 6, $"interrupts: {charge.InterruptFlags}");
    }

    private static void AppendYamlTelegraph(
        StringBuilder yaml,
        TelegraphSpec telegraph)
    {
        telegraph ??= new TelegraphSpec();
        AppendYaml(yaml, 4, "telegraph:");
        AppendYaml(yaml, 6, $"enabled: {YamlBool(telegraph.Enabled)}");
        AppendYaml(yaml, 6, $"leadTime: {YamlFloat(telegraph.LeadTime)}");
        AppendYaml(yaml, 6, $"cueId: {YamlString(telegraph.CueId)}");
        AppendYaml(
            yaml,
            6,
            $"worldRadius: {YamlFloat(telegraph.WorldRadius)}");
    }

    private static void AppendYamlBossPhases(
        StringBuilder yaml,
        EnemySpec spec)
    {
        if (!spec.IsBoss)
        {
            AppendYaml(yaml, 2, "phaseDefinitions: []");
            return;
        }
        AppendYaml(yaml, 2, "phaseDefinitions:");
        AppendYamlBossPhase(
            yaml,
            "P1",
            "enemy.catalog.b001.phase.p1.name",
            "March Before the Gate",
            66,
            100,
            true,
            new[] { "b001.p1.summon", "b001.p1.control_aura" });
        AppendYamlBossPhase(
            yaml,
            "P2",
            "enemy.catalog.b001.phase.p2.name",
            "Memory Erosion",
            31,
            65,
            false,
            new[]
            {
                "b001.p2.erosion",
                "b001.p2.death_heal",
                "b001.p2.maximum_health",
            });
        AppendYamlBossPhase(
            yaml,
            "P3",
            "enemy.catalog.b001.phase.p3.name",
            "Gate Opening",
            0,
            30,
            false,
            new[] { "b001.p3.charge", "b001.p3.aura" });
    }

    private static void AppendYamlBossPhase(
        StringBuilder yaml,
        string phaseId,
        string nameKey,
        string fallbackName,
        int minimumHealth,
        int maximumHealth,
        bool advanceOnCoreContact,
        IReadOnlyList<string> abilityIds)
    {
        AppendYaml(yaml, 2, $"- phaseId: {YamlString(phaseId)}");
        AppendYaml(
            yaml,
            4,
            $"nameLocalizationKey: {YamlString(nameKey)}");
        AppendYaml(
            yaml,
            4,
            $"fallbackName: {YamlString(fallbackName)}");
        AppendYaml(yaml, 4, $"minimumHealthPercent: {minimumHealth}");
        AppendYaml(yaml, 4, $"maximumHealthPercent: {maximumHealth}");
        AppendYaml(
            yaml,
            4,
            $"advanceOnCoreContact: {YamlBool(advanceOnCoreContact)}");
        AppendYamlStringList(yaml, 4, "abilityIds", abilityIds);
    }

    private static void AppendYamlStringList(
        StringBuilder yaml,
        int indent,
        string name,
        IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            AppendYaml(yaml, indent, name + ": []");
            return;
        }
        AppendYaml(yaml, indent, name + ":");
        foreach (string value in values)
            AppendYaml(yaml, indent, $"- {YamlString(value)}");
    }

    private static void AppendYamlEnumList<TEnum>(
        StringBuilder yaml,
        int indent,
        string name,
        IReadOnlyList<string> values)
        where TEnum : struct, Enum
    {
        if (values == null || values.Count == 0)
        {
            AppendYaml(yaml, indent, name + ": []");
            return;
        }
        AppendYaml(yaml, indent, name + ":");
        foreach (string value in values)
            AppendYaml(yaml, indent, $"- {EnumNumber<TEnum>(value)}");
    }

    private static int EnumNumber<TEnum>(string value)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse(value, out TEnum parsed) ||
            !Enum.IsDefined(typeof(TEnum), parsed))
        {
            throw new InvalidOperationException(
                $"Unknown {typeof(TEnum).Name} value '{value}'.");
        }
        return Convert.ToInt32(parsed, CultureInfo.InvariantCulture);
    }

    private static string YamlFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidOperationException("YAML cannot contain NaN/Infinity.");
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static int YamlBool(bool value)
    {
        return value ? 1 : 0;
    }

    private static string YamlString(string value)
    {
        value ??= string.Empty;
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
    }

    private static void AppendYaml(
        StringBuilder yaml,
        int indent,
        string value)
    {
        yaml.Append(' ', indent);
        yaml.AppendLine(value);
    }

    private static string ResolveWorkspacePath(
        string workspace,
        string relativePath)
    {
        string root = Path.GetFullPath(workspace)
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(
            workspace,
            NormalizePath(relativePath).Replace(
                '/',
                Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path '{resolved}' leaves workspace '{root}'.");
        }
        return resolved;
    }

    private static void DeleteVerifiedTemporaryDirectory(
        string workspace,
        string directory)
    {
        if (!Directory.Exists(directory))
            return;
        string expected = ResolveWorkspacePath(
            workspace,
            YamlStagingFolder);
        if (!string.Equals(
                Path.GetFullPath(directory),
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to delete unexpected directory '{directory}'.");
        }
        Directory.Delete(directory, recursive: true);
    }

    private static Dictionary<EEnemyType, YamlPresentationTemplate>
        ReadYamlPresentationTemplates(string enemyFolder)
    {
        Dictionary<EEnemyType, YamlPresentationTemplate> result = new();
        foreach (EEnemyType type in Enum.GetValues(typeof(EEnemyType)))
        {
            string path = Path.Combine(
                enemyFolder,
                ResolvePresentationTemplateFileName(type));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Preserved presentation template '{path}' is missing.",
                    path);
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            result.Add(type, new YamlPresentationTemplate(
                ReadYamlField(lines, "iconSprite"),
                ReadYamlField(lines, "boardSprite"),
                ReadOptionalYamlField(
                    lines,
                    "boardSpriteFacesRight",
                    "1"),
                ReadYamlField(lines, "spawnVfxCue"),
                ReadYamlField(lines, "deathVfxCue")));
        }
        return result;
    }

    private static string ReadYamlField(
        IEnumerable<string> lines,
        string fieldName)
    {
        string prefix = "  " + fieldName + ":";
        string line = lines.FirstOrDefault(candidate =>
            candidate.StartsWith(prefix, StringComparison.Ordinal));
        if (line == null)
        {
            throw new InvalidOperationException(
                $"YAML field '{fieldName}' is missing.");
        }
        string value = line.Substring(prefix.Length).Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"YAML field '{fieldName}' is empty.");
        }
        return value;
    }

    private static string ReadOptionalYamlField(
        IEnumerable<string> lines,
        string fieldName,
        string defaultValue)
    {
        string prefix = "  " + fieldName + ":";
        string line = lines.FirstOrDefault(candidate =>
            candidate.StartsWith(prefix, StringComparison.Ordinal));
        if (line == null)
            return defaultValue;

        string value = line.Substring(prefix.Length).Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"YAML field '{fieldName}' is empty.");
        }
        return value;
    }

    private static string CreateDeterministicAssetGuid(string enemyId)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
            "PS260714.EnemyRoster.v2:" + enemyId));
        StringBuilder result = new(32);
        for (int index = 0; index < 16; index++)
            result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static string ReadMetaGuid(string metaPath)
    {
        foreach (string line in File.ReadLines(metaPath, Encoding.UTF8))
        {
            const string prefix = "guid:";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            string value = line.Substring(prefix.Length).Trim();
            if (IsUnityGuid(value))
                return value;
            break;
        }
        throw new InvalidOperationException(
            $"Meta file '{metaPath}' has no valid GUID.");
    }

    private static bool IsUnityGuid(string value)
    {
        return value != null &&
               value.Length == 32 &&
               value.All(character =>
                   character is >= '0' and <= '9' or
                   >= 'a' and <= 'f');
    }

    private static string RenderNativeAssetMeta(string guid)
    {
        return "fileFormatVersion: 2\n" +
               $"guid: {guid}\n" +
               "NativeFormatImporter:\n" +
               "  externalObjects: {}\n" +
               "  mainObjectFileID: 11400000\n" +
               "  userData: \n" +
               "  assetBundleName: \n" +
               "  assetBundleVariant: \n";
    }

    private static void ValidateLocalizationKeys(
        IReadOnlyList<EnemySpec> specs,
        string workspace)
    {
        string csvPath = ResolveWorkspacePath(
            workspace,
            "Assets/11_LocalizationSource/strings.csv");
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(csvPath, Encoding.UTF8).Skip(1))
        {
            int comma = line.IndexOf(',');
            if (comma > 0)
                keys.Add(line.Substring(0, comma));
        }
        foreach (EnemySpec spec in specs)
        {
            if (!keys.Contains(spec.NameKey) ||
                !keys.Contains(spec.DescriptionKey))
            {
                throw new InvalidOperationException(
                    $"Localization CSV is missing keys for {spec.Id}.");
            }
        }
        foreach (string phaseKey in new[]
                 {
                     "enemy.catalog.b001.phase.p1.name",
                     "enemy.catalog.b001.phase.p2.name",
                     "enemy.catalog.b001.phase.p3.name",
                     "ui.dungeon.card.locked",
                 })
        {
            if (!keys.Contains(phaseKey))
            {
                throw new InvalidOperationException(
                    $"Localization CSV is missing '{phaseKey}'.");
            }
        }
    }

    private static void ValidateLiveAssetNamesBeforeYamlCommit(
        IReadOnlyList<EnemySpec> specs,
        string enemyFolder)
    {
        HashSet<string> expected = specs
            .Select(spec => spec.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(
                     enemyFolder,
                     "*.asset",
                     SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(path);
            if (!expected.Contains(fileName))
            {
                throw new InvalidOperationException(
                    $"Unexpected EnemySO asset '{fileName}' must be " +
                    "reviewed before deterministic YAML replacement.");
            }
        }
    }

    private static void ValidateYamlRoster(
        IReadOnlyList<EnemySpec> specs,
        string folder,
        IReadOnlyDictionary<string, string> expectedGuids,
        bool requireExactDirectoryContents)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> guids = new(StringComparer.Ordinal);
        HashSet<string> expectedFiles = specs
            .Select(spec => spec.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (EnemySpec spec in specs)
        {
            string assetPath = Path.Combine(folder, spec.FileName);
            string metaPath = assetPath + ".meta";
            if (!File.Exists(assetPath) || !File.Exists(metaPath))
            {
                throw new InvalidOperationException(
                    $"YAML roster file pair is missing for {spec.Id}.");
            }
            string yaml = File.ReadAllText(assetPath, Encoding.UTF8)
                .Replace("\r\n", "\n");
            if (!yaml.Contains(
                    $"\n  enemyId: {YamlString(spec.Id)}\n",
                    StringComparison.Ordinal) ||
                !yaml.Contains(
                    "\n  rosterSchemaVersion: 1\n",
                    StringComparison.Ordinal) ||
                !yaml.Contains(
                    $"\n  nameLocalizationKey: " +
                    $"{YamlString(spec.NameKey)}\n",
                    StringComparison.Ordinal) ||
                !ids.Add(spec.Id))
            {
                throw new InvalidOperationException(
                    $"Rendered YAML identity validation failed for " +
                    $"{spec.Id}.");
            }
            foreach (AbilitySpec ability in spec.Abilities)
            {
                if (!yaml.Contains(
                        $"abilityId: {YamlString(ability.Id)}",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Rendered YAML for {spec.Id} is missing ability " +
                        $"'{ability.Id}'.");
                }
            }

            string guid = ReadMetaGuid(metaPath);
            if (!string.Equals(
                    guid,
                    expectedGuids[spec.Id],
                    StringComparison.Ordinal) ||
                !guids.Add(guid))
            {
                throw new InvalidOperationException(
                    $"Rendered YAML GUID validation failed for {spec.Id}.");
            }
        }

        if (specs.Count != ExpectedEnemyCount ||
            ids.Count != ExpectedEnemyCount ||
            guids.Count != ExpectedEnemyCount)
        {
            throw new InvalidOperationException(
                "Rendered YAML roster count validation failed.");
        }
        if (!requireExactDirectoryContents)
            return;

        string[] actualFiles = Directory.GetFiles(
            folder,
            "*.asset",
            SearchOption.TopDirectoryOnly);
        if (actualFiles.Length != expectedFiles.Count ||
            actualFiles.Any(path =>
                !expectedFiles.Contains(Path.GetFileName(path))))
        {
            throw new InvalidOperationException(
                $"YAML roster directory '{folder}' does not contain the " +
                "exact 46-asset catalog.");
        }
    }

    private static void RollbackYamlCommit(
        IReadOnlyList<YamlCommitRecord> records,
        string backupFolder)
    {
        for (int index = records.Count - 1; index >= 0; index--)
        {
            YamlCommitRecord record = records[index];
            string assetBackup = Path.Combine(
                backupFolder,
                Path.GetFileName(record.AssetPath));
            string metaBackup = Path.Combine(
                backupFolder,
                Path.GetFileName(record.MetaPath));
            if (record.AssetExisted)
                File.Copy(assetBackup, record.AssetPath, overwrite: true);
            else if (File.Exists(record.AssetPath))
                File.Delete(record.AssetPath);
            if (record.MetaExisted)
                File.Copy(metaBackup, record.MetaPath, overwrite: true);
            else if (File.Exists(record.MetaPath))
                File.Delete(record.MetaPath);
        }
    }

    private static void EnsureFolder(string folder)
    {
        folder = NormalizePath(folder);
        if (AssetDatabase.IsValidFolder(folder))
            return;

        int slash = folder.LastIndexOf('/');
        if (slash <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot create invalid asset folder '{folder}'.");
        }
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
        {
            throw new InvalidOperationException(
                $"Could not clear scratch folder '{folder}'.");
        }
    }

    private static void MoveAssetChecked(string from, string to)
    {
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(
                $"Could not move '{from}' to '{to}': {error}");
        }
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
            throw new MissingFieldException(serialized.targetObject.name, name);
        return property;
    }

    private static SerializedProperty RequireRelative(
        SerializedProperty parent,
        string name)
    {
        SerializedProperty property = parent?.FindPropertyRelative(name);
        if (property == null)
        {
            throw new MissingFieldException(
                parent?.propertyPath ?? "<null>",
                name);
        }
        return property;
    }

    private static void PopulateStringList(
        SerializedProperty property,
        IReadOnlyList<string> values)
    {
        property.arraySize = values.Count;
        for (int index = 0; index < values.Count; index++)
            property.GetArrayElementAtIndex(index).stringValue = values[index];
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
        SerializedObject serialized,
        string name,
        float value)
    {
        RequireProperty(serialized, name).floatValue = value;
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

    private static void SetEnum(
        SerializedObject serialized,
        string name,
        string value)
    {
        SetEnumValue(RequireProperty(serialized, name), value);
    }

    private static void SetEnum(
        SerializedProperty parent,
        string name,
        string value)
    {
        SetEnumValue(RequireRelative(parent, name), value);
    }

    private static void SetEnumValue(
        SerializedProperty property,
        string value)
    {
        int index = Array.IndexOf(property.enumNames, value);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Enum property '{property.propertyPath}' has no value " +
                $"named '{value}'.");
        }
        property.enumValueIndex = index;
    }

    private static void SetEnumFlags(
        SerializedProperty parent,
        string name,
        int value)
    {
        RequireRelative(parent, name).intValue = value;
    }

    private sealed class EnemySpec
    {
        public string Id;
        public string KoreanName;
        public string EnglishName;
        public EnemyRosterTier Tier;
        public EEnemyGrade Grade;
        public EEnemyType LegacyType;
        public string Role;
        public string AbilityType;
        public float HealthMultiplier;
        public float CoreAttackMultiplier;
        public float AttackPeriodMultiplier;
        public float MoveSpeedMultiplier;
        public int UnlockDifficulty;
        public int AuthoredRecommendedMaxPerWave;
        public string Priority;
        public string KoreanAbilityDescription;
        public string EnglishAbilityDescription;
        public List<ParameterSpec> Parameters = new();
        public List<string> CounterTags = new();
        public List<string> AssumptionIds = new();
        public List<string> RoleTags = new();
        public List<AbilitySpec> Abilities = new();
        public int BaseHealth;
        public float PreciseCoreAttackDamage;
        public int LegacyCoreAttackDamage;
        public float CoreAttackInterval;
        public float ApproachSpeed;
        public float FormationRadius;
        public float ForwardSearchAngle =
            EnemySO.DefaultForwardSearchAngle;
        public float CoreAttackRange;
        public bool UsesFractionalCoreDamage;
        public int RecommendedMaxPerWave;
        public float SpawnBudget;
        public bool EncounterOnly;
        public string FileName;

        public bool IsBoss => Tier == EnemyRosterTier.Boss;
        public AbilitySpec Ability => Abilities.FirstOrDefault();
        public string NameKey =>
            $"enemy.catalog.{Id.ToLowerInvariant()}.name";
        public string DescriptionKey =>
            $"enemy.catalog.{Id.ToLowerInvariant()}.description";
        public string AbilityNameKey =>
            "codex.enemy.ability";
        public string AbilityDescriptionKey =>
            DescriptionKey;
        public int SortOrder
        {
            get
            {
                int number = int.Parse(
                    Id.Substring(1),
                    CultureInfo.InvariantCulture);
                return Tier switch
                {
                    EnemyRosterTier.Special => 100 + number,
                    EnemyRosterTier.Elite => 200 + number,
                    EnemyRosterTier.Boss => 300 + number,
                    _ => number,
                };
            }
        }
    }

    private sealed class AbilitySpec
    {
        public string Id;
        public string TypeId;
        public string FallbackName;
        public string FallbackDescription;
        public string Trigger;
        public readonly List<string> TriggerEvents = new();
        public float Cooldown;
        public readonly List<CooldownOverrideSpec> CooldownOverrides = new();
        public int InitialCharges;
        public float HealthThresholdPercent;
        public float NoDamageDuration;
        public TargetSpec Target;
        public ChargeSpec Charge = new();
        public TelegraphSpec Telegraph = new();
        public readonly List<ParameterSpec> Parameters = new();
        public readonly List<ConditionSpec> Conditions = new();
        public readonly List<OperationSpec> Operations = new();

        public OperationSpec AddOperation(string type)
        {
            OperationSpec operation = new() { Type = type };
            Operations.Add(operation);
            return operation;
        }

        public void AddEffect(EffectSpec effect)
        {
            OperationSpec operation = Operations.FirstOrDefault(
                item => item.Type == "ExecuteEffects");
            if (operation == null)
                operation = AddOperation("ExecuteEffects");
            operation.Effects.Add(effect);
        }

        public OperationSpec AddSummon(
            IReadOnlyList<string> candidateIds,
            int minimumCount,
            int maximumCount,
            float childHealthMultiplier,
            float childCoreAttackMultiplier,
            int maximumActive = SimultaneousSummonCap)
        {
            OperationSpec operation = AddOperation("SummonEnemy");
            operation.Summon = new EnemySummonSpec
            {
                CandidateIds = candidateIds.ToList(),
                MinimumCount = minimumCount,
                MaximumCount = maximumCount,
                MaximumActive = maximumActive,
                AllowRecursiveSummon = false,
                InheritFormationLayer = true,
                ChildHealthMultiplier = childHealthMultiplier,
                ChildCoreAttackMultiplier = childCoreAttackMultiplier,
            };
            return operation;
        }
    }

    private sealed class CooldownOverrideSpec
    {
        public float HealthAtOrBelowPercent { get; }
        public float Cooldown { get; }

        public CooldownOverrideSpec(
            float healthAtOrBelowPercent,
            float cooldown)
        {
            HealthAtOrBelowPercent = healthAtOrBelowPercent;
            Cooldown = cooldown;
        }
    }

    private sealed class ConditionSpec
    {
        public string Type;
        public string StatusAssetName = string.Empty;
        public float WindowDuration;
        public bool Expected = true;
    }

    private sealed class OperationSpec
    {
        public string Type;
        public float Multiplier = 1f;
        public int Amount;
        public int Count = 1;
        public float Duration;
        public float Interval;
        public float WorldRadius;
        public float Percentage;
        public int MaximumStacks;
        public string ReferencedAbilityId = string.Empty;
        public string ReferenceEnemyId = string.Empty;
        public string SourceId = string.Empty;
        public string TargetPriorityMode = "Exclude";
        public EnemySummonSpec Summon;
        public readonly List<EffectSpec> Effects = new();
    }

    private sealed class EnemySummonSpec
    {
        public List<string> CandidateIds = new();
        public int MinimumCount = 1;
        public int MaximumCount = 1;
        public int MaximumActive = SimultaneousSummonCap;
        public bool AllowRecursiveSummon;
        public bool InheritFormationLayer = true;
        public float ChildHealthMultiplier = 1f;
        public float ChildCoreAttackMultiplier = 1f;
    }

    private sealed class TargetSpec
    {
        public string Faction = "None";
        public string Subject = "None";
        public string Metric = "None";
        public int Count = 1;
        public float WorldRadius;
        public bool IncludeSource;
        public string LayerScope = "All";

        public static TargetSpec None()
        {
            return new TargetSpec();
        }

        public static TargetSpec Self()
        {
            return new TargetSpec
            {
                Faction = "Self",
                Subject = "Self",
                Metric = "Health",
                IncludeSource = true,
            };
        }

        public static TargetSpec Allies(
            float radius,
            int count,
            bool includeSource)
        {
            return new TargetSpec
            {
                Faction = "EnemyAllies",
                Subject = "WorldRadius",
                Metric = "None",
                Count = Mathf.Clamp(count, 1, 99),
                WorldRadius = radius,
                IncludeSource = includeSource,
                LayerScope = "All",
            };
        }

        public static TargetSpec AlliesLowest(float radius, int count)
        {
            TargetSpec target = Allies(radius, count, false);
            target.Metric = "HealthPercentage";
            return target;
        }

        public static TargetSpec AlliesAll(bool includeSource)
        {
            return new TargetSpec
            {
                Faction = "EnemyAllies",
                Subject = "All",
                Metric = "Health",
                Count = 99,
                IncludeSource = includeSource,
            };
        }

        public static TargetSpec PlayersAll()
        {
            return new TargetSpec
            {
                Faction = "PlayerCharacters",
                Subject = "All",
                Metric = "Health",
                Count = 99,
            };
        }

        public static TargetSpec PlayersInRadius(float radius)
        {
            return new TargetSpec
            {
                Faction = "PlayerCharacters",
                Subject = "WorldRadius",
                Metric = "None",
                Count = 99,
                WorldRadius = radius,
                LayerScope = "All",
            };
        }

        public static TargetSpec PlayersHighestDamage()
        {
            return new TargetSpec
            {
                Faction = "PlayerCharacters",
                Subject = "HighestValue",
                Metric = "TotalDamageDealt",
                Count = 1,
            };
        }
    }

    private sealed class ChargeSpec
    {
        public bool Enabled;
        public float Duration;
        public bool Interruptible = true;
        public int InterruptFlags = 11;

        public static ChargeSpec Create(float duration)
        {
            return new ChargeSpec
            {
                Enabled = true,
                Duration = duration,
                Interruptible = true,
                InterruptFlags = 11,
            };
        }
    }

    private sealed class TelegraphSpec
    {
        public bool Enabled;
        public float LeadTime;
        public string CueId = string.Empty;
        public float WorldRadius;

        public static TelegraphSpec Create(
            float leadTime,
            string cueId,
            float worldRadius)
        {
            return new TelegraphSpec
            {
                Enabled = true,
                LeadTime = leadTime,
                CueId = cueId,
                WorldRadius = worldRadius,
            };
        }
    }

    private sealed class EffectSpec
    {
        public string Id;
        public string Type;
        public float FixedAmount = 0f;
        public float TargetMaximumHealthScale;
        public float StatusDuration = 1f;
        public float StatusStacks = 1f;
        public string StatusAssetName = string.Empty;
        public bool RemovesDebuffs;

        public static EffectSpec PercentMaxHealth(
            string id,
            string type,
            float scale)
        {
            return new EffectSpec
            {
                Id = id,
                Type = type,
                TargetMaximumHealthScale = scale,
            };
        }

        public static EffectSpec Status(
            string id,
            string type,
            float duration,
            string statusAssetName)
        {
            return new EffectSpec
            {
                Id = id,
                Type = type,
                StatusDuration = duration,
                StatusStacks = 1f,
                StatusAssetName = statusAssetName,
            };
        }

        public static EffectSpec RemoveDebuffs(string id)
        {
            return new EffectSpec
            {
                Id = id,
                Type = "RemoveStatus",
                RemovesDebuffs = true,
            };
        }
    }

    private sealed class ParameterSpec
    {
        public string Key;
        public string ValueType;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public string TextValue = string.Empty;

        public static ParameterSpec Float(string key, float value)
        {
            return new ParameterSpec
            {
                Key = key,
                ValueType = "Float",
                FloatValue = value,
            };
        }

        public static ParameterSpec Integer(string key, int value)
        {
            return new ParameterSpec
            {
                Key = key,
                ValueType = "Integer",
                IntValue = value,
            };
        }

        public static ParameterSpec Boolean(string key, bool value)
        {
            return new ParameterSpec
            {
                Key = key,
                ValueType = "Boolean",
                BoolValue = value,
            };
        }

        public static ParameterSpec Text(string key, string value)
        {
            return new ParameterSpec
            {
                Key = key,
                ValueType = "Text",
                TextValue = value ?? string.Empty,
            };
        }

        public static ParameterSpec EnemyReference(
            string key,
            string enemyId)
        {
            return new ParameterSpec
            {
                Key = key,
                ValueType = "EnemyReference",
                TextValue = enemyId ?? string.Empty,
            };
        }
    }

    private sealed class AssumptionDefinition
    {
        public string Id { get; }
        public string Description { get; }

        public AssumptionDefinition(string id, string description)
        {
            Id = id;
            Description = description;
        }
    }

    private sealed class YamlPresentationTemplate
    {
        public string IconSprite { get; }
        public string BoardSprite { get; }
        public string BoardSpriteFacesRight { get; }
        public string SpawnVfxCue { get; }
        public string DeathVfxCue { get; }

        public YamlPresentationTemplate(
            string iconSprite,
            string boardSprite,
            string boardSpriteFacesRight,
            string spawnVfxCue,
            string deathVfxCue)
        {
            IconSprite = iconSprite;
            BoardSprite = boardSprite;
            BoardSpriteFacesRight = boardSpriteFacesRight;
            SpawnVfxCue = spawnVfxCue;
            DeathVfxCue = deathVfxCue;
        }
    }

    private readonly struct YamlCommitRecord
    {
        public string AssetPath { get; }
        public string MetaPath { get; }
        public bool AssetExisted { get; }
        public bool MetaExisted { get; }

        public YamlCommitRecord(
            string assetPath,
            string metaPath,
            bool assetExisted,
            bool metaExisted)
        {
            AssetPath = assetPath;
            MetaPath = metaPath;
            AssetExisted = assetExisted;
            MetaExisted = metaExisted;
        }
    }

    private sealed class OriginalAssetState
    {
        public EnemySO Asset { get; }
        public EnemySO Backup { get; }
        public string Name { get; }

        public OriginalAssetState(
            EnemySO asset,
            EnemySO backup,
            string name)
        {
            Asset = asset;
            Backup = backup;
            Name = name;
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

public readonly struct EnemyRosterCatalogAudit
{
    public int TotalCount { get; }
    public int GeneralCount { get; }
    public int SpecialCount { get; }
    public int EliteCount { get; }
    public int BossCount { get; }
    public int FractionalCoreDamageCount { get; }
    public int AssumptionCount { get; }
    public int EncounterOnlyCount { get; }

    public EnemyRosterCatalogAudit(
        int totalCount,
        int generalCount,
        int specialCount,
        int eliteCount,
        int bossCount,
        int fractionalCoreDamageCount,
        int assumptionCount,
        int encounterOnlyCount)
    {
        TotalCount = totalCount;
        GeneralCount = generalCount;
        SpecialCount = specialCount;
        EliteCount = eliteCount;
        BossCount = bossCount;
        FractionalCoreDamageCount = fractionalCoreDamageCount;
        AssumptionCount = assumptionCount;
        EncounterOnlyCount = encounterOnlyCount;
    }
}

public readonly struct EnemyRosterSpecSummary
{
    public string EnemyId { get; }
    public EnemyRosterTier Tier { get; }
    public int BaseHealth { get; }
    public float PreciseCoreAttackDamage { get; }
    public int LegacyCoreAttackDamage { get; }
    public float CoreAttackInterval { get; }
    public float ApproachSpeed { get; }
    public float FormationRadius { get; }
    public float ForwardSearchAngle { get; }
    public float CoreAttackRange { get; }
    public int RecommendedMaxPerWave { get; }
    public bool EncounterOnly { get; }
    public string AbilityTypeId { get; }
    public bool AllowsRecursiveSummon { get; }
    public int MaximumActiveSummons { get; }

    public EnemyRosterSpecSummary(
        string enemyId,
        EnemyRosterTier tier,
        int baseHealth,
        float preciseCoreAttackDamage,
        int legacyCoreAttackDamage,
        float coreAttackInterval,
        float approachSpeed,
        float formationRadius,
        float forwardSearchAngle,
        float coreAttackRange,
        int recommendedMaxPerWave,
        bool encounterOnly,
        string abilityTypeId,
        bool allowsRecursiveSummon,
        int maximumActiveSummons)
    {
        EnemyId = enemyId;
        Tier = tier;
        BaseHealth = baseHealth;
        PreciseCoreAttackDamage = preciseCoreAttackDamage;
        LegacyCoreAttackDamage = legacyCoreAttackDamage;
        CoreAttackInterval = coreAttackInterval;
        ApproachSpeed = approachSpeed;
        FormationRadius = formationRadius;
        ForwardSearchAngle = forwardSearchAngle;
        CoreAttackRange = coreAttackRange;
        RecommendedMaxPerWave = recommendedMaxPerWave;
        EncounterOnly = encounterOnly;
        AbilityTypeId = abilityTypeId;
        AllowsRecursiveSummon = allowsRecursiveSummon;
        MaximumActiveSummons = maximumActiveSummons;
    }
}
#endif
