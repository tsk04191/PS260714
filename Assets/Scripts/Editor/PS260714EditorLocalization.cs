using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared localization and tooltip policy for project-owned IMGUI editors.
/// Unity's editor language is Korean when a known built-in label is returned
/// in Hangul; every other editor language uses the English presentation.
/// </summary>
internal static class PS260714EditorText
{
    private static readonly Regex HangulPattern = new("[가-힣]");
    private static readonly Regex RemainingHangulWords = new("[가-힣]+(?:을|를|이|가|은|는|의|에|로|와|과|도|만)?");

    private static readonly IReadOnlyDictionary<string, string> KoreanPhrases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["속성을 찾을 수 없습니다"] = "property could not be found",
            ["목록을 찾을 수 없습니다"] = "list could not be found",
            ["구성을 찾을 수 없습니다"] = "configuration could not be found",
            ["선택하지 않았습니다"] = "is not selected",
            ["지정되지 않았습니다"] = "is not assigned",
            ["설정되지 않았습니다"] = "is not configured",
            ["비어 있습니다"] = "is empty",
            ["지원하지 않습니다"] = "is not supported",
            ["사용할 수 없습니다"] = "is unavailable",
            ["저장하지 못했습니다"] = "could not be saved",
            ["생성하지 못했습니다"] = "could not be created",
            ["삭제하지 못했습니다"] = "could not be deleted",
            ["찾지 못했습니다"] = "could not be found",
            ["선택해 주세요"] = "Please select",
            ["선택하세요"] = "Select",
            ["추가해 주세요"] = "Please add",
            ["입력해 주세요"] = "Please enter",
            ["원본 일러스트 / HUD 노출 영역"] =
                "Original Illustration / HUD Visible Area",
            ["CharacterInfo 마스크 결과"] = "CharacterInfo Mask Result",
            ["Standing Sprite 없음"] = "No Standing Sprite",
            ["Sprite 없음"] = "No Sprite",
            ["공통 등급 스타일"] = "Shared Grade Style",
            ["공통 팔레트"] = "Shared Palette",
            ["아이콘 미지정"] = "Icon Unassigned",
            ["여기에 여러 CharacterSO / ItemDefinitionSO 드래그"] =
                "Drag CharacterSO / ItemDefinitionSO Assets Here",
            ["새로고침"] = "Refresh",
            ["위로 이동"] = "Move up",
            ["아래로 이동"] = "Move down",
            ["상시 능력치 보정"] = "Permanent Stat Modifier",
            ["상태 능력치 기여 배율"] = "Status Stat Contribution Multiplier",
            ["던전 스테이지 진행도 배율"] = "Dungeon Stage Progress Multiplier",
            ["대상 우선순위"] = "Target Priority",
            ["공격 쿨다운"] = "Attack Cooldown",
            ["공격 속도"] = "Attack Speed",
            ["공격 피해량"] = "Attack Damage",
            ["패시브 피해량"] = "Passive Damage",
            ["기술 피해량"] = "Skill Damage",
            ["스킬 피해량"] = "Skill Damage",
            ["기술 비용 감소"] = "Skill Cost Reduction",
            ["스킬 비용 감소"] = "Skill Cost Reduction",
            ["받는 피해"] = "Incoming Damage",
            ["최대 체력"] = "Maximum Health",
            ["현재 체력"] = "Current Health",
            ["체력 효율 상한"] = "Health Performance Cap",
            ["체력 효율"] = "Health Performance",
            ["고정 피해"] = "Fixed Damage",
            ["피해량"] = "Damage Amount",
            ["보호막 회복"] = "Shield Recovery",
            ["보호막"] = "Shield",
            ["카드 드로우"] = "Card Draw",
            ["로컬라이제이션 키"] = "Localization Key",
            ["기본 설명"] = "Fallback Description",
            ["기본 이름"] = "Fallback Name",
            ["세부 직군"] = "Archetype",
            ["직군"] = "Role",
            ["캐릭터"] = "Character",
            ["캐릭터 스탯"] = "Character Stat",
            ["능력치 종류"] = "Stat Type",
            ["보정 종류"] = "Modifier Category",
            ["능력치 보정"] = "Stat Modifier",
            ["능력치 수정자"] = "Stat Modifier",
            ["누적 업그레이드"] = "Cumulative Upgrade",
            ["던전 업그레이드"] = "Dungeon Upgrade",
            ["모집 배너"] = "Recruit Banner",
            ["등급 확률"] = "Grade Probability",
            ["모집권"] = "Recruit Ticket",
            ["강화 재료"] = "Upgrade Material",
            ["이벤트 재화"] = "Event Currency",
            ["소모품"] = "Consumable",
            ["전투 아이템"] = "Battle Item",
            ["전투 카드"] = "Battle Card",
            ["상태 효과"] = "Status Effect",
            ["제어 효과"] = "Control Effect",
            ["트리거 블록"] = "Trigger Block",
            ["효과 블록"] = "Effect Block",
            ["구성 블록"] = "Configuration Block",
            ["대상 진영"] = "Target Faction",
            ["선정 방식"] = "Selection Mode",
            ["비교 수치"] = "Comparison Stat",
            ["대상 수"] = "Target Count",
            ["범위 형태"] = "Area Shape",
            ["범위 중심"] = "Area Origin",
            ["시전자"] = "Caster",
            ["아군"] = "Ally",
            ["적군"] = "Enemy",
            ["무작위"] = "Random",
            ["진행 방향 바라보기"] = "Face Movement Direction",
            ["포물선 높이"] = "Arc Height",
            ["이동 시간"] = "Travel Time",
            ["시작 시간"] = "Start Time",
            ["재생 길이"] = "Playback Duration",
            ["수명 방식"] = "Lifetime Mode",
            ["종료 방식"] = "End Mode",
            ["부착 방식"] = "Attachment Mode",
            ["스케일 방식"] = "Scale Mode",
            ["이동 방식"] = "Movement Mode",
            ["로컬 위치"] = "Local Position",
            ["로컬 회전"] = "Local Rotation",
            ["로컬 배율"] = "Local Scale",
            ["전체 배율"] = "Overall Scale",
            ["축별 배율"] = "Per-axis Scale",
            ["사전 생성 수"] = "Prewarm Count",
            ["동시 재생 제한"] = "Concurrent Playback Limit",
            ["필수 출력"] = "Required Output",
            ["클립 오디오"] = "Clip Audio",
            ["오디오 클립"] = "Audio Clip",
            ["아이콘"] = "Icon",
            ["프리팹"] = "Prefab",
            ["타임라인"] = "Timeline",
            ["확률"] = "Probability",
            ["가중치"] = "Weight",
            ["기본 수치"] = "Base Value",
            ["고정 수치"] = "Fixed Value",
            ["비율 수치"] = "Ratio Value",
            ["고정 가산"] = "Flat Add",
            ["기본값 비율 가산"] = "Add Base Ratio",
            ["곱연산 비율"] = "Multiplicative Ratio",
            ["연산"] = "Operation",
            ["조건"] = "Condition",
            ["대상"] = "Target",
            ["효과"] = "Effect",
            ["능력"] = "Ability",
            ["패시브"] = "Passive",
            ["기술"] = "Skill",
            ["공격력"] = "Attack Power",
            ["공격"] = "Attack",
            ["속도"] = "Speed",
            ["체력"] = "Health",
            ["피해"] = "Damage",
            ["회복"] = "Recovery",
            ["자원"] = "Resource",
            ["상태"] = "Status",
            ["스택"] = "Stacks",
            ["지속시간"] = "Duration",
            ["쿨다운"] = "Cooldown",
            ["비용"] = "Cost",
            ["코스트"] = "Cost",
            ["수량"] = "Amount",
            ["횟수"] = "Count",
            ["제한"] = "Limit",
            ["단계"] = "Stage",
            ["등급"] = "Grade",
            ["재화"] = "Currency",
            ["보상"] = "Reward",
            ["아이템"] = "Item",
            ["던전"] = "Dungeon",
            ["전투"] = "Battle",
            ["이벤트"] = "Event",
            ["휴식"] = "Rest",
            ["상점"] = "Shop",
            ["배너"] = "Banner",
            ["이름"] = "Name",
            ["설명"] = "Description",
            ["종류"] = "Type",
            ["방식"] = "Mode",
            ["범위"] = "Area",
            ["위치"] = "Position",
            ["회전"] = "Rotation",
            ["배율"] = "Multiplier",
            ["시간"] = "Time",
            ["경로"] = "Path",
            ["목록"] = "List",
            ["순서"] = "Order",
            ["정보"] = "Information",
            ["설정"] = "Settings",
            ["사용"] = "Enabled",
            ["공통"] = "Common",
            ["전용"] = "Exclusive",
            ["전체"] = "All",
            ["자신"] = "Self",
            ["기본"] = "Default",
            ["현재"] = "Current",
            ["최대"] = "Maximum",
            ["최소"] = "Minimum",
            ["선택"] = "Select",
            ["추가"] = "Add",
            ["삭제"] = "Delete",
            ["제거"] = "Remove",
            ["복제"] = "Duplicate",
            ["생성"] = "Create",
            ["저장"] = "Save",
            ["취소"] = "Cancel",
            ["확인"] = "Confirm",
            ["검색"] = "Search",
            ["편집"] = "Edit",
            ["적용"] = "Apply",
            ["실행"] = "Execute",
            ["재생"] = "Play",
            ["정지"] = "Stop",
            ["초"] = "sec",
            ["없음"] = "None",
            ["미지정"] = "Unassigned",
            ["사용 안 함"] = "Disabled",
            ["활성화"] = "Enable",
            ["비활성화"] = "Disable",
        };

    private static readonly IReadOnlyDictionary<string, string> EnglishPhrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Permanent Stat Modifier"] = "상시 능력치 보정",
            ["Status Stat Contribution Multiplier"] = "상태 능력치 기여 배율",
            ["Dungeon Stage Progress Multiplier"] = "던전 스테이지 진행도 배율",
            ["Target Priority"] = "대상 우선순위",
            ["Attack Cooldown"] = "공격 쿨다운",
            ["Attack Speed"] = "공격 속도",
            ["Attack Power"] = "공격력",
            ["Attack Damage"] = "공격 피해량",
            ["Passive Damage"] = "패시브 피해량",
            ["Skill Damage"] = "기술 피해량",
            ["Skill Cost Reduction"] = "기술 비용 감소",
            ["Incoming Damage"] = "받는 피해",
            ["Maximum Health"] = "최대 체력",
            ["Current Health"] = "현재 체력",
            ["Shield Recovery"] = "보호막 회복",
            ["Card Reward Pool"] = "카드 보상 풀",
            ["Consumable Reward Pool"] = "소모품 보상 풀",
            ["Override Shield Recovery"] = "보호막 회복 덮어쓰기",
            ["Battle Card Editor"] = "전투 카드 편집기",
            ["Battle Editor"] = "전투 편집기",
            ["Battle VFX"] = "전투 VFX",
            ["Character Editor"] = "캐릭터 편집기",
            ["Dungeon Editor"] = "던전 편집기",
            ["Enemy Editor"] = "적 편집기",
            ["Event Editor"] = "이벤트 편집기",
            ["Item Editor"] = "아이템 편집기",
            ["Recruit Editor"] = "모집 편집기",
            ["Rest Editor"] = "휴식 편집기",
            ["Status Effects"] = "상태 효과",
            ["Original Illustration / HUD Visible Area"] =
                "원본 일러스트 / HUD 노출 영역",
            ["CharacterInfo Mask Result"] = "CharacterInfo 마스크 결과",
            ["No Standing Sprite"] = "Standing Sprite 없음",
            ["No Sprite"] = "Sprite 없음",
            ["Shared Grade Style"] = "공통 등급 스타일",
            ["Shared Palette"] = "공통 팔레트",
            ["Icon Unassigned"] = "아이콘 미지정",
            ["Drag CharacterSO / ItemDefinitionSO Assets Here"] =
                "여기에 여러 CharacterSO / ItemDefinitionSO 드래그",
            ["Select a RestSO to preview."] =
                "미리 볼 RestSO를 선택하세요.",
            ["PARTY SD PREVIEW · SELECT TARGET"] =
                "파티 SD 미리보기 · 대상 선택",
            ["Localization Editor"] = "로컬리제이션 편집기",
            ["Localization Key"] = "로컬리제이션 키",
            ["Fallback Description"] = "기본 설명",
            ["Fallback Name"] = "기본 이름",
            ["Description Localization Key"] = "설명 로컬리제이션 키",
            ["Name Localization Key"] = "이름 로컬리제이션 키",
            ["Title Localization Key"] = "제목 로컬리제이션 키",
            ["Role"] = "직군",
            ["Archetype"] = "세부 직군",
            ["Character"] = "캐릭터",
            ["Character Stat"] = "캐릭터 스탯",
            ["Stat Type"] = "능력치 종류",
            ["Modifier Category"] = "보정 종류",
            ["Stat Modifier"] = "능력치 보정",
            ["Status Effect"] = "상태 효과",
            ["Control Effect"] = "제어 효과",
            ["Trigger Block"] = "트리거 블록",
            ["Effect Block"] = "효과 블록",
            ["Target Faction"] = "대상 진영",
            ["Selection Mode"] = "선정 방식",
            ["Comparison Stat"] = "비교 수치",
            ["Target Count"] = "대상 수",
            ["Area Shape"] = "범위 형태",
            ["Area Origin"] = "범위 중심",
            ["Scale Multiplier"] = "크기 배율",
            ["Ground Offset"] = "지면 오프셋",
            ["Head Height"] = "머리 높이",
            ["Source Faces Right"] = "원본이 오른쪽을 바라봄",
            ["Flow Policy"] = "진행 정책",
            ["Field View Prefab"] = "필드 뷰 프리팹",
            ["Dungeon BGM Profile"] = "던전 BGM 프로필",
            ["Spawn VFX Cue"] = "스폰 VFX 큐",
            ["Death VFX Cue"] = "사망 VFX 큐",
            ["Rest Effects"] = "휴식 효과",
            ["Modifier Modules"] = "수정자 모듈",
            ["Upgrade ID"] = "업그레이드 ID",
            ["Effect ID"] = "효과 ID",
            ["Status ID"] = "상태 ID",
            ["Action ID"] = "행동 ID",
            ["Cue ID"] = "큐 ID",
            ["Tutorial"] = "튜토리얼",
            ["Weight"] = "가중치",
            ["Limit"] = "제한",
            ["Count"] = "수량",
            ["Ratio Weight"] = "비율 가중치",
            ["Element"] = "항목",
            ["Enemy"] = "적",
            ["Ally"] = "아군",
            ["Caster"] = "시전자",
            ["Self"] = "자신",
            ["Target"] = "대상",
            ["Ability"] = "능력",
            ["Passive"] = "패시브",
            ["Skill"] = "기술",
            ["Attack"] = "공격",
            ["Damage"] = "피해",
            ["Health"] = "체력",
            ["Shield"] = "보호막",
            ["Status"] = "상태",
            ["Stacks"] = "스택",
            ["Duration"] = "지속시간",
            ["Cooldown"] = "쿨다운",
            ["Resource"] = "자원",
            ["Cost"] = "비용",
            ["Amount"] = "수량",
            ["Grade"] = "등급",
            ["Reward"] = "보상",
            ["Currency"] = "재화",
            ["Consumable"] = "소모품",
            ["Battle Item"] = "전투 아이템",
            ["Battle Card"] = "전투 카드",
            ["Dungeon"] = "던전",
            ["Battle"] = "전투",
            ["Event"] = "이벤트",
            ["Rest"] = "휴식",
            ["Shop"] = "상점",
            ["Banner"] = "배너",
            ["Name"] = "이름",
            ["Description"] = "설명",
            ["Type"] = "종류",
            ["Mode"] = "방식",
            ["Area"] = "범위",
            ["Position"] = "위치",
            ["Rotation"] = "회전",
            ["Multiplier"] = "배율",
            ["Time"] = "시간",
            ["Path"] = "경로",
            ["List"] = "목록",
            ["Order"] = "순서",
            ["Settings"] = "설정",
            ["Enabled"] = "사용",
            ["Default"] = "기본",
            ["Current"] = "현재",
            ["Maximum"] = "최대",
            ["Minimum"] = "최소",
            ["Select"] = "선택",
            ["Add"] = "추가",
            ["Delete"] = "삭제",
            ["Remove"] = "제거",
            ["Duplicate"] = "복제",
            ["Create"] = "생성",
            ["Save"] = "저장",
            ["Cancel"] = "취소",
            ["Confirm"] = "확인",
            ["Search"] = "검색",
            ["Edit"] = "편집",
            ["Apply"] = "적용",
            ["Execute"] = "실행",
            ["Play"] = "재생",
            ["Stop"] = "정지",
            ["None"] = "없음",
            ["Unassigned"] = "미지정",
            ["Refresh"] = "새로고침",
            ["Select or create a RestSO."] = "RestSO를 선택하거나 생성하세요.",
            ["Select or create an EventSO."] = "EventSO를 선택하거나 생성하세요.",
            ["Select an EventSO from the list."] =
                "목록에서 EventSO를 선택하세요.",
            ["Shows the visible asset count and total asset count."] =
                "현재 표시되는 에셋 수와 전체 에셋 수를 보여 줍니다.",
            ["Apply To Client Scene"] = "클라이언트 씬에 적용",
            ["No Event Selected"] = "선택된 이벤트 없음",
            ["Event Description"] = "이벤트 설명",
            ["Asset Name"] = "에셋 이름",
            ["Missing Asset"] = "누락된 에셋",
            ["Invalid Action"] = "잘못된 행동",
            ["End Event"] = "이벤트 종료",
            ["Conditions"] = "조건",
            ["Rewards"] = "보상",
            ["Inspector"] = "인스펙터",
            ["Validate"] = "검증",
            ["Entry"] = "진입점",
            ["Assets"] = "에셋",
            ["Asset"] = "에셋",
            ["Links"] = "연결선",
            ["Link"] = "연결선",
            ["Graph"] = "그래프",
            ["Client"] = "클라이언트",
            ["Scene"] = "씬",
            ["All"] = "전체",
            ["Text"] = "텍스트",
            ["Color"] = "색상",
            ["Key"] = "키",
            ["Button"] = "버튼",
            ["Page"] = "페이지",
            ["World"] = "월드",
            ["Localization"] = "로컬리제이션",
            ["Icon"] = "아이콘",
            ["ID"] = "ID",
            ["Sprite"] = "스프라이트",
            ["Font"] = "글꼴",
            ["Title"] = "제목",
            ["Tab"] = "탭",
            ["Ring"] = "링",
            ["Prefab"] = "프리팹",
            ["Version"] = "버전",
            ["SD"] = "SD",
            ["Root"] = "루트",
            ["Core"] = "코어",
            ["Recruit"] = "모집",
            ["Value"] = "값",
            ["Fill"] = "채우기",
            ["Volume"] = "볼륨",
            ["Background"] = "배경",
            ["Interval"] = "간격",
            ["Height"] = "높이",
            ["Movement"] = "이동",
            ["Display"] = "표시",
            ["Single"] = "단일",
            ["Label"] = "라벨",
            ["Ready"] = "준비",
            ["Backdrop"] = "배경막",
            ["Filter"] = "필터",
            ["Content"] = "콘텐츠",
            ["Codex"] = "도감",
            ["Panel"] = "패널",
            ["Day"] = "일차",
            ["Row"] = "행",
            ["Outline"] = "외곽선",
            ["Size"] = "크기",
            ["Stack"] = "스택",
            ["Claimed"] = "수령 완료",
            ["Actor"] = "행동 주체",
            ["Available"] = "사용 가능",
            ["Tooltip"] = "툴팁",
            ["Frame"] = "프레임",
            ["Overlay"] = "오버레이",
            ["VFX"] = "VFX",
            ["Dropdown"] = "드롭다운",
            ["Camera"] = "카메라",
            ["Environment"] = "환경",
            ["Width"] = "너비",
            ["Slider"] = "슬라이더",
            ["Policy"] = "정책",
            ["Main"] = "메인",
            ["Fade"] = "페이드",
            ["Image"] = "이미지",
            ["Active"] = "활성",
            ["View"] = "뷰",
            ["Stage"] = "스테이지",
            ["Layout"] = "레이아웃",
            ["Cue"] = "큐",
            ["Runtime"] = "런타임",
            ["Selected"] = "선택됨",
            ["Field"] = "필드",
            ["Last"] = "마지막",
            ["Scale"] = "스케일",
            ["Track"] = "트랙",
            ["Owned"] = "보유",
            ["Destination"] = "목적지",
            ["Art"] = "아트",
            ["Rate"] = "비율",
            ["Initial"] = "초기",
            ["Style"] = "스타일",
            ["Family"] = "계열",
            ["Board"] = "보드",
            ["Quit"] = "종료",
            ["Pool"] = "풀",
            ["Starting"] = "시작",
            ["Characters"] = "캐릭터들",
            ["Marker"] = "마커",
            ["Per"] = "당",
            ["Sort"] = "정렬",
            ["Percent"] = "퍼센트",
            ["Player"] = "플레이어",
            ["Viewport"] = "뷰포트",
            ["Choice"] = "선택지",
            ["Renderer"] = "렌더러",
            ["Scan"] = "스캔",
            ["Action"] = "행동",
            ["Input"] = "입력",
            ["Hover"] = "마우스 오버",
            ["Category"] = "분류",
            ["Group"] = "그룹",
            ["Uses"] = "사용 횟수",
            ["Run"] = "런",
            ["Detail"] = "세부",
            ["Ground"] = "지면",
            ["Allow"] = "허용",
            ["Global"] = "전역",
            ["BGM"] = "BGM",
            ["Base"] = "기본",
            ["Offset"] = "오프셋",
            ["Padding"] = "여백",
            ["Locale"] = "언어",
            ["Override"] = "덮어쓰기",
            ["Schedule"] = "일정",
            ["Unowned"] = "미보유",
            ["Total"] = "합계",
            ["Accent"] = "강조",
            ["Radius"] = "반지름",
            ["Normal"] = "일반",
            ["Alpha"] = "알파",
            ["Message"] = "메시지",
            ["Spacing"] = "간격",
            ["Illustration"] = "일러스트",
            ["Cleared"] = "완료",
            ["Neutral"] = "중립",
            ["Skip"] = "건너뛰기",
            ["Phase"] = "단계",
            ["Catalog"] = "카탈로그",
            ["Setting"] = "설정",
            ["SFX"] = "SFX",
            ["Flow"] = "진행",
            ["Line"] = "선",
            ["Extra"] = "추가",
            ["Pause"] = "일시정지",
            ["Designer"] = "디자이너",
            ["Clip"] = "클립",
            ["Result"] = "결과",
            ["Popup"] = "팝업",
            ["Preview"] = "미리보기",
            ["Disabled"] = "비활성",
            ["Storage"] = "보관함",
            ["Room"] = "방",
            ["Inactive"] = "비활성",
            ["Start"] = "시작",
            ["Delay"] = "지연",
            ["Rect"] = "영역",
            ["Next"] = "다음",
            ["Route"] = "경로",
            ["Free"] = "무료",
            ["Unselected"] = "선택 안 됨",
            ["Paid"] = "유료",
            ["Include"] = "포함",
            ["Node"] = "노드",
            ["Completion"] = "완료",
            ["Period"] = "주기",
            ["Definitions"] = "정의 목록",
            ["Children"] = "하위 항목",
            ["Game"] = "게임",
            ["Focus"] = "초점",
            ["Normalized"] = "정규화",
            ["Unavailable"] = "사용 불가",
            ["Difficulty"] = "난이도",
            ["Clear"] = "초기화",
            ["Collapse"] = "접기",
            ["Claim"] = "수령",
            ["Timer"] = "타이머",
        };

    public static bool IsKoreanEditor
    {
        get
        {
            string localizedCancel = L10n.Tr("Cancel");
            return ContainsHangul(localizedCancel);
        }
    }

    public static string Tr(string source)
    {
        return TranslateForLanguage(source, IsKoreanEditor);
    }

    public static string Choose(string korean, string english)
    {
        return IsKoreanEditor
            ? korean ?? string.Empty
            : english ?? string.Empty;
    }

    public static void SetText(TextElement element, string source)
    {
        if (element == null)
            return;

        element.text = Tr(source);
        element.tooltip = BuildTooltip(element.text);
    }

    public static void SetBilingualText(
        TextElement element,
        string korean,
        string english)
    {
        if (element == null)
            return;

        element.text = Choose(korean, english);
        element.tooltip = BuildTooltip(element.text);
    }

    internal static string TranslateForLanguage(
        string source,
        bool korean)
    {
        if (string.IsNullOrEmpty(source))
            return source ?? string.Empty;

        if (korean)
        {
            if (ContainsHangul(source))
                return source;

            string unityTranslation = L10n.Tr(source);
            if (ContainsHangul(unityTranslation))
                return unityTranslation;

            return ReplacePhrases(source, EnglishPhrases);
        }

        if (!ContainsHangul(source))
            return source;

        string translated = ReplacePhrases(source, KoreanPhrases);
        translated = RemoveKoreanParticles(translated);
        translated = RemainingHangulWords.Replace(translated, "field");
        translated = Regex.Replace(translated, @"\s{2,}", " ").Trim();
        return string.IsNullOrWhiteSpace(translated)
            ? "Editor field"
            : translated;
    }

    public static string[] Tr(string[] source)
    {
        if (source == null)
            return Array.Empty<string>();

        string[] translated = new string[source.Length];
        for (int index = 0; index < source.Length; index++)
            translated[index] = Tr(source[index]);
        return translated;
    }

    public static UnityEngine.GUIContent Content(
        string text,
        string tooltip = null)
    {
        string translatedText = Tr(text);
        string translatedTooltip = string.IsNullOrWhiteSpace(tooltip)
            ? BuildTooltip(translatedText)
            : Tr(tooltip);
        return new UnityEngine.GUIContent(
            translatedText,
            translatedTooltip);
    }

    public static UnityEngine.GUIContent Normalize(
        UnityEngine.GUIContent content)
    {
        if (content == null || ReferenceEquals(content, UnityEngine.GUIContent.none))
            return content ?? UnityEngine.GUIContent.none;

        string text = Tr(content.text);
        string tooltip = string.IsNullOrWhiteSpace(content.tooltip)
            ? BuildTooltip(text)
            : Tr(content.tooltip);
        return new UnityEngine.GUIContent(text, content.image, tooltip);
    }

    public static UnityEngine.GUIContent[] Normalize(
        UnityEngine.GUIContent[] contents)
    {
        if (contents == null)
            return Array.Empty<UnityEngine.GUIContent>();

        UnityEngine.GUIContent[] translated =
            new UnityEngine.GUIContent[contents.Length];
        for (int index = 0; index < contents.Length; index++)
            translated[index] = Normalize(contents[index]);
        return translated;
    }

    public static string BuildTooltip(string translatedText)
    {
        string label = string.IsNullOrWhiteSpace(translatedText)
            ? (IsKoreanEditor ? "이 항목" : "this field")
            : translatedText.Trim();
        return IsKoreanEditor
            ? $"{label} 항목입니다. 값을 확인하거나 설정합니다."
            : $"View or configure {label}.";
    }

    public static bool DrawDefaultInspector(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return false;

        serializedObject.UpdateIfRequiredOrScript();
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        EditorGUI.BeginChangeCheck();
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            using (new EditorGUI.DisabledScope(
                       string.Equals(
                           iterator.propertyPath,
                           "m_Script",
                           StringComparison.Ordinal)))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();
        return changed;
    }

    private static string ReplacePhrases(
        string source,
        IReadOnlyDictionary<string, string> phrases)
    {
        string result = source;
        List<KeyValuePair<string, string>> ordered = new(phrases);
        ordered.Sort((left, right) =>
            right.Key.Length.CompareTo(left.Key.Length));
        foreach (KeyValuePair<string, string> pair in ordered)
        {
            result = result.Replace(
                pair.Key,
                pair.Value,
                StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private static string RemoveKoreanParticles(string value)
    {
        return Regex.Replace(
            value,
            @"(?:에서|으로|에게|부터|까지|보다|처럼|을|를|이|가|은|는|의|에|로|와|과|도|만)(?=\s|[.,:;!?)]|$)",
            string.Empty);
    }

    private static bool ContainsHangul(string value)
    {
        return !string.IsNullOrEmpty(value) && HangulPattern.IsMatch(value);
    }
}

[CustomPropertyDrawer(typeof(HeaderAttribute))]
internal sealed class PS260714LocalizedHeaderDrawer : DecoratorDrawer
{
    public override float GetHeight()
    {
        return EditorGUIUtility.singleLineHeight + 8f;
    }

    public override void OnGUI(Rect position)
    {
        HeaderAttribute header = (HeaderAttribute)attribute;
        position.y += 6f;
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(
            position,
            PS260714EditorText.Content(header.header),
            EditorStyles.boldLabel);
    }
}

/// <summary>
/// Odin property trees bypass Unity's EditorGUILayout facade. This processor
/// gives every project-owned runtime data member the same localized label and
/// tooltip policy without changing its serialization attributes.
/// </summary>
internal sealed class PS260714LocalizedOdinAttributeProcessor :
    OdinAttributeProcessor
{
    private static readonly Assembly RuntimeDataAssembly =
        typeof(CharacterSO).Assembly;

    public override bool CanProcessChildMemberAttributes(
        InspectorProperty parentProperty,
        MemberInfo member)
    {
        return member?.DeclaringType?.Assembly == RuntimeDataAssembly;
    }

    public override void ProcessChildMemberAttributes(
        InspectorProperty parentProperty,
        MemberInfo member,
        List<Attribute> attributes)
    {
        if (member == null || attributes == null)
            return;

        string sourceLabel = ObjectNames.NicifyVariableName(member.Name);
        string sourceTooltip = null;
        for (int index = attributes.Count - 1; index >= 0; index--)
        {
            switch (attributes[index])
            {
                case LabelTextAttribute labelText
                    when !string.IsNullOrWhiteSpace(labelText.Text) &&
                         !labelText.Text.StartsWith("$", StringComparison.Ordinal) &&
                         !labelText.Text.StartsWith("@", StringComparison.Ordinal):
                    sourceLabel = labelText.Text;
                    attributes.RemoveAt(index);
                    break;
                case PropertyTooltipAttribute propertyTooltip
                    when !string.IsNullOrWhiteSpace(propertyTooltip.Tooltip):
                    sourceTooltip = propertyTooltip.Tooltip;
                    attributes.RemoveAt(index);
                    break;
                case TooltipAttribute unityTooltip
                    when !string.IsNullOrWhiteSpace(unityTooltip.tooltip):
                    sourceTooltip = unityTooltip.tooltip;
                    attributes.RemoveAt(index);
                    break;
            }
        }

        string localizedLabel = PS260714EditorText.Tr(sourceLabel);
        attributes.Add(new LabelTextAttribute(localizedLabel, false));
        attributes.Add(new PropertyTooltipAttribute(
            string.IsNullOrWhiteSpace(sourceTooltip)
                ? PS260714EditorText.BuildTooltip(localizedLabel)
                : PS260714EditorText.Tr(sourceTooltip)));
    }
}

/// <summary>
/// Drop-in GUIContent that translates its label and guarantees a tooltip.
/// Existing editor code in the predefined editor assembly resolves this type
/// before UnityEngine.GUIContent, so custom data editors inherit the policy.
/// </summary>
internal sealed class GUIContent : UnityEngine.GUIContent
{
    public GUIContent()
    {
    }

    public GUIContent(string text)
        : base(
            PS260714EditorText.Tr(text),
            PS260714EditorText.BuildTooltip(PS260714EditorText.Tr(text)))
    {
    }

    public GUIContent(string text, string tooltip)
        : base(
            PS260714EditorText.Tr(text),
            string.IsNullOrWhiteSpace(tooltip)
                ? PS260714EditorText.BuildTooltip(
                    PS260714EditorText.Tr(text))
                : PS260714EditorText.Tr(tooltip))
    {
    }

    public GUIContent(string text, Texture image)
        : base(
            PS260714EditorText.Tr(text),
            image,
            PS260714EditorText.BuildTooltip(PS260714EditorText.Tr(text)))
    {
    }

    public GUIContent(string text, Texture image, string tooltip)
        : base(
            PS260714EditorText.Tr(text),
            image,
            string.IsNullOrWhiteSpace(tooltip)
                ? PS260714EditorText.BuildTooltip(
                    PS260714EditorText.Tr(text))
                : PS260714EditorText.Tr(tooltip))
    {
    }

    public GUIContent(Texture image)
        : base(image)
    {
    }

    public GUIContent(Texture image, string tooltip)
        : base(image, PS260714EditorText.Tr(tooltip))
    {
    }

    public GUIContent(UnityEngine.GUIContent source)
        : base(PS260714EditorText.Normalize(source))
    {
    }
}

/// <summary>
/// Localized facade for the IMGUI layout calls used by project editor tools.
/// </summary>
internal static class EditorGUILayout
{
    public sealed class HorizontalScope : IDisposable
    {
        private readonly UnityEditor.EditorGUILayout.HorizontalScope scope;

        public HorizontalScope(params GUILayoutOption[] options)
        {
            scope = new UnityEditor.EditorGUILayout.HorizontalScope(options);
        }

        public HorizontalScope(GUIStyle style, params GUILayoutOption[] options)
        {
            scope = new UnityEditor.EditorGUILayout.HorizontalScope(style, options);
        }

        public Rect rect => scope.rect;
        public void Dispose() => scope.Dispose();
    }

    public sealed class VerticalScope : IDisposable
    {
        private readonly UnityEditor.EditorGUILayout.VerticalScope scope;

        public VerticalScope(params GUILayoutOption[] options)
        {
            scope = new UnityEditor.EditorGUILayout.VerticalScope(options);
        }

        public VerticalScope(GUIStyle style, params GUILayoutOption[] options)
        {
            scope = new UnityEditor.EditorGUILayout.VerticalScope(style, options);
        }

        public Rect rect => scope.rect;
        public void Dispose() => scope.Dispose();
    }

    public sealed class ScrollViewScope : IDisposable
    {
        private readonly UnityEditor.EditorGUILayout.ScrollViewScope scope;

        public ScrollViewScope(Vector2 scrollPosition, params GUILayoutOption[] options)
        {
            scope = new UnityEditor.EditorGUILayout.ScrollViewScope(
                scrollPosition,
                options);
        }

        public Vector2 scrollPosition => scope.scrollPosition;
        public void Dispose() => scope.Dispose();
    }

    public static void BeginHorizontal(params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginHorizontal(options);

    public static void BeginHorizontal(
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginHorizontal(style, options);

    public static void EndHorizontal() =>
        UnityEditor.EditorGUILayout.EndHorizontal();

    public static void BeginVertical(params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginVertical(options);

    public static void BeginVertical(
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginVertical(style, options);

    public static void EndVertical() =>
        UnityEditor.EditorGUILayout.EndVertical();

    public static Vector2 BeginScrollView(
        Vector2 scrollPosition,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginScrollView(scrollPosition, options);

    public static Vector2 BeginScrollView(
        Vector2 scrollPosition,
        GUIStyle background,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginScrollView(
            scrollPosition,
            background,
            options);

    public static Vector2 BeginScrollView(
        Vector2 scrollPosition,
        bool alwaysShowHorizontal,
        bool alwaysShowVertical,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.BeginScrollView(
            scrollPosition,
            alwaysShowHorizontal,
            alwaysShowVertical,
            options);

    public static void EndScrollView() =>
        UnityEditor.EditorGUILayout.EndScrollView();

    public static bool BeginFoldoutHeaderGroup(
        bool foldout,
        string content) =>
        UnityEditor.EditorGUILayout.BeginFoldoutHeaderGroup(
            foldout,
            PS260714EditorText.Content(content));

    public static bool BeginFoldoutHeaderGroup(
        bool foldout,
        UnityEngine.GUIContent content,
        GUIStyle style = null,
        Action<Rect> menuAction = null,
        GUIStyle menuIcon = null) =>
        UnityEditor.EditorGUILayout.BeginFoldoutHeaderGroup(
            foldout,
            PS260714EditorText.Normalize(content),
            style,
            menuAction,
            menuIcon);

    public static void EndFoldoutHeaderGroup() =>
        UnityEditor.EditorGUILayout.EndFoldoutHeaderGroup();

    public static void Space() => UnityEditor.EditorGUILayout.Space();
    public static void Space(float pixels) =>
        UnityEditor.EditorGUILayout.Space(pixels);

    public static Rect GetControlRect(
        bool hasLabel = true,
        float height = 18f,
        GUIStyle style = null,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.GetControlRect(
            hasLabel,
            height,
            style ?? GUIStyle.none,
            options);

    public static Rect GetControlRect(
        bool hasLabel,
        float height,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.GetControlRect(
            hasLabel,
            height,
            options);

    public static void HelpBox(
        string message,
        MessageType type,
        bool wide = true) =>
        UnityEditor.EditorGUILayout.HelpBox(
            PS260714EditorText.Tr(message),
            type,
            wide);

    public static void LabelField(
        string label,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Content(label),
            options);

    public static void LabelField(
        string label,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Content(label),
            style,
            options);

    public static void LabelField(
        string label,
        string label2,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Content(label),
            PS260714EditorText.Content(label2),
            options);

    public static void LabelField(
        string label,
        string label2,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Content(label),
            PS260714EditorText.Content(label2),
            style,
            options);

    public static void LabelField(
        UnityEngine.GUIContent label,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Normalize(label),
            options);

    public static void LabelField(
        UnityEngine.GUIContent label,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.LabelField(
            PS260714EditorText.Normalize(label),
            style,
            options);

    public static void PropertyField(
        SerializedProperty property,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.PropertyField(
            property,
            PS260714EditorText.Content(
                property?.displayName,
                property?.tooltip),
            false,
            options);

    public static void PropertyField(
        SerializedProperty property,
        bool includeChildren,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.PropertyField(
            property,
            PS260714EditorText.Content(
                property?.displayName,
                property?.tooltip),
            includeChildren,
            options);

    public static void PropertyField(
        SerializedProperty property,
        UnityEngine.GUIContent label,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.PropertyField(
            property,
            PS260714EditorText.Normalize(label),
            false,
            options);

    public static void PropertyField(
        SerializedProperty property,
        UnityEngine.GUIContent label,
        bool includeChildren,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.PropertyField(
            property,
            PS260714EditorText.Normalize(label),
            includeChildren,
            options);

    public static string TextField(
        string label,
        string text,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextField(
            PS260714EditorText.Content(label),
            text,
            options);

    public static string TextField(
        UnityEngine.GUIContent label,
        string text,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextField(
            PS260714EditorText.Normalize(label),
            text,
            options);

    public static string TextField(
        string text,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextField(text, options);

    public static string TextField(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextField(text, style, options);

    public static string TextArea(
        string text,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextArea(text, options);

    public static string TextArea(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.TextArea(text, style, options);

    public static float FloatField(
        string label,
        float value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.FloatField(
            PS260714EditorText.Content(label),
            value,
            options);

    public static float FloatField(
        UnityEngine.GUIContent label,
        float value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.FloatField(
            PS260714EditorText.Normalize(label),
            value,
            options);

    public static float FloatField(
        float value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.FloatField(value, options);

    public static int IntField(
        string label,
        int value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntField(
            PS260714EditorText.Content(label),
            value,
            options);

    public static int IntField(
        UnityEngine.GUIContent label,
        int value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntField(
            PS260714EditorText.Normalize(label),
            value,
            options);

    public static int IntField(
        int value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntField(value, options);

    public static int DelayedIntField(
        string label,
        int value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.DelayedIntField(
            PS260714EditorText.Content(label),
            value,
            options);

    public static bool Toggle(
        string label,
        bool value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Toggle(
            PS260714EditorText.Content(label),
            value,
            options);

    public static bool Toggle(
        UnityEngine.GUIContent label,
        bool value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Toggle(
            PS260714EditorText.Normalize(label),
            value,
            options);

    public static bool ToggleLeft(
        string label,
        bool value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.ToggleLeft(
            PS260714EditorText.Content(label),
            value,
            options);

    public static bool ToggleLeft(
        UnityEngine.GUIContent label,
        bool value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.ToggleLeft(
            PS260714EditorText.Normalize(label),
            value,
            options);

    public static bool Toggle(
        bool value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Toggle(value, options);

    public static int Popup(
        string label,
        int selectedIndex,
        string[] displayedOptions,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Popup(
            PS260714EditorText.Content(label),
            selectedIndex,
            PS260714EditorText.Tr(displayedOptions),
            options);

    public static int Popup(
        int selectedIndex,
        string[] displayedOptions,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Popup(
            selectedIndex,
            PS260714EditorText.Tr(displayedOptions),
            options);

    public static int Popup(
        int selectedIndex,
        string[] displayedOptions,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Popup(
            selectedIndex,
            PS260714EditorText.Tr(displayedOptions),
            style,
            options);

    public static int Popup(
        UnityEngine.GUIContent label,
        int selectedIndex,
        string[] displayedOptions,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Popup(
            PS260714EditorText.Normalize(label),
            selectedIndex,
            PS260714EditorText.Tr(displayedOptions),
            options);

    public static int Popup(
        UnityEngine.GUIContent label,
        int selectedIndex,
        UnityEngine.GUIContent[] displayedOptions,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Popup(
            PS260714EditorText.Normalize(label),
            selectedIndex,
            PS260714EditorText.Normalize(displayedOptions),
            options);

    public static int IntPopup(
        string label,
        int selectedValue,
        string[] displayedOptions,
        int[] optionValues,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntPopup(
            PS260714EditorText.Tr(label),
            selectedValue,
            PS260714EditorText.Tr(displayedOptions),
            optionValues,
            options);

    public static float Slider(
        string label,
        float value,
        float leftValue,
        float rightValue,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Slider(
            PS260714EditorText.Content(label),
            value,
            leftValue,
            rightValue,
            options);

    public static float Slider(
        UnityEngine.GUIContent label,
        float value,
        float leftValue,
        float rightValue,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Slider(
            PS260714EditorText.Normalize(label),
            value,
            leftValue,
            rightValue,
            options);

    public static int IntSlider(
        string label,
        int value,
        int leftValue,
        int rightValue,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntSlider(
            PS260714EditorText.Content(label),
            value,
            leftValue,
            rightValue,
            options);

    public static int IntSlider(
        UnityEngine.GUIContent label,
        int value,
        int leftValue,
        int rightValue,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntSlider(
            PS260714EditorText.Normalize(label),
            value,
            leftValue,
            rightValue,
            options);

    public static void IntSlider(
        SerializedProperty property,
        int leftValue,
        int rightValue,
        UnityEngine.GUIContent label,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.IntSlider(
            property,
            leftValue,
            rightValue,
            PS260714EditorText.Normalize(label),
            options);

    public static UnityEngine.Object ObjectField(
        string label,
        UnityEngine.Object value,
        Type objectType,
        bool allowSceneObjects,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.ObjectField(
            PS260714EditorText.Content(label),
            value,
            objectType,
            allowSceneObjects,
            options);

    public static UnityEngine.Object ObjectField(
        UnityEngine.GUIContent label,
        UnityEngine.Object value,
        Type objectType,
        bool allowSceneObjects,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.ObjectField(
            PS260714EditorText.Normalize(label),
            value,
            objectType,
            allowSceneObjects,
            options);

    public static UnityEngine.Object ObjectField(
        UnityEngine.Object value,
        Type objectType,
        bool allowSceneObjects,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.ObjectField(
            value,
            objectType,
            allowSceneObjects,
            options);

    public static bool Foldout(
        bool foldout,
        string content,
        bool toggleOnLabelClick = false,
        GUIStyle style = null) =>
        UnityEditor.EditorGUILayout.Foldout(
            foldout,
            PS260714EditorText.Content(content),
            toggleOnLabelClick,
            style ?? EditorStyles.foldout);

    public static bool Foldout(
        bool foldout,
        UnityEngine.GUIContent content,
        bool toggleOnLabelClick = false,
        GUIStyle style = null) =>
        UnityEditor.EditorGUILayout.Foldout(
            foldout,
            PS260714EditorText.Normalize(content),
            toggleOnLabelClick,
            style ?? EditorStyles.foldout);

    public static void PrefixLabel(string label) =>
        UnityEditor.EditorGUILayout.PrefixLabel(
            PS260714EditorText.Content(label));

    public static void PrefixLabel(UnityEngine.GUIContent label) =>
        UnityEditor.EditorGUILayout.PrefixLabel(
            PS260714EditorText.Normalize(label));

    public static void SelectableLabel(
        string text,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.SelectableLabel(text, options);

    public static void SelectableLabel(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.SelectableLabel(text, style, options);

    public static Vector2 Vector2Field(
        string label,
        Vector2 value,
        params GUILayoutOption[] options) =>
        UnityEditor.EditorGUILayout.Vector2Field(
            PS260714EditorText.Content(label),
            value,
            options);
}

/// <summary>
/// Localized facade for layout buttons, labels, toggles and toolbars.
/// </summary>
internal static class GUILayout
{
    public static GUILayoutOption Width(float width) =>
        UnityEngine.GUILayout.Width(width);
    public static GUILayoutOption Height(float height) =>
        UnityEngine.GUILayout.Height(height);
    public static GUILayoutOption MinWidth(float width) =>
        UnityEngine.GUILayout.MinWidth(width);
    public static GUILayoutOption MinHeight(float height) =>
        UnityEngine.GUILayout.MinHeight(height);
    public static GUILayoutOption MaxHeight(float height) =>
        UnityEngine.GUILayout.MaxHeight(height);
    public static GUILayoutOption ExpandWidth(bool expand) =>
        UnityEngine.GUILayout.ExpandWidth(expand);
    public static GUILayoutOption ExpandHeight(bool expand) =>
        UnityEngine.GUILayout.ExpandHeight(expand);
    public static void FlexibleSpace() =>
        UnityEngine.GUILayout.FlexibleSpace();
    public static void Space(float pixels) =>
        UnityEngine.GUILayout.Space(pixels);

    public static bool Button(
        string text,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(
            PS260714EditorText.Content(text),
            options);

    public static bool Button(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(
            PS260714EditorText.Content(text),
            style,
            options);

    public static bool Button(
        UnityEngine.GUIContent content,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(
            PS260714EditorText.Normalize(content),
            options);

    public static bool Button(
        UnityEngine.GUIContent content,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(
            PS260714EditorText.Normalize(content),
            style,
            options);

    public static bool Button(
        Texture image,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(image, options);

    public static bool Button(
        Texture image,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Button(image, style, options);

    public static void Label(
        string text,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Label(
            PS260714EditorText.Content(text),
            options);

    public static void Label(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Label(
            PS260714EditorText.Content(text),
            style,
            options);

    public static void Label(
        UnityEngine.GUIContent content,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Label(
            PS260714EditorText.Normalize(content),
            options);

    public static void Label(
        UnityEngine.GUIContent content,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Label(
            PS260714EditorText.Normalize(content),
            style,
            options);

    public static void Label(
        Texture image,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Label(image, options);

    public static bool Toggle(
        bool value,
        string text,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toggle(
            value,
            PS260714EditorText.Content(text),
            options);

    public static bool Toggle(
        bool value,
        string text,
        string style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toggle(
            value,
            PS260714EditorText.Content(text),
            style,
            options);

    public static bool Toggle(
        bool value,
        UnityEngine.GUIContent content,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toggle(
            value,
            PS260714EditorText.Normalize(content),
            options);

    public static string TextField(
        string text,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.TextField(text, options);

    public static string TextField(
        string text,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.TextField(text, style, options);

    public static int Toolbar(
        int selected,
        string[] texts,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toolbar(
            selected,
            PS260714EditorText.Tr(texts),
            options);

    public static int Toolbar(
        int selected,
        string[] texts,
        GUIStyle style,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toolbar(
            selected,
            PS260714EditorText.Tr(texts),
            style,
            options);

    public static int Toolbar(
        int selected,
        UnityEngine.GUIContent[] contents,
        params GUILayoutOption[] options) =>
        UnityEngine.GUILayout.Toolbar(
            selected,
            PS260714EditorText.Normalize(contents),
            options);
}
