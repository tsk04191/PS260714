using System;
using System.Collections.Generic;
using System.IO;
using PS260714.Localization;
using PS260714.Localization.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(GameManager))]
public sealed class GameManagerEditor : Editor
{
    private SerializedProperty _defaultLobbyRepresentative;
    private SerializedProperty _lobbyRepresentativeTogglePrefab;

    private void OnEnable()
    {
        _defaultLobbyRepresentative =
            serializedObject.FindProperty(
                "defaultLobbyRepresentative");
        _lobbyRepresentativeTogglePrefab =
            serializedObject.FindProperty(
                "lobbyRepresentativeTogglePrefab");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "defaultLobbyRepresentative",
            "lobbyRepresentativeTogglePrefab");

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "메인 화면 대표 대원",
            EditorStyles.boldLabel);
        DrawDefaultRepresentativePopup();
        EditorGUILayout.PropertyField(
            _lobbyRepresentativeTogglePrefab,
            new GUIContent("대표 설정 토글 프리팹"));

        if (_lobbyRepresentativeTogglePrefab.objectReferenceValue ==
            null)
        {
            EditorGUILayout.HelpBox(
                "대원 상세 화면에서 사용할 btnToggle 프리팹을 " +
                "지정하세요.",
                MessageType.Warning);
        }

        DrawCommonSettings();
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawCommonSettings()
    {
        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField(
            "빌드 및 공통 설정",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle(
                new GUIContent(
                    "새 빌드에서 로컬 데이터 초기화",
                    "CommonDef.ResetLocalDataOnNewBuild 코드 상수입니다."),
                CommonDef.ResetLocalDataOnNewBuild);
        }

        if (CommonDef.ResetLocalDataOnNewBuild)
        {
            EditorGUILayout.HelpBox(
                "개발 빌드의 GUID가 변경되면 첫 실행에서 PlayerPrefs를 " +
                "초기화합니다. 배포 전 CommonDef의 값을 false로 변경하세요.",
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("CommonDef.cs 열기"))
                CommonSettingsEditorUtility.OpenCommonDefSource();
            if (GUILayout.Button("Project Settings에서 편집"))
                CommonSettingsProjectProvider.Open();
        }

        CharacterGradePaletteSO palette =
            CommonSettingsEditorUtility.LoadGradePalette();
        if (palette == null)
        {
            EditorGUILayout.HelpBox(
                "공통 캐릭터 등급 팔레트가 없습니다.",
                MessageType.Error);
            if (GUILayout.Button("기본 등급 팔레트 생성"))
            {
                palette =
                    CommonSettingsEditorUtility.CreateGradePalette();
            }
        }
        else
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "등급 팔레트",
                    palette,
                    typeof(CharacterGradePaletteSO),
                    false);
            }
        }

        CharacterRoleCatalogSO roleCatalog =
            CommonSettingsEditorUtility.LoadRoleCatalog();
        if (roleCatalog == null)
        {
            EditorGUILayout.HelpBox(
                "공통 직군/세부 직군 카탈로그가 없습니다.",
                MessageType.Warning);
            if (GUILayout.Button("기본 직군 카탈로그 생성"))
            {
                roleCatalog =
                    CommonSettingsEditorUtility.CreateRoleCatalog();
            }
        }
        else
        {
            int roleCount = 0;
            int archetypeCount = 0;
            int passiveCount = 0;
            foreach (CharacterRoleSO role in roleCatalog.Roles)
            {
                if (role == null)
                    continue;
                roleCount++;
                foreach (CharacterRolePassiveDefinition passive in
                         role.PassiveDefinitions)
                {
                    if (passive != null && passive.IsConfigured)
                        passiveCount++;
                }
            }
            foreach (CharacterArchetypeSO archetype in
                     roleCatalog.Archetypes)
            {
                if (archetype == null)
                    continue;
                archetypeCount++;
                foreach (CharacterRolePassiveDefinition passive in
                         archetype.PassiveDefinitions)
                {
                    if (passive != null && passive.IsConfigured)
                        passiveCount++;
                }
            }

            EditorGUILayout.LabelField(
                "직군 데이터",
                $"직군 {roleCount} / 세부 직군 {archetypeCount} / " +
                $"패시브 {passiveCount}");
            IReadOnlyList<string> issues =
                roleCatalog.GetValidationIssues();
            EditorGUILayout.HelpBox(
                issues.Count == 0
                    ? "공통 직군 데이터가 유효합니다."
                    : $"공통 직군 데이터에 {issues.Count}개의 " +
                      "검토 항목이 있습니다. Project Settings에서 확인하세요.",
                issues.Count == 0
                    ? MessageType.Info
                    : MessageType.Warning);
        }
    }

    private void DrawDefaultRepresentativePopup()
    {
        List<CharacterSO> eligible = LoadEligibleCharacters();
        string[] labels = new string[eligible.Count + 1];
        labels[0] = "없음";
        for (int index = 0; index < eligible.Count; index++)
        {
            CharacterSO character = eligible[index];
            labels[index + 1] =
                $"{character.name}  [{character.CharacterId}]";
        }

        CharacterSO current =
            _defaultLobbyRepresentative.objectReferenceValue
                as CharacterSO;
        int selectedIndex = 0;
        for (int index = 0; index < eligible.Count; index++)
        {
            if (eligible[index] == current)
            {
                selectedIndex = index + 1;
                break;
            }
        }

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(
                "기본 대표 대원",
                "CharacterSO에서 기본 보유가 체크된 대원만 표시됩니다."),
            selectedIndex,
            labels);
        if (EditorGUI.EndChangeCheck())
        {
            _defaultLobbyRepresentative.objectReferenceValue =
                nextIndex > 0 && nextIndex <= eligible.Count
                    ? eligible[nextIndex - 1]
                    : null;
        }

        if (current != null &&
            !GameManager.IsEligibleDefaultLobbyRepresentative(current))
        {
            EditorGUILayout.HelpBox(
                "현재 대원은 기본 보유가 해제되어 기본 대표로 " +
                "사용할 수 없습니다.",
                MessageType.Error);
        }
        else if (eligible.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "기본 보유가 체크된 CharacterSO가 없습니다.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "저장된 사용자 선택이 없을 때 이 대원이 메인 화면에 " +
                "표시됩니다.",
                MessageType.Info);
        }
    }

    private static List<CharacterSO> LoadEligibleCharacters()
    {
        List<CharacterSO> result = new();
        string[] guids = AssetDatabase.FindAssets("t:CharacterSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSO character =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(path);
            if (GameManager.IsEligibleDefaultLobbyRepresentative(
                    character))
            {
                result.Add(character);
            }
        }

        result.Sort((left, right) => string.Compare(
            left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty,
            StringComparison.OrdinalIgnoreCase));
        return result;
    }
}

