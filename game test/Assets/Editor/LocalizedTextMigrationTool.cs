using System.Collections.Generic;
using IndieGame.UI.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Components;

/// <summary>
/// 批量将场景/Prefab 中的 TextMeshProUGUI 替换为 LocalizedText，
/// 同时自动清除已被 LocalizedText 内置取代的 FontLocalizationSetter 和 LocalizeStringEvent。
/// 菜单：Tools → Localization → Migrate Selected GameObjects (TMP → LocalizedText)
/// </summary>
public static class LocalizedTextMigrationTool
{
    // ─── 菜单项 ───────────────────────────────────────────────────────────────

    [MenuItem("Tools/Localization/Migrate Selected GameObjects (TMP → LocalizedText)")]
    private static void MigrateSelected()
    {
        var targets = CollectFromSelection();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("迁移工具", "请先在 Hierarchy 或 Project 中选中要迁移的对象。", "OK");
            return;
        }
        var result = MigrateComponents(targets);
        EditorUtility.DisplayDialog("迁移完成",
            $"替换 TextMeshProUGUI：{result.replaced} 个\n" +
            $"移除 FontLocalizationSetter：{result.removedFont} 个\n" +
            $"移除 LocalizeStringEvent：{result.removedLocalize} 个",
            "OK");
    }

    [MenuItem("Tools/Localization/Migrate Selected GameObjects (TMP → LocalizedText)", true)]
    private static bool MigrateSelectedValidate() => Selection.gameObjects.Length > 0;

    // ─── 核心逻辑 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 从当前 Selection 收集所有 GameObject（含子节点）。
    /// 同时支持 Hierarchy 选中和 Project 窗口选中 Prefab。
    /// </summary>
    private static List<GameObject> CollectFromSelection()
    {
        var result = new List<GameObject>();
        foreach (var go in Selection.gameObjects)
        {
            // Project 窗口选中的是 Prefab Asset，需要打开才能修改
            if (!go.scene.IsValid())
            {
                // Prefab Asset：用 PrefabUtility 修改并保存
                result.Add(go);
            }
            else
            {
                result.Add(go);
            }
        }
        return result;
    }

    private struct MigrateResult
    {
        public int replaced;
        public int removedFont;
        public int removedLocalize;
    }

    private static MigrateResult MigrateComponents(List<GameObject> roots)
    {
        var total = new MigrateResult();
        foreach (var root in roots)
        {
            bool isPrefabAsset = !root.scene.IsValid();

            if (isPrefabAsset)
            {
                string path = AssetDatabase.GetAssetPath(root);
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                var r = ProcessGameObject(prefabRoot);
                if (r.replaced + r.removedFont + r.removedLocalize > 0)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Accumulate(ref total, r);
            }
            else
            {
                Accumulate(ref total, ProcessGameObject(root));
            }
        }
        return total;
    }

    private static void Accumulate(ref MigrateResult dst, MigrateResult src)
    {
        dst.replaced        += src.replaced;
        dst.removedFont     += src.removedFont;
        dst.removedLocalize += src.removedLocalize;
    }

    /// <summary>
    /// 递归处理目标及其所有子节点：替换 TMP，清除冗余本地化组件。
    /// </summary>
    private static MigrateResult ProcessGameObject(GameObject target)
    {
        var result = new MigrateResult();

        // ── 1. 替换 TextMeshProUGUI → LocalizedText ──────────────────────────
        var allTMP = target.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in allTMP)
        {
            if (tmp is LocalizedText) continue;
            if (ReplaceOne(tmp)) result.replaced++;
        }

        // ── 2. 清除 FontLocalizationSetter（功能已内置于 LocalizedText）────────
        var allFLS = target.GetComponentsInChildren<FontLocalizationSetter>(true);
        foreach (var fls in allFLS)
        {
            Undo.DestroyObjectImmediate(fls);
            result.removedFont++;
        }

        // ── 3. 清除 LocalizeStringEvent（功能已内置于 LocalizedText）──────────
        var allLSE = target.GetComponentsInChildren<LocalizeStringEvent>(true);
        foreach (var lse in allLSE)
        {
            Undo.DestroyObjectImmediate(lse);
            result.removedLocalize++;
        }

        return result;
    }

    /// <summary>
    /// 将单个 TextMeshProUGUI 替换为 LocalizedText，保留所有序列化字段。
    /// </summary>
    private static bool ReplaceOne(TextMeshProUGUI src)
    {
        var go = src.gameObject;

        // ── 1. 读取原始数据 ──────────────────────────────────────────────────
        string  savedText        = src.text;
        var     savedFont        = src.font;
        float   savedFontSize    = src.fontSize;
        bool    savedAutoSize    = src.enableAutoSizing;
        float   savedMinSize     = src.fontSizeMin;
        float   savedMaxSize     = src.fontSizeMax;
        var     savedColor       = src.color;
        var     savedAlignment   = src.alignment;
        bool    savedRaycast     = src.raycastTarget;
        var     savedWordWrap    = src.textWrappingMode;
        var     savedOverflow    = src.overflowMode;
        var     savedStyle       = src.fontStyle;
        float   savedCharSpacing = src.characterSpacing;
        float   savedLineSpacing = src.lineSpacing;

        // ── 2. 移除旧组件并添加新组件 ────────────────────────────────────────
        Undo.DestroyObjectImmediate(src);
        var lt = Undo.AddComponent<LocalizedText>(go);

        // ── 3. 回写属性 ──────────────────────────────────────────────────────
        lt.text              = savedText;
        lt.font              = savedFont;
        lt.fontSize          = savedFontSize;
        lt.enableAutoSizing  = savedAutoSize;
        lt.fontSizeMin       = savedMinSize;
        lt.fontSizeMax       = savedMaxSize;
        lt.color             = savedColor;
        lt.alignment         = savedAlignment;
        lt.raycastTarget     = savedRaycast;
        lt.textWrappingMode  = savedWordWrap;
        lt.overflowMode      = savedOverflow;
        lt.fontStyle         = savedStyle;
        lt.characterSpacing  = savedCharSpacing;
        lt.lineSpacing       = savedLineSpacing;

        EditorUtility.SetDirty(go);
        return true;
    }
}
