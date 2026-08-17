using System.Collections.Generic;
using UnityEngine;

public sealed class BattleAreaPreviewView
{
    private readonly GameObject _root;
    private readonly DungeonBattleAreaPreviewPrefabView _view;

    public BattleAreaPreviewView(GameObject prefab, Transform parent)
    {
        _root = prefab != null && parent != null
            ? UnityEngine.Object.Instantiate(prefab, parent)
            : null;
        if (_root != null)
            SetLayerRecursively(_root, parent.gameObject.layer);
        _view = _root != null
            ? _root.GetComponent<DungeonBattleAreaPreviewPrefabView>()
            : null;
        if (_view == null || !_view.HasRequiredReferences)
        {
            Debug.LogError(
                "Dungeon battle area preview prefab references are incomplete.",
                _root);
        }
        _view?.Hide();
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public void Show(
        Vector2 origin,
        Vector2 direction,
        BattleAreaDefinition definition)
    {
        _view?.Show(origin, direction, definition);
    }

    public void Hide()
    {
        _view?.Hide();
    }

    public void Dispose()
    {
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
    }
}

public static class DungeonWorldSpawnGeometry
{
    public static float ResolveSpawnLineRadius(
        IReadOnlyList<Vector2> viewportGroundCorners,
        float padding)
    {
        float radius = 0f;
        if (viewportGroundCorners?.Count >= 4)
        {
            float bottomWidth = Vector2.Distance(
                viewportGroundCorners[0],
                viewportGroundCorners[2]);
            float topWidth = Vector2.Distance(
                viewportGroundCorners[1],
                viewportGroundCorners[3]);
            int leftIndex = topWidth >= bottomWidth ? 1 : 0;
            int rightIndex = topWidth >= bottomWidth ? 3 : 2;
            radius = Mathf.Max(
                viewportGroundCorners[leftIndex].magnitude,
                viewportGroundCorners[rightIndex].magnitude);
        }
        else if (viewportGroundCorners != null)
        {
            for (int index = 0;
                 index < viewportGroundCorners.Count;
                 index++)
            {
                radius = Mathf.Max(
                    radius,
                    viewportGroundCorners[index].magnitude);
            }
        }

        return radius + Mathf.Max(0f, padding);
    }

    public static Vector2 DirectionFromUnitSample(float sample)
    {
        float angle = Mathf.Clamp01(sample) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
