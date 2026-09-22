using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 图鉴单条目：一个角色在图鉴格子（Example）与 Card 上展示所需的全部数据。
/// 在 BookCatalog.asset 的 Inspector 里填写。
/// </summary>
[Serializable]
public sealed class BookEntry
{
    [Tooltip("稳定 ID（唯一，解锁记录用）")]
    public string id = string.Empty;
    [Tooltip("角色显示名")]
    public string displayName = string.Empty;
    [Tooltip("所属派系（对应 BookCatalog.categories 里的页签 ID）")]
    public string categoryId = string.Empty;
    [Tooltip("Card 种族图标（对应 Card 上 Race 子对象名，如 mouse/cat/chicken/goat/insect/rabbit/dog）")]
    public string raceId = string.Empty;
#if UNITY_EDITOR
    [Tooltip("图鉴格子头像（编辑器预览用，出包自动剔除，不占首包）")]
    public Sprite avatarSprite;

    [Tooltip("Card 立绘（编辑器预览用，出包自动剔除，不占首包）")]
    public Sprite roleSprite;
#endif

    [Tooltip("图鉴格子头像资源键（SpriteRegistry 键，运行时经框架加载；编辑器下留空则用拖拽预览图）")]
    public string avatarSpriteKey = string.Empty;

    [Tooltip("Card 立绘资源键（SpriteRegistry 键，运行时经框架加载）")]
    public string roleSpriteKey = string.Empty;
    [Tooltip("星级 1-7：Card 上点亮前 N 颗星")]
    [Range(1, 7)]
    public int star = 1;
    [Tooltip("对应单位预制体键：关卡内加载到该键即视为“遇见”并解锁（可空）")]
    public string prefabKey = string.Empty;
    [Tooltip("兵营等级达到该值即视为“已拥有”（0 = 不需要兵营条件）")]
    [Min(0)]
    public int unlockBarracksLevel = 0;

    [Header("Card 数值文本（填什么显示什么）")]
    public string repelText = string.Empty;
    public string quantityText = string.Empty;
    public string scopeText = string.Empty;
    public string timeText = string.Empty;
    public string weightText = string.Empty;
    public string coinText = string.Empty;

    [Header("Card 简介")]
    [TextArea(3, 8)]
    public string explainText = string.Empty;

    /// <summary>编辑器下键缺失时使用的头像直引（玩家构建中为 null，必须靠资源键加载）。</summary>
#if UNITY_EDITOR
    public Sprite AvatarEditorFallback => avatarSprite;
#else
    public Sprite AvatarEditorFallback => null;
#endif

    /// <summary>编辑器下键缺失时使用的立绘直引（玩家构建中为 null，必须靠资源键加载）。</summary>
#if UNITY_EDITOR
    public Sprite RoleEditorFallback => roleSprite;
#else
    public Sprite RoleEditorFallback => null;
#endif
}

/// <summary>
/// 图鉴目录资产：派系页签顺序、Card 种族选项与全部条目。
/// 在 Inspector 里点“+ 新增条目”逐条填写。
/// </summary>
[CreateAssetMenu(fileName = "BookCatalog", menuName = "Book/Catalog")]
public sealed class BookCatalog : ScriptableObject
{
    [Tooltip("派系页签 ID 顺序（与场景里页签按钮一一对应，可增删）")]
    public List<string> categories = new List<string> { "mouse", "cat", "chicken", "goat", "magic", "en" };

    [Tooltip("Card 种族图标选项（与 Card 上 Race 子对象名一一对应）")]
    public List<string> raceOptions = new List<string> { "mouse", "cat", "chicken", "goat", "insect", "rabbit", "dog" };

    [SerializeField] private List<BookEntry> entries = new List<BookEntry>();

    public IReadOnlyList<BookEntry> Entries => entries;

    /// <summary>按派系 ID 取出全部条目（页签切换用）。</summary>
    public List<BookEntry> GetEntriesByCategory(string categoryId)
    {
        var result = new List<BookEntry>();
        if (string.IsNullOrEmpty(categoryId))
            return result;

        for (int i = 0; i < entries.Count; i++)
        {
            BookEntry entry = entries[i];
            if (entry != null && entry.categoryId == categoryId)
                result.Add(entry);
        }
        return result;
    }

    /// <summary>按单位预制体键取条目（“遇见”解锁判定用）。</summary>
    public BookEntry GetEntryByPrefabKey(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            BookEntry entry = entries[i];
            if (entry != null && entry.prefabKey == prefabKey)
                return entry;
        }
        return null;
    }
}

/// <summary>
/// 图鉴解锁进度服务（静态）。
/// 解锁规则：关卡内遇见该兵种（按 prefabKey 标记）或已拥有该兵种（兵营等级达标），
/// 任一满足即解锁，并把结果持久化到 UserGlobalInfo。
/// </summary>
public static class BookProgress
{
    private static BookCatalog _catalog;
    private static readonly HashSet<string> _encountered = new HashSet<string>();

    public static void RegisterCatalog(BookCatalog catalog)
    {
        if (catalog != null)
            _catalog = catalog;
    }

    /// <summary>标记“遇见”：关卡实例化出该预制体键时调用。</summary>
    public static void MarkEncountered(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey) || _encountered.Contains(prefabKey))
            return;

        _encountered.Add(prefabKey);

        if (_catalog != null)
        {
            BookEntry entry = _catalog.GetEntryByPrefabKey(prefabKey);
            if (entry != null)
                PersistUnlock(entry.id);
        }
    }

    /// <summary>条目是否已解锁（遇见 或 拥有）。评估到“拥有”时自动解锁并持久化。</summary>
    public static bool IsUnlocked(BookEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return false;

        if (UserGlobalInfo.Instance != null && UserGlobalInfo.Instance.IsBookEntryUnlocked(entry.id))
            return true;

        if (!string.IsNullOrEmpty(entry.prefabKey) && _encountered.Contains(entry.prefabKey))
            return PersistUnlock(entry.id);

        if (entry.unlockBarracksLevel > 0 && UserGlobalInfo.Instance != null &&
            UserGlobalInfo.Instance.BarracksLevel >= entry.unlockBarracksLevel)
            return PersistUnlock(entry.id);

        return false;
    }

    private static bool PersistUnlock(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || UserGlobalInfo.Instance == null)
            return false;

        UserGlobalInfo.Instance.TryUnlockBookEntry(entryId);
        return true;
    }
}