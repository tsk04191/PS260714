using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MenuPageSceneBuilder
{
    private const string ClientScenePath = "Assets/Scenes/ClientScene.unity";

    static MenuPageSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
        EditorApplication.update += BuildWhenSceneIsReady;
    }

    [MenuItem("Tools/PS260714/Rebuild Main Menu Pages")]
    public static void RebuildClientPages()
    {
        BuildClientPages(true);
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        BuildClientPages(false);
    }

    private static void BuildWhenSceneIsReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            return;
        }

        EditorApplication.update -= BuildWhenSceneIsReady;
        BuildClientPages(false);
    }

    private static void BuildClientPages(bool forceRebuild)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            return;
        }

        GameObject layClient = FindSceneObject(scene, "layClient");
        if (layClient == null)
            return;

        GameObject titleObject = FindDirectChild(layClient, "pagTitle");
        GameObject mainObject = FindDirectChild(layClient, "pagMain");
        GameObject dungeonObject = FindDirectChild(layClient, "pagDungeon");
        GameObject settingObject = FindDirectChild(layClient, "pagSetting");
        if (titleObject == null || mainObject == null ||
            dungeonObject == null || settingObject == null)
        {
            return;
        }

        GameObject codexObject = FindDirectChild(layClient, "pagCodex");
        GameObject rosterObject = FindDirectChild(layClient, "pagRoster");
        GameObject shopObject = FindDirectChild(layClient, "pagShop");
        GameObject questObject = FindDirectChild(layClient, "pagQuest");
        GameObject storageObject = FindDirectChild(layClient, "pagStorage");
        GameObject enemyCodexObject =
            FindDirectChild(layClient, "pagEnemyCodex");
        GameObject characterCodexObject =
            FindDirectChild(layClient, "pagCharacterCodex");
        GameObject skillCodexObject =
            FindDirectChild(layClient, "pagSkillCodex");
        GameObject itemCodexObject =
            FindDirectChild(layClient, "pagItemCodex");
        TitlePage titlePage = titleObject.GetComponent<TitlePage>();
        MainPage mainPage = mainObject.GetComponent<MainPage>();
        DungeonPage dungeonPage = dungeonObject.GetComponent<DungeonPage>();
        MainSubPage codexPage = codexObject != null
            ? codexObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage rosterPage = rosterObject != null
            ? rosterObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage shopPage = shopObject != null
            ? shopObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage questPage = questObject != null
            ? questObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage storagePage = storageObject != null
            ? storageObject.GetComponent<MainSubPage>()
            : null;
        EnemyCodexPage enemyCodexPage = enemyCodexObject != null
            ? enemyCodexObject.GetComponent<EnemyCodexPage>()
            : null;
        CharacterCodexPage characterCodexPage =
            characterCodexObject != null
                ? characterCodexObject.GetComponent<CharacterCodexPage>()
                : null;
        BattleCardCodexPage skillCodexPage = skillCodexObject != null
            ? skillCodexObject.GetComponent<BattleCardCodexPage>()
            : null;
        BattleCardCodexPage itemCodexPage = itemCodexObject != null
            ? itemCodexObject.GetComponent<BattleCardCodexPage>()
            : null;
        bool titleUiExists = HasGeneratedUi(titleObject);
        bool mainUiExists = HasGeneratedUi(mainObject);
        if (!forceRebuild && titlePage != null && mainPage != null &&
            dungeonPage != null &&
            HasObjectReference(dungeonPage, "mainPage") &&
            HasObjectReference(codexPage, "enemyCodexPage") &&
            HasObjectReference(codexPage, "characterCodexPage") &&
            HasObjectReference(codexPage, "skillCodexPage") &&
            HasObjectReference(codexPage, "itemCodexPage") &&
            enemyCodexPage != null &&
            HasObjectReference(enemyCodexPage, "codexPage") &&
            HasObjectReference(enemyCodexPage, "dungeonPage") &&
            characterCodexPage != null &&
            HasObjectReference(characterCodexPage, "codexPage") &&
            HasObjectReference(characterCodexPage, "dungeonPage") &&
            skillCodexPage != null &&
            HasObjectReference(skillCodexPage, "codexPage") &&
            itemCodexPage != null &&
            HasObjectReference(itemCodexPage, "codexPage") &&
            codexPage != null && rosterPage != null && shopPage != null &&
            questPage != null && storagePage != null &&
            titleUiExists && mainUiExists &&
            HasGeneratedUi(codexObject) && HasGeneratedUi(rosterObject) &&
            HasGeneratedUi(shopObject) && HasGeneratedUi(questObject) &&
            HasGeneratedUi(storageObject) &&
            HasGeneratedUi(enemyCodexObject) &&
            HasGeneratedUi(characterCodexObject) &&
            HasGeneratedUi(skillCodexObject) &&
            HasGeneratedUi(itemCodexObject))
        {
            return;
        }

        const string undoName = "Build Main Menu Pages";
        Undo.SetCurrentGroupName(undoName);
        int undoGroup = Undo.GetCurrentGroup();

        codexObject ??= CreatePageObject(layClient, "pagCodex", undoName);
        rosterObject ??= CreatePageObject(layClient, "pagRoster", undoName);
        shopObject ??= CreatePageObject(layClient, "pagShop", undoName);
        questObject ??= CreatePageObject(layClient, "pagQuest", undoName);
        storageObject ??= CreatePageObject(layClient, "pagStorage", undoName);
        enemyCodexObject ??= CreatePageObject(
            layClient,
            "pagEnemyCodex",
            undoName);
        characterCodexObject ??= CreatePageObject(
            layClient,
            "pagCharacterCodex",
            undoName);
        skillCodexObject ??= CreatePageObject(
            layClient,
            "pagSkillCodex",
            undoName);
        itemCodexObject ??= CreatePageObject(
            layClient,
            "pagItemCodex",
            undoName);

        titlePage ??= Undo.AddComponent<TitlePage>(titleObject);
        mainPage ??= Undo.AddComponent<MainPage>(mainObject);
        dungeonPage ??= Undo.AddComponent<DungeonPage>(dungeonObject);
        codexPage ??= Undo.AddComponent<MainSubPage>(codexObject);
        rosterPage ??= Undo.AddComponent<MainSubPage>(rosterObject);
        shopPage ??= Undo.AddComponent<MainSubPage>(shopObject);
        questPage ??= Undo.AddComponent<MainSubPage>(questObject);
        storagePage ??= Undo.AddComponent<MainSubPage>(storageObject);
        enemyCodexPage ??=
            Undo.AddComponent<EnemyCodexPage>(enemyCodexObject);
        characterCodexPage ??=
            Undo.AddComponent<CharacterCodexPage>(characterCodexObject);
        skillCodexPage ??=
            Undo.AddComponent<BattleCardCodexPage>(skillCodexObject);
        itemCodexPage ??=
            Undo.AddComponent<BattleCardCodexPage>(itemCodexObject);

        ConfigureFullScreenRect(titleObject, undoName);
        ConfigureFullScreenRect(mainObject, undoName);
        ConfigureFullScreenRect(codexObject, undoName);
        ConfigureFullScreenRect(rosterObject, undoName);
        ConfigureFullScreenRect(shopObject, undoName);
        ConfigureFullScreenRect(questObject, undoName);
        ConfigureFullScreenRect(storageObject, undoName);
        ConfigureFullScreenRect(enemyCodexObject, undoName);
        ConfigureFullScreenRect(characterCodexObject, undoName);
        ConfigureFullScreenRect(skillCodexObject, undoName);
        ConfigureFullScreenRect(itemCodexObject, undoName);

        SetObjectReference(titlePage, "mainPage", mainObject);
        SetObjectReference(titlePage, "settingPage", settingObject);
        SetObjectReference(mainPage, "dungeonPage", dungeonObject);
        SetObjectReference(mainPage, "codexPage", codexObject);
        SetObjectReference(mainPage, "rosterPage", rosterObject);
        SetObjectReference(mainPage, "shopPage", shopObject);
        SetObjectReference(mainPage, "questPage", questObject);
        SetObjectReference(mainPage, "storagePage", storageObject);
        SetObjectReference(mainPage, "settingPage", settingObject);
        SetObjectReference(dungeonPage, "mainPage", mainObject);
        SetObjectReference(
            codexPage,
            "enemyCodexPage",
            enemyCodexObject);
        SetObjectReference(
            codexPage,
            "characterCodexPage",
            characterCodexObject);
        SetObjectReference(codexPage, "skillCodexPage", skillCodexObject);
        SetObjectReference(codexPage, "itemCodexPage", itemCodexObject);
        SetObjectReference(enemyCodexPage, "codexPage", codexObject);
        SetObjectReference(enemyCodexPage, "dungeonPage", dungeonObject);
        SetEnemyDefinitions(enemyCodexPage);
        SetObjectReference(characterCodexPage, "codexPage", codexObject);
        SetObjectReference(
            characterCodexPage,
            "dungeonPage",
            dungeonObject);
        SetCharacterDefinitions(characterCodexPage);
        ConfigureBattleCardCodex(
            skillCodexPage,
            codexObject,
            EBattleCardCodexCategory.Skills);
        ConfigureBattleCardCodex(
            itemCodexPage,
            codexObject,
            EBattleCardCodexCategory.Items);

        ConfigureSubPage(
            codexPage,
            EMainSubPageType.Codex,
            mainObject);
        ConfigureSubPage(
            rosterPage,
            EMainSubPageType.Roster,
            mainObject);
        ConfigureSubPage(
            shopPage,
            EMainSubPageType.Shop,
            mainObject);
        ConfigureSubPage(
            questPage,
            EMainSubPageType.Quest,
            mainObject);
        ConfigureSubPage(
            storagePage,
            EMainSubPageType.Storage,
            mainObject);

        titlePage.RebuildEditorPreview();
        mainPage.RebuildEditorPreview();
        codexPage.RebuildEditorPreview();
        rosterPage.RebuildEditorPreview();
        shopPage.RebuildEditorPreview();
        questPage.RebuildEditorPreview();
        storagePage.RebuildEditorPreview();
        enemyCodexPage.RebuildEditorPreview();
        characterCodexPage.RebuildEditorPreview();
        skillCodexPage.RebuildEditorPreview();
        itemCodexPage.RebuildEditorPreview();

        SetActive(titleObject, true, undoName);
        SetActive(mainObject, false, undoName);
        SetActive(dungeonObject, false, undoName);
        SetActive(settingObject, false, undoName);
        SetActive(codexObject, false, undoName);
        SetActive(rosterObject, false, undoName);
        SetActive(shopObject, false, undoName);
        SetActive(questObject, false, undoName);
        SetActive(storageObject, false, undoName);
        SetActive(enemyCodexObject, false, undoName);
        SetActive(characterCodexObject, false, undoName);
        SetActive(skillCodexObject, false, undoName);
        SetActive(itemCodexObject, false, undoName);
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetEnemyDefinitions(EnemyCodexPage page)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets" });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        List<EnemySO> definitions = new();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(assetPath);
            if (definition != null)
                definitions.Add(definition);
        }

        SerializedObject serializedObject = new(page);
        SerializedProperty property =
            serializedObject.FindProperty("enemyDefinitions");
        if (property == null)
            return;

        property.arraySize = definitions.Count;
        for (int index = 0; index < definitions.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static void SetCharacterDefinitions(CharacterCodexPage page)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CharacterSO",
            new[] { "Assets" });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        List<CharacterSO> definitions = new();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSO definition =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(assetPath);
            if (definition != null)
                definitions.Add(definition);
        }

        SerializedObject serializedObject = new(page);
        SerializedProperty property =
            serializedObject.FindProperty("characterDefinitions");
        if (property == null)
            return;

        property.arraySize = definitions.Count;
        for (int index = 0; index < definitions.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static GameObject CreatePageObject(
        GameObject parent,
        string objectName,
        string undoName)
    {
        GameObject pageObject = new(objectName, typeof(RectTransform));
        pageObject.layer = parent.layer;
        pageObject.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(pageObject, undoName);
        return pageObject;
    }

    private static void ConfigureBattleCardCodex(
        BattleCardCodexPage page,
        GameObject codexObject,
        EBattleCardCodexCategory category)
    {
        SerializedObject serializedObject = new(page);
        SerializedProperty codexProperty =
            serializedObject.FindProperty("codexPage");
        SerializedProperty categoryProperty =
            serializedObject.FindProperty("category");
        if (codexProperty != null)
            codexProperty.objectReferenceValue = codexObject;
        if (categoryProperty != null)
            categoryProperty.enumValueIndex = (int)category;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static void ConfigureSubPage(
        MainSubPage page,
        EMainSubPageType pageType,
        GameObject mainObject)
    {
        SerializedObject serializedObject = new(page);
        SerializedProperty typeProperty =
            serializedObject.FindProperty("pageType");
        SerializedProperty mainProperty =
            serializedObject.FindProperty("mainPage");
        if (typeProperty != null)
            typeProperty.enumValueIndex = (int)pageType;
        if (mainProperty != null)
            mainProperty.objectReferenceValue = mainObject;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static bool HasGeneratedUi(GameObject pageObject)
    {
        return pageObject != null && pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName) != null;
    }

    private static bool HasObjectReference(
        Object target,
        string propertyName)
    {
        if (target == null)
            return false;

        SerializedObject serializedObject = new(target);
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private static void ConfigureFullScreenRect(
        GameObject target,
        string undoName)
    {
        if (target == null ||
            target.transform is not RectTransform rectTransform)
        {
            return;
        }

        Undo.RecordObject(rectTransform, undoName);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        EditorUtility.SetDirty(rectTransform);
    }

    private static void SetObjectReference(
        Object target,
        string propertyName,
        GameObject value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetActive(
        GameObject target,
        bool active,
        string undoName)
    {
        if (target == null || target.activeSelf == active)
            return;

        Undo.RecordObject(target, undoName);
        target.SetActive(active);
        EditorUtility.SetDirty(target);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(
                root.GetChild(index),
                objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindDirectChild(
        GameObject parent,
        string objectName)
    {
        if (parent == null)
            return null;

        Transform child = parent.transform.Find(objectName);
        return child != null ? child.gameObject : null;
    }
}
