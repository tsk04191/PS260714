using System;
using System.Collections.Generic;
using UnityEditor;
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

        serializedObject.ApplyModifiedProperties();
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
