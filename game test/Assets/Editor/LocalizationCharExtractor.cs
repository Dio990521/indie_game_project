using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// 从所有本地化字符串表里提取指定语言的唯一字符集，
/// 用于 TMP Font Asset Creator 的 Custom Characters 字段。
/// 菜单：Tools → Extract Localization Characters
/// </summary>
public static class LocalizationCharExtractor
{
    [MenuItem("Tools/Extract Localization Characters")]
    public static void Extract()
    {
        // 分语言收集字符
        var charSets = new Dictionary<string, HashSet<char>>
        {
            { "zh-Hans", new HashSet<char>() },
            { "zh-Hant", new HashSet<char>() },
            { "ja",      new HashSet<char>() },
        };

        // 遍历项目里所有 StringTableCollection
        var collections = LocalizationEditorSettings.GetStringTableCollections();
        foreach (var collection in collections)
        {
            foreach (var table in collection.StringTables)
            {
                string locale = table.LocaleIdentifier.Code;
                if (!charSets.TryGetValue(locale, out var set)) continue;

                foreach (var entry in table.Values)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    foreach (char c in entry.Value)
                        set.Add(c);
                }
            }
        }

        // 输出到 Assets/Editor/ExtractedChars/ 目录
        string outputDir = "Assets/Editor/ExtractedChars";
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "../", outputDir));

        var sb = new StringBuilder();
        sb.AppendLine("=== 提取完成，将对应内容粘贴进 Font Asset Creator → Custom Characters ===\n");

        foreach (var kv in charSets)
        {
            string locale   = kv.Key;
            string chars    = new string(kv.Value.OrderBy(c => c).ToArray());
            string filePath = $"{outputDir}/chars_{locale}.txt";

            File.WriteAllText(
                Path.Combine(Application.dataPath, "../", filePath),
                chars, Encoding.UTF8);

            sb.AppendLine($"[{locale}]  共 {chars.Length} 个字符 → 已保存到 {filePath}");
        }

        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());

        EditorUtility.DisplayDialog(
            "字符提取完成",
            sb.ToString(),
            "OK");
    }
}
