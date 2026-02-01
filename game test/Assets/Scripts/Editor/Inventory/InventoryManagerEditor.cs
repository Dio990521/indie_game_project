using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Editor.Inventory
{
    /// <summary>
    /// 背包管理器编辑器：
    /// 提供快速测试按钮（添加道具 / 整理背包）。
    /// </summary>
    [CustomEditor(typeof(InventoryManager))]
    public class InventoryManagerEditor : UnityEditor.Editor
    {
        private ItemSO _itemToAdd;
        private int _amountToAdd = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            _itemToAdd = (ItemSO)EditorGUILayout.ObjectField("Item To Add", _itemToAdd, typeof(ItemSO), false);
            _amountToAdd = Mathf.Max(1, EditorGUILayout.IntField("Amount", _amountToAdd));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加道具到背包"))
                {
                    InventoryManager manager = (InventoryManager)target;
                    if (_itemToAdd == null)
                    {
                        Debug.LogWarning("[InventoryManagerEditor] Item is null.");
                    }
                    else
                    {
                        Undo.RecordObject(manager, "Add Item To Inventory");
                        manager.AddItem(_itemToAdd, _amountToAdd);
                        EditorUtility.SetDirty(manager);
                    }
                }

                if (GUILayout.Button("整理背包（按分类）"))
                {
                    InventoryManager manager = (InventoryManager)target;
                    Undo.RecordObject(manager, "Sort Inventory By Category");
                    manager.SortByCategory();
                    EditorUtility.SetDirty(manager);
                }
            }
        }
    }
}
