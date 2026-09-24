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
    [Tooltip("对应单位预制体键：关卡内遇见该单位，或从任意途径获得该单位（商店/礼包/兵营训练等），即解锁（可空）")]
    public string prefabKey = string.Empty;

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
/// 解锁规则：关卡内遇见该兵种（按 prefabKey 标记）或从任意途径获得该兵种
/// （兵营训练产出、商店购买、礼包发放等，获得即视为拥有），任一满足即解锁，
/// 并把结果持久化到 UserGlobalInfo。解锁与兵营等级等成长需求无关。
/// 拥有状态独立记录：已解锁但尚未拥有的条目，图鉴头像显示为灰白色。
/// </summary>
public static class BookProgress
{
    private static BookCatalog _catalog;
    private static readonly HashSet<string> _unlockedPrefabKeys = new HashSet<string>();
    private static readonly HashSet<string> _ownedPrefabKeys = new HashSet<string>();

    public static void RegisterCatalog(BookCatalog catalog)
    {
        if (catalog != null)
            _catalog = catalog;
    }

    /// <summary>标记“遇见”：关卡实例化出该预制体键时调用（仅解锁，不视为拥有）。</summary>
    public static void MarkEncountered(string prefabKey)
    {
        UnlockByPrefabKey(prefabKey);
    }

    /// <summary>
    /// 标记“获得/拥有”：玩家从任意途径获得该兵种时调用
    /// （兵营训练产出、商店购买、礼包发放等）：解锁图鉴并记为“已拥有”。
    /// </summary>
    public static void MarkOwned(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey))
            return;

        _ownedPrefabKeys.Add(prefabKey);

        if (_catalog != null)
        {
            BookEntry entry = _catalog.GetEntryByPrefabKey(prefabKey);
            if (entry != null)
            {
                PersistOwned(entry.id);
            }
        }

        UnlockByPrefabKey(prefabKey);
    }

    /// <summary>按预制体键解锁对应图鉴条目并持久化（幂等）。</summary>
    private static void UnlockByPrefabKey(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey) || _unlockedPrefabKeys.Contains(prefabKey))
            return;

        _unlockedPrefabKeys.Add(prefabKey);

        if (_catalog != null)
        {
            BookEntry entry = _catalog.GetEntryByPrefabKey(prefabKey);
            if (entry != null)
                PersistUnlock(entry.id);
        }
    }

    /// <summary>条目是否已解锁（遇见 或 获得该兵种；评估到解锁时结果已持久化）。</summary>
    public static bool IsUnlocked(BookEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return false;

        if (UserGlobalInfo.Instance != null && UserGlobalInfo.Instance.IsBookEntryUnlocked(entry.id))
            return true;

        if (!string.IsNullOrEmpty(entry.prefabKey) && _unlockedPrefabKeys.Contains(entry.prefabKey))
            return PersistUnlock(entry.id);

        return false;
    }

    /// <summary>
    /// 条目对应兵种是否已被玩家拥有（任意途径获得过）。
    /// 已解锁但未拥有的条目在图鉴中头像显示为灰白色。
    /// </summary>
    public static bool IsOwned(BookEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return false;

        if (UserGlobalInfo.Instance != null && UserGlobalInfo.Instance.IsBookEntryOwned(entry.id))
            return true;

        return !string.IsNullOrEmpty(entry.prefabKey) && _ownedPrefabKeys.Contains(entry.prefabKey);
    }

    private static bool PersistUnlock(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || UserGlobalInfo.Instance == null)
            return false;

        UserGlobalInfo.Instance.TryUnlockBookEntry(entryId);
        return true;
    }

    private static bool PersistOwned(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || UserGlobalInfo.Instance == null)
            return false;

        UserGlobalInfo.Instance.TryOwnBookEntry(entryId);
        return true;
    }
}