[CustomEditor(typeof(CharacterGradePaletteSO))]
public sealed class CharacterGradePaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CommonSettingsEditorGUI.DrawGradePalette(
            (CharacterGradePaletteSO)target,
            true);
    }
}

[CustomEditor(typeof(CharacterRoleCatalogSO))]
public sealed class CharacterRoleCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CommonSettingsEditorGUI.DrawRoleCatalog(
            (CharacterRoleCatalogSO)target);
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Project Settings에서 전체 편집"))
            CommonSettingsProjectProvider.Open();
    }
}

[CustomEditor(typeof(CharacterRoleSO))]
public sealed class CharacterRoleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CommonSettingsEditorGUI.DrawRoleDefinition(
            (CharacterRoleSO)target);
    }
}

[CustomEditor(typeof(CharacterArchetypeSO))]
public sealed class CharacterArchetypeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CommonSettingsEditorGUI.DrawArchetypeDefinition(
            (CharacterArchetypeSO)target);
    }
}

internal static class CommonSettingsEditorGUI
{
    public static void DrawGradePalette(
        CharacterGradePaletteSO palette,
        bool drawPreview)
    {
        if (palette == null)
            return;
        CommonSettingsEditorUtility.DrawGradePaletteEditor(palette);
        if (drawPreview)
        {
            EditorGUILayout.Space(8f);
            CommonSettingsEditorUtility.DrawGradePalettePreview(
                palette);
        }
    }

    public static void DrawRoleCatalog(
        CharacterRoleCatalogSO catalog,
        bool drawCreateButtons = true)
    {
        CommonSettingsEditorUtility.DrawRoleCatalogEditor(
            catalog,
            drawCreateButtons);
    }

    public static void DrawRoleDefinition(CharacterRoleSO role)
    {
        if (role == null)
            return;
        EditorGUILayout.LabelField(
            "직군 및 직군 패시브",
            EditorStyles.boldLabel);
        DrawLocalizationControls();

        SerializedObject serialized = new(role);
        serialized.UpdateIfRequiredOrScript();
        DrawReadOnlyProperty(serialized, "roleId", "직군 ID");
        PS260714LocalizationKeyField.Draw(
            serialized.FindProperty("nameLocalizationKey"),
            "이름 Localization 키");
        DrawProperty(serialized, "fallbackName", "이름 fallback");
        PS260714LocalizationKeyField.Draw(
            serialized.FindProperty("descriptionLocalizationKey"),
            "설명 Localization 키");
        DrawProperty(
            serialized,
            "fallbackDescription",
            "설명 fallback");
        DrawProperty(serialized, "iconSprite", "직군 아이콘");
        DrawRolePassives(
            serialized.FindProperty("passiveDefinitions"),
            "직군");
        ApplyDefinition(serialized, role);

        DrawLocalizationPreview(
            "직군 이름",
            role.NameLocalizationKey,
            role.GetDisplayName());
        DrawOptionalLocalizationPreview(
            "직군 설명",
            role.DescriptionLocalizationKey,
            role.GetDescription());
        int passiveIndex = 1;
        foreach (CharacterRolePassiveDefinition passive in
                 role.PassiveDefinitions)
        {
            if (passive == null || !passive.IsConfigured)
                continue;
            DrawLocalizationPreview(
                $"직군 패시브 {passiveIndex} 이름",
                passive.NameLocalizationKey,
                passive.GetDisplayName());
            string description = passive.GetDescription();
            if (!string.IsNullOrWhiteSpace(description))
            {
                DrawLocalizationPreview(
                    $"직군 패시브 {passiveIndex} 설명",
                    passive.DescriptionLocalizationKey,
                    description);
            }
            passiveIndex++;
        }
        DrawDefinitionFooter(role);
    }

    public static void DrawArchetypeDefinition(
        CharacterArchetypeSO archetype)
    {
        if (archetype == null)
            return;
        EditorGUILayout.LabelField(
            "세부 직군",
            EditorStyles.boldLabel);
        DrawLocalizationControls();

        SerializedObject serialized = new(archetype);
        serialized.UpdateIfRequiredOrScript();
        DrawReadOnlyProperty(serialized, "archetypeId", "세부 직군 ID");
        PS260714AssetReferenceField.Draw(
            serialized.FindProperty("parentRole"),
            new GUIContent(
                "분류 직군",
                "세부 직군을 정리하기 위한 분류 정보입니다. " +
                "캐릭터의 직군 선택을 제한하지 않습니다."));
        PS260714LocalizationKeyField.Draw(
            serialized.FindProperty("nameLocalizationKey"),
            "이름 Localization 키");
        DrawProperty(serialized, "fallbackName", "이름 fallback");
        PS260714LocalizationKeyField.Draw(
            serialized.FindProperty("descriptionLocalizationKey"),
            "설명 Localization 키");
        DrawProperty(
            serialized,
            "fallbackDescription",
            "설명 fallback");
        DrawProperty(serialized, "iconSprite", "세부 직군 아이콘");
        DrawRolePassives(
            serialized.FindProperty("passiveDefinitions"),
            "세부 직군");
        ApplyDefinition(serialized, archetype);

        DrawLocalizationPreview(
            "세부 직군 이름",
            archetype.NameLocalizationKey,
            archetype.GetDisplayName());
        DrawOptionalLocalizationPreview(
            "세부 직군 설명",
            archetype.DescriptionLocalizationKey,
            archetype.GetDescription());
        int passiveIndex = 1;
        foreach (CharacterRolePassiveDefinition passive in
                 archetype.PassiveDefinitions)
        {
            if (passive == null || !passive.IsConfigured)
                continue;
            DrawLocalizationPreview(
                $"세부 직군 패시브 {passiveIndex} 이름",
                passive.NameLocalizationKey,
                passive.GetDisplayName());
            string description = passive.GetDescription();
            if (!string.IsNullOrWhiteSpace(description))
            {
                DrawLocalizationPreview(
                    $"세부 직군 패시브 {passiveIndex} 설명",
                    passive.DescriptionLocalizationKey,
                    description);
            }
            passiveIndex++;
        }
        DrawDefinitionFooter(archetype);
    }

