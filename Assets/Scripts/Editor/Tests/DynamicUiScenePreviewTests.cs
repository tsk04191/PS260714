using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DynamicUiScenePreviewTests
{
    private const string ScenePath = "Assets/Scenes/ClientScene.unity";

    [Test]
    public void AttendanceCalendar_HasTwentyEightAuthoredPrefabCells()
    {
        Scene scene = OpenScene(out bool opened);
        try
        {
            MonthlyAttendancePopupView popup = FindOne<
                MonthlyAttendancePopupView>(scene);
            RectTransform root = new SerializedObject(popup)
                .FindProperty("calendarRoot").objectReferenceValue
                as RectTransform;
            Assert.That(root, Is.Not.Null);

            AttendanceRewardCellView[] cells = DirectComponents<
                AttendanceRewardCellView>(root).ToArray();
            Assert.That(
                cells.Length,
                Is.EqualTo(AttendanceRewardScheduleSO.CycleRewardCount));
            for (int index = 0; index < cells.Length; index++)
            {
                Assert.That(
                    cells[index].name,
                    Is.EqualTo($"grpAttendanceReward{index + 1:00}"));
                AssertPrefabPath(
                    cells[index].gameObject,
                    "Assets/Resources/Presentation/" +
                    "AttendanceRewardCell.prefab");
            }
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ClientScene_DynamicUiContainersHaveRealPrefabPreviews()
    {
        Scene scene = OpenScene(out bool opened);
        try
        {
            foreach (CodexBrowserDesignerSettings settings in
                     FindAll<CodexBrowserDesignerSettings>(scene))
            {
                GameObject preview = settings.CardContent
                    .Find("btnCodexCard_0")?.gameObject;
                AssertPrefabPath(
                    preview,
                    "Assets/Resources/Presentation/CodexCard.prefab");
            }

            OperatorRosterDesignerSettings roster =
                FindOne<OperatorRosterDesignerSettings>(scene);
            AssertPrefabPath(
                roster.transform.Find(
                    "scrRosterList/vptRosterList/grpRosterCardContent/" +
                    "btnOperatorCard_0")?.gameObject,
                "Assets/Resources/Presentation/OperatorRosterCard.prefab");

            OperatorDetailDesignerSettings detail =
                FindOne<OperatorDetailDesignerSettings>(scene);
            AssertPrefabPath(
                detail.transform.Find(
                    "grpOperatorDetailRight/grpOperatorPassives/" +
                    "grpPassiveIconRoot/grpPassiveIcon_0")?.gameObject,
                "Assets/Resources/Presentation/OperatorAbilityIcon.prefab");
            AssertPrefabPath(
                detail.transform.Find(
                    "grpOperatorDetailRight/grpOperatorSkills/" +
                    "grpSkillIconRoot/grpSkillIcon_0")?.gameObject,
                "Assets/Resources/Presentation/OperatorAbilityIcon.prefab");

            AssertCharacterInfoPreview(scene);
            AssertDungeonItemPreviews(scene);
            AssertDungeonChoicePreviews(scene);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertCharacterInfoPreview(Scene scene)
    {
        DungeonPage page = FindOne<DungeonPage>(scene);
        SerializedObject pageSerialized = new(page);
        RectTransform root = pageSerialized
            .FindProperty("playerCharacterRoot").objectReferenceValue
            as RectTransform;
        Assert.That(root, Is.Not.Null);

        CharacterRuntime[] characters = DirectComponents<CharacterRuntime>(
            root).ToArray();
        Assert.That(characters.Length, Is.EqualTo(1));
        AssertPrefabPath(
            characters[0].gameObject,
            "Assets/Resources/Presentation/CharacterInfo.prefab");

        RectTransform buffRoot = new SerializedObject(characters[0])
            .FindProperty("buffIconContainer").objectReferenceValue
            as RectTransform;
        CharacterBuffIconView[] buffs = DirectComponents<
            CharacterBuffIconView>(buffRoot).ToArray();
        Assert.That(buffs.Length, Is.EqualTo(1));
        AssertPrefabPath(
            buffs[0].gameObject,
            "Assets/Resources/Presentation/CharacterBuffIcon.prefab");
    }

    private static void AssertDungeonItemPreviews(Scene scene)
    {
        foreach (DungeonItemHandView hand in FindAll<DungeonItemHandView>(scene))
        {
            DungeonItemCardView[] cards = DirectComponents<
                DungeonItemCardView>(hand.transform).ToArray();
            Assert.That(cards.Length, Is.EqualTo(1));
            AssertPrefabPath(
                cards[0].gameObject,
                "Assets/Resources/Presentation/BattleItemCard.prefab");
        }

        foreach (DungeonSpawnQueueView queue in
                 FindAll<DungeonSpawnQueueView>(scene))
        {
            RectTransform content = new SerializedObject(queue)
                .FindProperty("content").objectReferenceValue
                as RectTransform;
            DungeonSpawnQueueItemView[] items = DirectComponents<
                DungeonSpawnQueueItemView>(content).ToArray();
            Assert.That(items.Length, Is.EqualTo(1));
            AssertPrefabPath(
                items[0].gameObject,
                "Assets/Prefabs/UI/Dungeon/DungeonSpawnQueueItem.prefab");
        }

        EnemyCard[] enemyCards = FindAll<DungeonTileView>(scene)
            .SelectMany(tile =>
            {
                RectTransform stack = new SerializedObject(tile)
                    .FindProperty("stackRoot").objectReferenceValue
                    as RectTransform;
                return DirectComponents<EnemyCard>(stack);
            })
            .ToArray();
        Assert.That(enemyCards.Length, Is.EqualTo(1));
        AssertPrefabPath(
            enemyCards[0].gameObject,
            "Assets/Prefabs/UI/Dungeon/EnemyCard.prefab");
    }

    private static void AssertDungeonChoicePreviews(Scene scene)
    {
        Transform[] eventButtonRoots = FindTransforms(
            scene,
            "grpEventButtons").ToArray();
        Transform[] roomChoiceRoots = FindTransforms(
            scene,
            "grpRoomChoices").ToArray();
        Assert.That(eventButtonRoots.Length, Is.GreaterThan(0));
        Assert.That(roomChoiceRoots.Length, Is.GreaterThan(0));
        foreach (Transform root in eventButtonRoots.Concat(roomChoiceRoots))
        {
            DungeonDynamicChoiceButtonView[] buttons = DirectComponents<
                DungeonDynamicChoiceButtonView>(root).ToArray();
            Assert.That(buttons.Length, Is.EqualTo(1));
            AssertPrefabPath(
                buttons[0].gameObject,
                "Assets/Resources/Presentation/DungeonChoiceButton.prefab");
        }

        DungeonRewardCardView[] rewards = FindTransforms(
                scene,
                "grpRewardCards")
            .SelectMany(DirectComponents<DungeonRewardCardView>)
            .ToArray();
        Assert.That(rewards.Length, Is.GreaterThan(0));
        foreach (DungeonRewardCardView reward in rewards)
        {
            AssertPrefabPath(
                reward.gameObject,
                "Assets/Resources/Presentation/DungeonRewardCard.prefab");
        }

        DungeonStartingItemSlotView[] startingItems = FindTransforms(
                scene,
                "grpRewardCards")
            .SelectMany(DirectComponents<DungeonStartingItemSlotView>)
            .ToArray();
        Assert.That(startingItems.Length, Is.EqualTo(1));
        AssertPrefabPath(
            startingItems[0].gameObject,
            "Assets/Resources/Presentation/" +
            "DungeonStartingItemSlot.prefab");
    }

    private static void AssertPrefabPath(
        GameObject instance,
        string expectedPath)
    {
        Assert.That(instance, Is.Not.Null, expectedPath);
        GameObject source = PrefabUtility
            .GetCorrespondingObjectFromSource(instance);
        Assert.That(source, Is.Not.Null, instance.name);
        Assert.That(
            AssetDatabase.GetAssetPath(source),
            Is.EqualTo(expectedPath),
            instance.name);
    }

    private static IEnumerable<T> DirectComponents<T>(Transform root)
        where T : Component
    {
        Assert.That(root, Is.Not.Null);
        for (int index = 0; index < root.childCount; index++)
        {
            T component = root.GetChild(index).GetComponent<T>();
            if (component != null)
                yield return component;
        }
    }

    private static T FindOne<T>(Scene scene)
        where T : Component
    {
        T[] items = FindAll<T>(scene).ToArray();
        Assert.That(items.Length, Is.EqualTo(1), typeof(T).Name);
        return items[0];
    }

    private static IEnumerable<T> FindAll<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static IEnumerable<Transform> FindTransforms(
        Scene scene,
        string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(item => item.name == objectName);
    }

    private static Scene OpenScene(out bool opened)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        opened = !scene.IsValid() || !scene.isLoaded;
        return opened
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
            : scene;
    }
}
