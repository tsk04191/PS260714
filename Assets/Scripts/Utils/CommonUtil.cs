using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CommonUtil
{
    public static bool IntToBool(int i)
    {
        return i == 1;
    }

    public static int BoolToInt(bool b)
    {
        return b ? 1 : 0;
    }

    public static List<GameObject> GetChilds(Transform parrent)
    {
        List<GameObject> childs = new List<GameObject>();

        for (int i = parrent.childCount - 1; i >= 0; i--)
        {
            childs.Add(parrent.GetChild(i).gameObject);
        }

        childs.Reverse();
        return childs;
    }

    public static async UniTask ClearChilds(Transform parrent, CancellationToken cancellationToken = default)
    {
        if (parrent == null)
            return;

        List<UniTask> destroyTasks = new List<UniTask>();

        for (int i = parrent.childCount - 1; i >= 0; i--)
        {
            destroyTasks.Add(DestroyGameObject(parrent.GetChild(i).gameObject, cancellationToken));
        }

        await UniTask.WhenAll(destroyTasks);
    }

    private static async UniTask DestroyGameObject(GameObject target, CancellationToken cancellationToken = default)
    {
        if (target == null)
            return;

        GameObject.Destroy(target);
        await UniTask.WaitUntil(() => target == null, cancellationToken: cancellationToken);
    }

    public static void ArrayImageSet(Image[] imgs, bool b, int index = -1)
    {
        if (index == -1)
            index = imgs.Length;
        else if (index < 1)
            return;

        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] == null)
                continue;

            if (i < index)
                imgs[i].gameObject.SetActive(b);
            else
                return;
        }
    }

    public static void ArrayImageSpriteSet(Image[] imgs, Sprite img, int index = -1)
    {
        if (index == -1)
            index = imgs.Length;
        else if (index < 1)
            return;

        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] == null)
                continue;

            if (i < index)
                imgs[i].sprite = img;
            else
                return;
        }
    }

    public static void ArrayImageColorSet(Image[] imgs, Color clr, int index = -1)
    {
        if (index == -1)
            index = imgs.Length;
        else if (index < 1)
            return;

        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] == null)
                continue;
                
            if (i < index)
                imgs[i].color = clr;
            else
                return;
        }
    }
    public static void ArrayImageColorSet(Image[] imgs, Color clrA, Color clrB, int index = -1)
    {
        if (index == -1)
            index = imgs.Length;
        else if (index < 0)
            return;

        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] == null)
                continue;
                
            if (i < index)
                imgs[i].color = clrA;
            else
                imgs[i].color = clrB;
        }
    }
}