    private static void DrawLocalizationControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                "strings.csv 키를 계층형 목록에서 선택합니다.",
                EditorStyles.miniLabel);
            if (GUILayout.Button("키 새로고침", GUILayout.Width(86f)))
                PS260714LocalizationKeyField.Refresh();
            if (GUILayout.Button("Localization 편집", GUILayout.Width(112f)))
                LocalizationEditorWindow.Open();
        }
        PS260714LocalizationKeyField.DrawLoadError();
    }

    private static void ApplyDefinition(
        SerializedObject serialized,
        UnityEngine.Object asset)
    {
        if (serialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(asset);
            CharacterRolePresentation.Invalidate();
        }
    }

    private static void DrawDefinitionFooter(UnityEngine.Object asset)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Project에서 선택"))
                CommonSettingsEditorUtility.SelectAsset(asset);
            if (GUILayout.Button("공통 설정 열기"))
                CommonSettingsProjectProvider.Open();
        }
    }

    private static void DrawRolePassives(
        SerializedProperty passives,
        string ownerLabel)
    {
        if (passives == null)
            return;

        EditorGUILayout.Space(6f);
        passives.isExpanded = EditorGUILayout.Foldout(
            passives.isExpanded,
            $"{ownerLabel} 패시브 ({passives.arraySize})",
            true,
            EditorStyles.foldoutHeader);
        if (!passives.isExpanded)
            return;

        int removeIndex = -1;
        for (int index = 0; index < passives.arraySize; index++)
        {
            SerializedProperty passive =
                passives.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"패시브 {index + 1}",
                        EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("삭제", GUILayout.Width(48f)))
                        removeIndex = index;
                }

                DrawRelativeProperty(passive, "passiveId", "패시브 ID");
                PS260714LocalizationKeyField.Draw(
                    passive.FindPropertyRelative("nameLocalizationKey"),
                    "이름 Localization 키");
                DrawRelativeProperty(
                    passive,
                    "fallbackName",
                    "이름 fallback");
                PS260714LocalizationKeyField.Draw(
                    passive.FindPropertyRelative(
                        "descriptionLocalizationKey"),
                    "설명 Localization 키");
                DrawRelativeProperty(
                    passive,
                    "fallbackDescription",
                    "설명 fallback");
                DrawRelativeProperty(passive, "iconSprite", "패시브 아이콘");
                CharacterEditorWindow.DrawEmbeddedPassiveDefinition(
                    passive.FindPropertyRelative("ability"),
                    passives.serializedObject.targetObject);
            }
        }

        if (removeIndex >= 0)
            passives.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button($"{ownerLabel} 패시브 추가"))
        {
            int newIndex = passives.arraySize;
            passives.arraySize++;
            SerializedProperty added =
                passives.GetArrayElementAtIndex(newIndex);
            string passiveId = CreateUniqueRolePassiveId(
                passives,
                newIndex);
            SerializedProperty passiveIdProperty =
                added.FindPropertyRelative("passiveId");
            if (passiveIdProperty != null)
                passiveIdProperty.stringValue = passiveId;
            ClearRelativeString(added, "nameLocalizationKey");
            ClearRelativeString(added, "descriptionLocalizationKey");
            ClearRelativeString(added, "fallbackName");
            ClearRelativeString(added, "fallbackDescription");
            SerializedProperty icon =
                added.FindPropertyRelative("iconSprite");
            if (icon != null)
                icon.objectReferenceValue = null;
            CharacterEditorWindow.InitializeEmbeddedPassiveDefinition(
                added.FindPropertyRelative("ability"),
                passiveId);
        }
    }

    private static string CreateUniqueRolePassiveId(
        SerializedProperty passives,
        int excludedIndex)
    {
        HashSet<string> usedIds = new(StringComparer.Ordinal);
        for (int index = 0; index < passives.arraySize; index++)
        {
            if (index == excludedIndex)
                continue;

            SerializedProperty passive =
                passives.GetArrayElementAtIndex(index);
            string passiveId = passive.FindPropertyRelative("passiveId")
                ?.stringValue;
            if (!string.IsNullOrWhiteSpace(passiveId))
                usedIds.Add(passiveId.Trim());

            string actionId = passive.FindPropertyRelative("ability")
                ?.FindPropertyRelative("actionId")?.stringValue;
            if (!string.IsNullOrWhiteSpace(actionId))
                usedIds.Add(actionId.Trim());
        }

        int suffix = 1;
        string candidate;
        do
        {
            candidate = $"passive_{suffix++}";
        }
        while (usedIds.Contains(candidate));

        return candidate;
    }

    private static void ClearRelativeString(
        SerializedProperty parent,
        string propertyName)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.stringValue = string.Empty;
    }

    private static void DrawRelativeProperty(
        SerializedProperty parent,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label),
                true);
    }

    private static void DrawProperty(
        SerializedObject serialized,
        string propertyName,
        string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label),
                true);
    }

    private static void DrawReadOnlyProperty(
        SerializedObject serialized,
        string propertyName,
        string label)
    {
        using (new EditorGUI.DisabledScope(true))
            DrawProperty(serialized, propertyName, label);
    }

    private static void DrawLocalizationPreview(
        string label,
        string localizationKey,
        string resolvedText)
    {
        bool localized =
            LocalizationService.TryGet(localizationKey, out _);
        EditorGUILayout.HelpBox(
            $"{label}: {resolvedText}\n" +
            (localized
                ? $"Localization: {localizationKey}"
                : "Localization 키를 찾지 못해 fallback을 표시합니다."),
            localized ? MessageType.Info : MessageType.Warning);
    }

    private static void DrawOptionalLocalizationPreview(
        string label,
        string localizationKey,
        string resolvedText)
    {
        if (string.IsNullOrWhiteSpace(localizationKey) &&
            string.IsNullOrWhiteSpace(resolvedText))
        {
            return;
        }
        DrawLocalizationPreview(label, localizationKey, resolvedText);
    }
}

