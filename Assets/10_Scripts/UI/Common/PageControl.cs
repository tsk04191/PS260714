using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IPage
{
    public AudioSource Speaker { get; set; }

    public void Open(PageOpenMode mode = PageOpenMode.Fresh);
    public void Close();
    public void Init();
}

public enum PageOpenMode
{
    Fresh,
    Resume,
}

public static class PageControl
{
    public static void PagToPag(GameObject pagFrom, GameObject pagTo, PageOpenMode mode = PageOpenMode.Fresh)
    {
        if (!TryGetPage(pagFrom, out IPage pageFrom) ||
            !TryGetPage(pagTo, out IPage pageTo))
        {
            return;
        }

        if (pageTo is IPageLoadingTarget loadingTarget &&
            loadingTarget.RequiresLoading(mode) &&
            LoadingPage.TryBeginTransition(() =>
            {
                pageFrom.Close();
                OpenPage(pagTo, pageTo, mode);
            }))
        {
            return;
        }

        pageFrom.Close();
        OpenPage(pagTo, pageTo, mode);
    }

    public static void TabInTabs(GameObject[] tabs, GameObject tabTo)
    {
        if (tabs == null || tabTo == null)
            return;

        int tabIndex = Array.IndexOf(tabs, tabTo);
        if (tabIndex < 0)
            return;

        TabInTabs(tabs, tabIndex);
    }

    public static void TabInTabs(GameObject[] tabs, int tabTo)
    {
        if (tabs == null || tabTo < 0 || tabTo >= tabs.Length)
            return;

        if (!TryResolvePages(tabs, out IPage[] pages))
            return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (i == tabTo)
                OpenPage(tabs[i], pages[i], PageOpenMode.Fresh);
            else
                pages[i].Close();
        }
    }

    public static void TabSetDefault(GameObject[] tabs, PageOpenMode mode = PageOpenMode.Fresh)
    {
        if (!TryResolvePages(tabs, out IPage[] pages))
            return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (i == 0)
            {
                OpenPage(tabs[i], pages[i], mode);
            }
            else
            {
                pages[i].Close();
            }
        }
    }

    public static void Popup(GameObject pop)
    {
        if (!TryGetPage(pop, out IPage page))
            return;

        OpenPage(pop, page, PageOpenMode.Fresh);
    }

    public static void DropdownInit(TMP_Dropdown drd, List<string> options)
    {
        if (drd == null || options == null)
            return;

        drd.ClearOptions();
        drd.AddOptions(options);
        drd.RefreshShownValue();
    }

    public static void TabInTabsColoring(GameObject tabToButton, GameObject[] tabButtons, List<Color> tabSelectedColor, List<Color> tabUnselectedColor)
    {
        if (tabToButton == null || tabButtons == null ||
            tabSelectedColor == null || tabUnselectedColor == null)
        {
            return;
        }

        int index = Array.IndexOf(tabButtons, tabToButton);
        if (index < 0 || !CanApplyTabColors(tabButtons, index, tabSelectedColor, tabUnselectedColor))
            return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (i == index)
                UIChildColorPairing(tabButtons[i], tabSelectedColor);
            else
                UIChildColorPairing(tabButtons[i], tabUnselectedColor);
        }
    }

    public static void UIChildColorPairing(GameObject parent, Color data)
    {
        if (parent == null)
            return;

        Transform parentTransform = parent.transform;
        for (int i = 0; i < parentTransform.childCount; i++)
        {
            GameObject child = parentTransform.GetChild(i).gameObject;

            if (child.TryGetComponent<TMP_Text>(out TMP_Text text))
                text.color = data;
            else if (child.TryGetComponent<Image>(out Image image))
                image.color = data;
        }
    }

    public static void UIChildColorPairing(GameObject parent, List<Color> data)
    {
        if (parent == null || data == null)
            return;

        Transform parentTransform = parent.transform;
        if (parentTransform.childCount != data.Count)
            return;

        for (int i = 0; i < parentTransform.childCount; i++)
        {
            GameObject child = parentTransform.GetChild(i).gameObject;

            if (child.TryGetComponent<TMP_Text>(out TMP_Text text))
                text.color = data[i];
            else if (child.TryGetComponent<Image>(out Image image))
                image.color = data[i];
        }
    }

    private static bool TryGetPage(GameObject target, out IPage page)
    {
        page = null;
        return target != null && target.TryGetComponent(out page);
    }

    private static void OpenPage(
        GameObject target,
        IPage page,
        PageOpenMode mode)
    {
        page.Open(mode);
        // Dungeon music is selected by the active dungeon theme so that its
        // intro/phase loops/exit sequence cannot be replaced by a page track.
        if (page is DungeonPage)
            return;

        if (target != null &&
            target.TryGetComponent(out PageBgmSelection selection))
        {
            selection.RequestSelectedBgm();
            return;
        }

        // Stage Select intentionally shares the configurable Main page track.
        // This also replaces a dungeon theme after returning from a run.
        if (page is StageSelectPage stageSelectPage)
            stageSelectPage.RequestMainMenuBgm();
    }

    private static bool TryResolvePages(GameObject[] targets, out IPage[] pages)
    {
        pages = null;

        if (targets == null || targets.Length == 0)
            return false;

        IPage[] resolvedPages = new IPage[targets.Length];

        for (int i = 0; i < targets.Length; i++)
        {
            if (!TryGetPage(targets[i], out IPage page))
                return false;

            resolvedPages[i] = page;
        }

        pages = resolvedPages;
        return true;
    }

    private static bool CanApplyTabColors(GameObject[] tabButtons, int selectedIndex, List<Color> selectedColors, List<Color> unselectedColors)
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
                return false;

            int requiredColorCount = i == selectedIndex ? selectedColors.Count : unselectedColors.Count;
            if (tabButtons[i].transform.childCount != requiredColorCount)
                return false;
        }

        return true;
    }
}
