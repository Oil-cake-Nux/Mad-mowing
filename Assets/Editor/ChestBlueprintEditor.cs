using UnityEditor;
using UnityEngine;
using Vampire;

// 为ChestBlueprint自定义Inspector绘制逻辑
[CustomEditor(typeof(ChestBlueprint))]
public class ChestBlueprintEditor : Editor
{
    private ChestBlueprint _target;

    private void OnEnable()
    {
        // 获取当前编辑的ChestBlueprint实例
        _target = (ChestBlueprint)target;
    }

    // 重写Inspector绘制逻辑
    public override void OnInspectorGUI()
    {
        // 绘制默认字段（Ability Chest、宝箱精灵等）
        DrawDefaultInspector();

        // 分隔线，区分核心字段和lootTable
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("=== 宝箱掉落表 ===", EditorStyles.boldLabel);

        // 处理lootTable为空的情况
        if (_target.lootTable == null)
        {
            EditorGUILayout.HelpBox("掉落表（lootTable）未初始化，请先赋值！", MessageType.Warning);
            return;
        }

        // 自定义绘制lootTable的数组元素，避免原生绘制的Bug
        SerializedProperty lootTableProp = serializedObject.FindProperty("lootTable");
        SerializedProperty lootArrayProp = lootTableProp.FindPropertyRelative("lootTable");

        // 绘制数组长度设置框
        lootArrayProp.arraySize = EditorGUILayout.IntField("掉落项数量", lootArrayProp.arraySize);

        // 遍历数组，逐个绘制每个Loot<GameObject>元素
        for (int i = 0; i < lootArrayProp.arraySize; i++)
        {
            SerializedProperty elementProp = lootArrayProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box"); // 用盒子包裹，区分每个元素

            // 绘制元素索引
            EditorGUILayout.LabelField($"掉落项 {i + 1}", EditorStyles.miniBoldLabel);

            // 绘制item字段（GameObject类型）
            SerializedProperty itemProp = elementProp.FindPropertyRelative("item");
            EditorGUILayout.PropertyField(itemProp, new GUIContent("物品预制体"));

            // 绘制dropChance字段（带范围限制）
            SerializedProperty chanceProp = elementProp.FindPropertyRelative("dropChance");
            EditorGUILayout.Slider(chanceProp, 0f, 1f, new GUIContent("掉落概率 (0~1)"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Separator();
        }

        // 保存序列化数据的修改
        serializedObject.ApplyModifiedProperties();
    }
}