public sealed class CommonSettingsProjectProvider : SettingsProvider
{
    public const string SettingsPath =
        "Project/PS260714/Common Settings";
    private const string RoleRenameControlName =
        "CommonSettingsRoleRenameField";
    private static UnityEngine.Object _pendingRoleSelection;

    private CharacterGradePaletteSO _palette;
    private CharacterRoleCatalogSO _roleCatalog;
    private UnityEngine.Object _selectedRoleDefinition;
    private Vector2 _scroll;
    private Vector2 _roleListScroll;
    private string _roleSearchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenamingRoleDefinition;
    private bool _focusRenameField;
    private bool _rolesExpanded = true;
    private bool _archetypesExpanded = true;

    private CommonSettingsProjectProvider(
        string path,
        SettingsScope scope,
        IEnumerable<string> keywords)
        : base(path, scope, keywords)
    {
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new CommonSettingsProjectProvider(
            SettingsPath,
            SettingsScope.Project,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CommonDef",
                "공통 설정",
                "빌드",
                "등급",
                "직군",
                "세부 직군",
                "패시브",
            });
    }

    [MenuItem(PS260714EditorMenu.CommonSettings)]
    public static void Open()
    {
        SettingsService.OpenProjectSettings(SettingsPath);
    }

    public static void Open(UnityEngine.Object roleDefinition)
    {
        _pendingRoleSelection = roleDefinition;
        Open();
    }

    public override void OnActivate(
        string searchContext,
        UnityEngine.UIElements.VisualElement rootElement)
    {
        ReloadAssets();
        ApplyPendingRoleSelection();
    }

    public override void OnGUI(string searchContext)
    {
        ApplyPendingRoleSelection();
        if (_palette == null)
        {
            _palette =
                CommonSettingsEditorUtility.LoadGradePalette();
        }
        if (_roleCatalog == null)
        {
            _roleCatalog =
                CommonSettingsEditorUtility.LoadRoleCatalog();
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField(
            "PS260714 공통 설정",
            EditorStyles.largeLabel);
        EditorGUILayout.HelpBox(
            "이 페이지에서 공통 프레젠테이션과 직군 데이터를 " +
            "편집합니다. GameManager는 설정을 소유하지 않습니다.",
            MessageType.Info);

        DrawBuildSettings();
        EditorGUILayout.Space(16f);
        DrawGradeSettings();
        EditorGUILayout.Space(16f);
        DrawRoleSettings();
        EditorGUILayout.EndScrollView();
    }

    private void ApplyPendingRoleSelection()
    {
        if (_pendingRoleSelection is not CharacterRoleSO &&
            _pendingRoleSelection is not CharacterArchetypeSO)
        {
            return;
        }

        _selectedRoleDefinition = _pendingRoleSelection;
        _pendingRoleSelection = null;
        CancelRenameSelectedRoleDefinition();
    }

    private static void DrawBuildSettings()
    {
        EditorGUILayout.LabelField(
            "빌드 설정",
            EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle(
                new GUIContent(
                    "새 빌드 로컬 데이터 초기화",
                    "CommonDef.ResetLocalDataOnNewBuild const 값입니다."),
                CommonDef.ResetLocalDataOnNewBuild);
        }

        EditorGUILayout.HelpBox(
            CommonDef.ResetLocalDataOnNewBuild
                ? "테스트 빌드 설정입니다. 비개발 빌드를 만들기 전에 " +
                  "CommonDef의 값을 false로 변경해야 합니다."
                : "배포용 로컬 데이터 유지 설정입니다.",
            CommonDef.ResetLocalDataOnNewBuild
                ? MessageType.Warning
                : MessageType.Info);
        if (GUILayout.Button("CommonDef.cs 열기"))
            CommonSettingsEditorUtility.OpenCommonDefSource();
    }

    private void DrawGradeSettings()
    {
        EditorGUILayout.LabelField(
            "캐릭터 등급 프레젠테이션",
            EditorStyles.boldLabel);
        if (_palette == null)
        {
            EditorGUILayout.HelpBox(
                "공통 캐릭터 등급 팔레트가 없습니다.",
                MessageType.Error);
            if (GUILayout.Button("기본 등급 팔레트 생성"))
            {
                _palette =
                    CommonSettingsEditorUtility.CreateGradePalette();
            }
            return;
        }

        DrawAssetHeader("팔레트", _palette);
        CommonSettingsEditorGUI.DrawGradePalette(
            _palette,
            true);
    }

    private void DrawRoleSettings()
    {
        EditorGUILayout.LabelField(
            "직군 / 세부 직군 / 직군 패시브",
            EditorStyles.boldLabel);
        if (_roleCatalog == null)
        {
            EditorGUILayout.HelpBox(
                "공통 직군 카탈로그가 없습니다.",
                MessageType.Error);
            if (GUILayout.Button("기본 직군 카탈로그 생성"))
            {
                _roleCatalog =
                    CommonSettingsEditorUtility.CreateRoleCatalog();
            }
            return;
        }

        DrawAssetHeader("카탈로그", _roleCatalog);
        EnsureRoleSelection();
        DrawRoleToolbar();
        if (_isRenamingRoleDefinition)
            DrawRoleRenameField();
        CommonSettingsEditorGUI.DrawRoleCatalog(
            _roleCatalog,
            false);
        DrawRoleWorkspace();
    }

    private void DrawRoleToolbar()
    {
        int definitionCount = 0;
        foreach (CharacterRoleSO role in _roleCatalog.Roles)
        {
            if (role != null)
                definitionCount++;
        }
        foreach (CharacterArchetypeSO archetype in
                 _roleCatalog.Archetypes)
        {
            if (archetype != null)
                definitionCount++;
        }

        PS260714AssetEditorToolbar.Draw(
            $"Role Assets: {definitionCount}",
            _selectedRoleDefinition != null,
            ShowCreateRoleMenu,
            SaveSelectedRoleDefinition,
            DuplicateSelectedRoleDefinition,
            BeginRenameSelectedRoleDefinition,
            DeleteSelectedRoleDefinition,
            () => PS260714AssetEditorList.Ping(
                _selectedRoleDefinition),
            () =>
            {
                PS260714LocalizationKeyField.Refresh();
                ReloadAssets();
                EnsureRoleSelection();
            });
    }

    private void ShowCreateRoleMenu()
    {
        GenericMenu menu = new();
        menu.AddItem(
            new GUIContent("직군"),
            false,
            () =>
            {
                _selectedRoleDefinition =
                    CommonSettingsEditorUtility.CreateRole(
                        _roleCatalog);
                CancelRenameSelectedRoleDefinition();
            });
        menu.AddItem(
            new GUIContent("세부 직군"),
            false,
            () =>
            {
                _selectedRoleDefinition =
                    CommonSettingsEditorUtility.CreateArchetype(
                        _roleCatalog);
                CancelRenameSelectedRoleDefinition();
            });
        menu.ShowAsContext();
    }

    private void SaveSelectedRoleDefinition()
    {
        if (_selectedRoleDefinition == null)
            return;

        EditorUtility.SetDirty(_selectedRoleDefinition);
        AssetDatabase.SaveAssetIfDirty(_selectedRoleDefinition);
        EditorUtility.SetDirty(_roleCatalog);
        AssetDatabase.SaveAssetIfDirty(_roleCatalog);
        CharacterRolePresentation.Invalidate();
    }

    private void DuplicateSelectedRoleDefinition()
    {
        UnityEngine.Object source = _selectedRoleDefinition;
        if (source == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        string directory = Path.GetDirectoryName(sourcePath)
            ?.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{fileName} Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            EditorUtility.DisplayDialog(
                "직군 에셋 복제",
                "에셋을 복제하지 못했습니다.",
                "확인");
            return;
        }

        AssetDatabase.ImportAsset(destinationPath);
        UnityEngine.Object duplicate = null;
        if (source is CharacterRoleSO)
        {
            CharacterRoleSO role =
                AssetDatabase.LoadAssetAtPath<CharacterRoleSO>(
                    destinationPath);
            role?.RegenerateRoleId();
            duplicate = role;
            if (role != null)
            {
                CommonSettingsEditorUtility.AppendAssetReference(
                    _roleCatalog,
                    "roles",
                    role);
            }
        }
        else if (source is CharacterArchetypeSO)
        {
            CharacterArchetypeSO archetype =
                AssetDatabase.LoadAssetAtPath<CharacterArchetypeSO>(
                    destinationPath);
            archetype?.RegenerateArchetypeId();
            duplicate = archetype;
            if (archetype != null)
            {
                CommonSettingsEditorUtility.AppendAssetReference(
                    _roleCatalog,
                    "archetypes",
                    archetype);
            }
        }

        if (duplicate == null)
        {
            EditorUtility.DisplayDialog(
                "직군 에셋 복제",
                "복제한 에셋을 불러오지 못했습니다.",
                "확인");
            return;
        }

        EditorUtility.SetDirty(duplicate);
        AssetDatabase.SaveAssetIfDirty(duplicate);
        AssetDatabase.SaveAssetIfDirty(_roleCatalog);
        CharacterRolePresentation.Invalidate();
        _selectedRoleDefinition = duplicate;
        CancelRenameSelectedRoleDefinition();
        EditorGUIUtility.PingObject(duplicate);
    }

    private void BeginRenameSelectedRoleDefinition()
    {
        if (_selectedRoleDefinition == null)
            return;
        string path = AssetDatabase.GetAssetPath(
            _selectedRoleDefinition);
        if (string.IsNullOrWhiteSpace(path))
            return;

        _renameAssetName = Path.GetFileNameWithoutExtension(path);
        _isRenamingRoleDefinition = true;
        _focusRenameField = true;
    }

    private void DrawRoleRenameField()
    {
        using (new EditorGUILayout.HorizontalScope(
                   EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "SO 파일명",
                GUILayout.Width(70f));
            GUI.SetNextControlName(RoleRenameControlName);
            _renameAssetName =
                EditorGUILayout.TextField(_renameAssetName);
            if (_focusRenameField)
            {
                EditorGUI.FocusTextInControl(RoleRenameControlName);
                _focusRenameField = false;
            }

            bool apply = GUILayout.Button("적용", GUILayout.Width(52f));
            bool cancel = GUILayout.Button("취소", GUILayout.Width(52f));
            Event current = Event.current;
            if (current.type == EventType.KeyDown &&
                GUI.GetNameOfFocusedControl() == RoleRenameControlName)
            {
                if (current.keyCode == KeyCode.Return ||
                    current.keyCode == KeyCode.KeypadEnter)
                {
                    apply = true;
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    cancel = true;
                    current.Use();
                }
            }

            if (cancel)
                CancelRenameSelectedRoleDefinition();
            else if (apply)
                RenameSelectedRoleDefinition();
        }
    }

    private void RenameSelectedRoleDefinition()
    {
        UnityEngine.Object selected = _selectedRoleDefinition;
        if (selected == null)
        {
            CancelRenameSelectedRoleDefinition();
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(selected);
        string requestedName = (_renameAssetName ?? string.Empty).Trim();
        if (requestedName.EndsWith(
                ".asset",
                StringComparison.OrdinalIgnoreCase))
        {
            requestedName = requestedName.Substring(
                0,
                requestedName.Length - ".asset".Length).Trim();
        }

        if (!IsValidAssetFileName(
                requestedName,
                out string validationError))
        {
            EditorUtility.DisplayDialog(
                "직군 에셋 이름 변경",
                validationError,
                "확인");
            _focusRenameField = true;
            return;
        }

        string currentName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.Equals(
                currentName,
                requestedName,
                StringComparison.Ordinal))
        {
            CancelRenameSelectedRoleDefinition();
            return;
        }

        string error = AssetDatabase.RenameAsset(
            sourcePath,
            requestedName);
        if (!string.IsNullOrWhiteSpace(error))
        {
            EditorUtility.DisplayDialog(
                "직군 에셋 이름 변경",
                error,
                "확인");
            _focusRenameField = true;
            return;
        }

        AssetDatabase.SaveAssets();
        CancelRenameSelectedRoleDefinition();
        EditorGUIUtility.PingObject(selected);
    }

    private void CancelRenameSelectedRoleDefinition()
    {
        _isRenamingRoleDefinition = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
    }

    private static bool IsValidAssetFileName(
        string fileName,
        out string validationError)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            validationError = "파일명을 입력하세요.";
            return false;
        }
        if (fileName == "." || fileName == ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.IndexOf('/') >= 0 ||
            fileName.IndexOf('\\') >= 0 ||
            fileName.EndsWith(".", StringComparison.Ordinal) ||
            fileName.EndsWith(" ", StringComparison.Ordinal))
        {
            validationError = "파일명에 사용할 수 없는 문자가 있습니다.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private void DeleteSelectedRoleDefinition()
    {
        UnityEngine.Object selected = _selectedRoleDefinition;
        if (selected == null)
            return;

        List<string> references = FindDefinitionReferences(selected);
        if (references.Count > 0)
        {
            int visibleCount = Math.Min(references.Count, 8);
            string message =
                "다음 에셋이 선택한 정의를 참조하므로 삭제할 수 " +
                "없습니다.\n\n";
            for (int index = 0; index < visibleCount; index++)
                message += $"• {references[index]}\n";
            if (references.Count > visibleCount)
            {
                message +=
                    $"• 그 외 {references.Count - visibleCount}개";
            }
            EditorUtility.DisplayDialog(
                "직군 에셋 삭제 차단",
                message,
                "확인");
            return;
        }

        bool wasRole = selected is CharacterRoleSO;
        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                selected,
                "Role Definition",
                false))
            return;

        CommonSettingsEditorUtility.RemoveMissingAssetReferences(
            _roleCatalog,
            wasRole ? "roles" : "archetypes");
        AssetDatabase.SaveAssetIfDirty(_roleCatalog);
        CharacterRolePresentation.Invalidate();
        _selectedRoleDefinition = null;
        CancelRenameSelectedRoleDefinition();
        ReloadAssets();
        EnsureRoleSelection();
    }

    private static List<string> FindDefinitionReferences(
        UnityEngine.Object definition)
    {
        List<string> references = new();
        HashSet<string> uniquePaths =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in AssetDatabase.FindAssets("t:CharacterSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSO character =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(path);
            bool usesDefinition = definition switch
            {
                CharacterRoleSO role => character != null &&
                                        character.Role == role,
                CharacterArchetypeSO archetype => character != null &&
                    character.Archetype == archetype,
                _ => false
            };
            if (usesDefinition && uniquePaths.Add(path))
                references.Add($"CharacterSO: {path}");
        }

        if (definition is CharacterRoleSO selectedRole)
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:CharacterArchetypeSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterArchetypeSO archetype =
                    AssetDatabase.LoadAssetAtPath<CharacterArchetypeSO>(
                        path);
                if (archetype != null &&
                    archetype.ParentRole == selectedRole &&
                    uniquePaths.Add(path))
                {
                    references.Add($"세부 직군: {path}");
                }
            }
        }

        references.Sort(StringComparer.OrdinalIgnoreCase);
        return references;
    }

    private static void DrawAssetHeader(
        string label,
        UnityEngine.Object asset)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    label,
                    asset,
                    asset.GetType(),
                    false);
            }
            if (GUILayout.Button("선택", GUILayout.Width(56f)))
                CommonSettingsEditorUtility.SelectAsset(asset);
        }
    }

    private void EnsureRoleSelection()
    {
        if (_selectedRoleDefinition != null &&
            !ContainsDefinition(
                _roleCatalog,
                _selectedRoleDefinition))
        {
            _selectedRoleDefinition = null;
        }
        if (_selectedRoleDefinition != null)
            return;
        foreach (CharacterRoleSO role in _roleCatalog.Roles)
        {
            if (role == null)
                continue;
            _selectedRoleDefinition = role;
            return;
        }
        foreach (CharacterArchetypeSO archetype in
                 _roleCatalog.Archetypes)
        {
            if (archetype == null)
                continue;
            _selectedRoleDefinition = archetype;
            return;
        }
    }

    private void DrawRoleWorkspace()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(PS260714AssetEditorList.Width)))
            {
                _roleSearchText =
                    PS260714AssetEditorList.DrawSearchField(
                        _roleSearchText);
                int visibleCount = 0;
                using (EditorGUILayout.ScrollViewScope listScroll = new(
                           _roleListScroll,
                           GUILayout.MinHeight(240f),
                           GUILayout.MaxHeight(520f)))
                {
                    _roleListScroll = listScroll.scrollPosition;
                    _rolesExpanded = EditorGUILayout.Foldout(
                        _rolesExpanded,
                        "직군",
                        true,
                        EditorStyles.foldoutHeader);
                    if (_rolesExpanded)
                    {
                        foreach (CharacterRoleSO role in _roleCatalog.Roles)
                        {
                            if (role != null && !MatchesRoleSearch(role))
                                continue;
                            visibleCount++;
                            DrawSelectionButton(
                                role,
                                role != null
                                    ? role.GetDisplayName()
                                    : "(비어 있는 직군 참조)");
                        }
                    }

                    EditorGUILayout.Space(6f);
                    _archetypesExpanded = EditorGUILayout.Foldout(
                        _archetypesExpanded,
                        "세부 직군",
                        true,
                        EditorStyles.foldoutHeader);
                    if (_archetypesExpanded)
                    {
                        foreach (CharacterArchetypeSO archetype in
                                 _roleCatalog.Archetypes)
                        {
                            if (archetype != null &&
                                !MatchesRoleSearch(archetype))
                            {
                                continue;
                            }
                            visibleCount++;
                            DrawSelectionButton(
                                archetype,
                                archetype != null
                                    ? archetype.GetDisplayName()
                                    : "(비어 있는 세부 직군 참조)");
                        }
                    }
                }
                PS260714AssetEditorList.DrawCountFooter(
                    visibleCount,
                    _roleCatalog.Roles.Count +
                    _roleCatalog.Archetypes.Count);
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinWidth(340f)))
            {
                if (_selectedRoleDefinition is CharacterRoleSO role)
                {
                    CommonSettingsEditorGUI.DrawRoleDefinition(role);
                }
                else if (_selectedRoleDefinition is
                         CharacterArchetypeSO archetype)
                {
                    CommonSettingsEditorGUI.DrawArchetypeDefinition(
                        archetype);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "왼쪽 목록에서 편집할 직군 또는 세부 직군을 " +
                        "선택하세요.",
                        MessageType.Info);
                }
            }
        }
    }

    private void DrawSelectionButton(
        UnityEngine.Object asset,
        string label)
    {
        using (new EditorGUI.DisabledScope(asset == null))
        {
            bool selected = asset != null &&
                            _selectedRoleDefinition == asset;
            UnityEngine.Object preview = asset switch
            {
                CharacterRoleSO role => role.IconSprite,
                CharacterArchetypeSO archetype => archetype.IconSprite,
                _ => null
            };
            string stableId = asset switch
            {
                CharacterRoleSO role => role.RoleId,
                CharacterArchetypeSO archetype => archetype.ArchetypeId,
                _ => string.Empty
            };
            bool clicked = PS260714AssetEditorList.DrawAssetRow(
                selected,
                asset,
                preview,
                label,
                stableId,
                AssetDatabase.GetAssetPath(asset));
            if (clicked && !selected)
            {
                _selectedRoleDefinition = asset;
                CancelRenameSelectedRoleDefinition();
            }
        }
    }

    private bool MatchesRoleSearch(UnityEngine.Object definition)
    {
        if (string.IsNullOrWhiteSpace(_roleSearchText))
            return true;

        string search = _roleSearchText.Trim();
        string displayName;
        string localizationKey;
        string stableId;
        if (definition is CharacterRoleSO role)
        {
            displayName = role.GetDisplayName();
            localizationKey = role.NameLocalizationKey;
            stableId = role.RoleId;
        }
        else if (definition is CharacterArchetypeSO archetype)
        {
            displayName = archetype.GetDisplayName();
            localizationKey = archetype.NameLocalizationKey;
            stableId = archetype.ArchetypeId;
        }
        else
        {
            return false;
        }

        return ContainsIgnoreCase(definition.name, search) ||
               ContainsIgnoreCase(displayName, search) ||
               ContainsIgnoreCase(localizationKey, search) ||
               ContainsIgnoreCase(stableId, search);
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        return (value ?? string.Empty).IndexOf(
            search,
            StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ReloadAssets()
    {
        _palette =
            CommonSettingsEditorUtility.LoadGradePalette();
        _roleCatalog =
            CommonSettingsEditorUtility.LoadRoleCatalog();
        PS260714LocalizationKeyField.Refresh();
        if (_selectedRoleDefinition != null &&
            (_roleCatalog == null ||
             !ContainsDefinition(
                 _roleCatalog,
                 _selectedRoleDefinition)))
        {
            _selectedRoleDefinition = null;
        }
    }

    private static bool ContainsDefinition(
        CharacterRoleCatalogSO catalog,
        UnityEngine.Object definition)
    {
        foreach (CharacterRoleSO role in catalog.Roles)
        {
            if (role == definition)
                return true;
        }
        foreach (CharacterArchetypeSO archetype in catalog.Archetypes)
        {
            if (archetype == definition)
                return true;
        }
        return false;
    }
}

internal static class CommonSettingsEditorUtility
{
    private const string CommonDefAssetPath =
        "Assets/Scripts/Utils/CommonDef.cs";
    private const string GradePaletteAssetPath =
        "Assets/Resources/" +
        CommonDef.CharacterGradePaletteResourcePath +
        ".asset";
    private const string RoleCatalogAssetPath =
        "Assets/Resources/" +
        CommonDef.CharacterRoleCatalogResourcePath +
        ".asset";
    private const string RoleAssetFolder =
        "Assets/Resources/Presentation/Roles";
    private const string ArchetypeAssetFolder =
        "Assets/Resources/Presentation/Archetypes";

    public static CharacterGradePaletteSO LoadGradePalette()
    {
        return AssetDatabase.LoadAssetAtPath<CharacterGradePaletteSO>(
            GradePaletteAssetPath);
    }

    public static CharacterGradePaletteSO CreateGradePalette()
    {
        CharacterGradePaletteSO existing = LoadGradePalette();
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Resources/Presentation");
        CharacterGradePaletteSO palette =
            ScriptableObject.CreateInstance<CharacterGradePaletteSO>();
        AssetDatabase.CreateAsset(palette, GradePaletteAssetPath);
        AssetDatabase.SaveAssets();
        CharacterGradePresentation.Invalidate();
        SelectAsset(palette);
        return palette;
    }

    public static CharacterRoleCatalogSO LoadRoleCatalog()
    {
        return AssetDatabase.LoadAssetAtPath<CharacterRoleCatalogSO>(
            RoleCatalogAssetPath);
    }

    public static CharacterRoleCatalogSO CreateRoleCatalog()
    {
        CharacterRoleCatalogSO existing = LoadRoleCatalog();
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Resources/Presentation");
        CharacterRoleCatalogSO catalog =
            ScriptableObject.CreateInstance<CharacterRoleCatalogSO>();
        AssetDatabase.CreateAsset(catalog, RoleCatalogAssetPath);
        AssetDatabase.SaveAssets();
        CharacterRolePresentation.Invalidate();
        SelectAsset(catalog);
        return catalog;
    }

    public static void OpenCommonDefSource()
    {
        MonoScript script =
            AssetDatabase.LoadAssetAtPath<MonoScript>(CommonDefAssetPath);
        if (script != null)
            AssetDatabase.OpenAsset(script);
    }

    public static void SelectAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    public static void DrawGradePalettePreview(
        CharacterGradePaletteSO palette)
    {
        if (palette == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "등급 미리보기",
            EditorStyles.miniBoldLabel);
        for (int value = (int)CharacterGrade.Grade0;
             value <= (int)CharacterGrade.Grade3;
             value++)
        {
            CharacterGrade grade = (CharacterGrade)value;
            CharacterGradeStyle style = palette.GetStyle(grade);
            DrawGradePreview(grade, style);
        }
    }

    public static void DrawGradePaletteEditor(
        CharacterGradePaletteSO palette)
    {
        if (palette == null)
            return;

        EditorGUILayout.HelpBox(
            "이름 옆 등급 아이콘의 표시 색상은 흰색으로 고정됩니다. " +
            "아래 색상은 카드 배경·강조·테두리·텍스트에만 적용됩니다.",
            MessageType.Info);
        SerializedObject serialized = new(palette);
        serialized.UpdateIfRequiredOrScript();
        DrawProperty(serialized, "grade0", "0등급");
        DrawProperty(serialized, "grade1", "1등급");
        DrawProperty(serialized, "grade2", "2등급");
        DrawProperty(serialized, "grade3", "3등급");
        if (serialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(palette);
            CharacterGradePresentation.Invalidate();
        }
    }

    public static void DrawRoleCatalogEditor(
        CharacterRoleCatalogSO catalog,
        bool drawCreateButtons = true)
    {
        if (catalog == null)
            return;

        SerializedObject serialized = new(catalog);
        serialized.UpdateIfRequiredOrScript();
        DrawProperty(serialized, "roles", "직군 목록");
        DrawProperty(serialized, "archetypes", "세부 직군 목록");
        if (serialized.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(catalog);
            CharacterRolePresentation.Invalidate();
        }

        IReadOnlyList<string> issues = catalog.GetValidationIssues();
        foreach (string issue in issues)
            EditorGUILayout.HelpBox(issue, MessageType.Warning);

        if (drawCreateButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("직군 생성"))
                    CreateRole(catalog);
                if (GUILayout.Button("세부 직군 생성"))
                    CreateArchetype(catalog);
            }
        }
    }

    public static CharacterRoleSO CreateRole(
        CharacterRoleCatalogSO catalog)
    {
        EnsureFolder(RoleAssetFolder);
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        string path = AssetDatabase.GenerateUniqueAssetPath(
            RoleAssetFolder + "/CharacterRole.asset");
        AssetDatabase.CreateAsset(role, path);
        AppendAssetReference(catalog, "roles", role);
        AssetDatabase.SaveAssets();
        CharacterRolePresentation.Invalidate();
        SelectAsset(role);
        return role;
    }

    public static CharacterArchetypeSO CreateArchetype(
        CharacterRoleCatalogSO catalog)
    {
        EnsureFolder(ArchetypeAssetFolder);
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        SerializedObject archetypeSerialized = new(archetype);
        SerializedProperty parent =
            archetypeSerialized.FindProperty("parentRole");
        foreach (CharacterRoleSO role in catalog.Roles)
        {
            if (role == null)
                continue;
            parent.objectReferenceValue = role;
            break;
        }
        archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            ArchetypeAssetFolder + "/CharacterArchetype.asset");
        AssetDatabase.CreateAsset(archetype, path);
        AppendAssetReference(catalog, "archetypes", archetype);
        AssetDatabase.SaveAssets();
        CharacterRolePresentation.Invalidate();
        SelectAsset(archetype);
        return archetype;
    }

    public static void AppendAssetReference(
        CharacterRoleCatalogSO catalog,
        string propertyName,
        UnityEngine.Object asset)
    {
        SerializedObject serialized = new(catalog);
        SerializedProperty list = serialized.FindProperty(propertyName);
        for (int index = 0; index < list.arraySize; index++)
        {
            if (list.GetArrayElementAtIndex(index).objectReferenceValue ==
                asset)
            {
                return;
            }
        }
        int appendIndex = list.arraySize;
        list.InsertArrayElementAtIndex(appendIndex);
        list.GetArrayElementAtIndex(appendIndex).objectReferenceValue = asset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    public static void RemoveMissingAssetReferences(
        CharacterRoleCatalogSO catalog,
        string propertyName)
    {
        if (catalog == null)
            return;

        SerializedObject serialized = new(catalog);
        SerializedProperty list = serialized.FindProperty(propertyName);
        if (list == null || !list.isArray)
            return;

        for (int index = list.arraySize - 1; index >= 0; index--)
        {
            SerializedProperty element =
                list.GetArrayElementAtIndex(index);
            if (element.objectReferenceValue != null)
                continue;

            int previousSize = list.arraySize;
            list.DeleteArrayElementAtIndex(index);
            if (list.arraySize == previousSize)
                list.DeleteArrayElementAtIndex(index);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void DrawProperty(
        SerializedObject serialized,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label),
                true);
    }

    private static void DrawGradePreview(
        CharacterGrade grade,
        CharacterGradeStyle style)
    {
        Rect row = EditorGUILayout.GetControlRect(false, 34f);
        EditorGUI.DrawRect(row, style.BackgroundColor);
        EditorGUI.DrawRect(
            new Rect(row.x, row.y, 8f, row.height),
            style.PrimaryColor);
        Handles.DrawSolidRectangleWithOutline(
            row,
            Color.clear,
            style.OutlineColor);

        Rect iconRect = new(
            row.x + 14f,
            row.y + 3f,
            28f,
            28f);
        if (style.GradeIcon != null)
        {
            Texture icon =
                AssetPreview.GetAssetPreview(style.GradeIcon) ??
                AssetPreview.GetMiniThumbnail(style.GradeIcon);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        }

        GUIStyle labelStyle = new(EditorStyles.boldLabel);
        labelStyle.normal.textColor = style.TextColor;
        string iconState = style.GradeIcon != null
            ? style.GradeIcon.name
            : "아이콘 미지정";
        EditorGUI.LabelField(
            new Rect(
                row.x + 48f,
                row.y,
                row.width - 54f,
                row.height),
            $"{CharacterGradePresentation.GetLabel(grade)} · {iconState}",
            labelStyle);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separatorIndex = path.LastIndexOf('/');
        string parent = path.Substring(0, separatorIndex);
        string folderName = path.Substring(separatorIndex + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}

public sealed class CommonBuildConfigurationGuard :
    IPreprocessBuildWithReport
{
    public int callbackOrder => -11000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsLocalDataResetEnabled())
            return;

        bool developmentBuild =
            (report.summary.options & BuildOptions.Development) != 0;
        if (!developmentBuild)
        {
            throw new BuildFailedException(
                "CommonDef.ResetLocalDataOnNewBuild is enabled. " +
                "Disable it before creating a non-development build.");
        }

        if ((report.summary.options & BuildOptions.NoUniqueIdentifier) != 0)
        {
            throw new BuildFailedException(
                "Automatic local-data reset requires a unique build GUID. " +
                "Remove BuildOptions.NoUniqueIdentifier.");
        }
    }

    private static bool IsLocalDataResetEnabled()
    {
        return CommonDef.ResetLocalDataOnNewBuild;
    }
}
