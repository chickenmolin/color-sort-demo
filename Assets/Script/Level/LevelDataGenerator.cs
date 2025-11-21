using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[System.Serializable]
public class BottleData
{
    public int colorCount;
    public int[] colorIndices = new int[4]; // 0=Red, 1=Blue, 2=Green, 3=Yellow, etc
}

[System.Serializable]
public class LevelData
{
    public int levelNumber;
    public List<BottleData> bottles = new List<BottleData>();
}

[System.Serializable]
public class LevelListData
{
    public List<LevelData> levels = new List<LevelData>();
}

public class LevelDataGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ColorOption
    {
        public string colorName;
        public Color color;
    }

    [SerializeField] private List<ColorOption> availableColors = new List<ColorOption>();
    [SerializeField] private string jsonFilePath = "Assets/Data/levels.json";

    [Header("Level Generation Settings")]
    [SerializeField] private int startLevelNumber = 1;
    [SerializeField] private int endLevelNumber = 10;
    [SerializeField] private int totalBottlesPerLevel = 4;
    [SerializeField] private int emptyBottles = 1;

    [Header("Bottle Configuration")]
    [SerializeField] public List<int> bottleColorCounts = new List<int>(); // Số lượng từng màu
    [SerializeField] public LevelListData generatedLevels = new LevelListData();

    private void Start()
    {
        InitializeDefaultColors();
    }

    private void InitializeDefaultColors()
    {
        if (availableColors.Count == 0)
        {
            availableColors.Add(new ColorOption { colorName = "Red", color = Color.red });
            availableColors.Add(new ColorOption { colorName = "Blue", color = Color.blue });
            availableColors.Add(new ColorOption { colorName = "Green", color = Color.green });
            availableColors.Add(new ColorOption { colorName = "Yellow", color = new Color(1, 1, 0) });
        }
    }

    public void GenerateLevels()
    {
        if (bottleColorCounts.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Bottle Color Counts không được rỗng!", "OK");
            return;
        }

        generatedLevels.levels.Clear();

        for (int levelNum = startLevelNumber; levelNum <= endLevelNumber; levelNum++)
        {
            LevelData newLevel = new LevelData { levelNumber = levelNum };

            // Tạo các ống có màu
            int bottleIndex = 0;
            for (int colorIdx = 0; colorIdx < bottleColorCounts.Count; colorIdx++)
            {
                int countForThisColor = bottleColorCounts[colorIdx];

                // Chia màu này vào các ống
                for (int i = 0; i < countForThisColor; i++)
                {
                    if (bottleIndex >= totalBottlesPerLevel - emptyBottles)
                        break;

                    BottleData bottle = new BottleData();

                    // Mỗi ống chứa 1-4 lớp của cùng một màu
                    int layersInThisBottle = Random.Range(1, Mathf.Min(5, countForThisColor - i + 1));
                    bottle.colorCount = layersInThisBottle;

                    for (int layer = 0; layer < layersInThisBottle; layer++)
                    {
                        bottle.colorIndices[layer] = colorIdx;
                    }

                    newLevel.bottles.Add(bottle);
                    bottleIndex++;
                    i += layersInThisBottle - 1;
                }
            }

            // Thêm các ống rỗng
            for (int i = 0; i < emptyBottles; i++)
            {
                BottleData emptyBottle = new BottleData { colorCount = 0 };
                newLevel.bottles.Add(emptyBottle);
            }

            // Shuffle bottles
            for (int i = newLevel.bottles.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                BottleData temp = newLevel.bottles[i];
                newLevel.bottles[i] = newLevel.bottles[randomIndex];
                newLevel.bottles[randomIndex] = temp;
            }

            generatedLevels.levels.Add(newLevel);
        }

        EditorUtility.DisplayDialog("Success",
            $"Generated {generatedLevels.levels.Count} levels!", "OK");
    }

    public void SaveToJSON()
    {
        string directory = Path.GetDirectoryName(jsonFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(generatedLevels, true);
        File.WriteAllText(jsonFilePath, json);

        EditorUtility.DisplayDialog("Success",
            $"Levels saved to {jsonFilePath}", "OK");
    }

    public void LoadFromJSON()
    {
        if (!File.Exists(jsonFilePath))
        {
            EditorUtility.DisplayDialog("Error",
                $"File not found: {jsonFilePath}", "OK");
            return;
        }

        string json = File.ReadAllText(jsonFilePath);
        generatedLevels = JsonUtility.FromJson<LevelListData>(json);

        EditorUtility.DisplayDialog("Success",
            $"Loaded {generatedLevels.levels.Count} levels!", "OK");
    }

    public void ClearLevels()
    {
        generatedLevels.levels.Clear();
    }

    public void AddColorCount()
    {
        bottleColorCounts.Add(1);
    }

    public void RemoveColorCount(int index)
    {
        if (index >= 0 && index < bottleColorCounts.Count)
        {
            bottleColorCounts.RemoveAt(index);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LevelDataGenerator))]
public class LevelDataGeneratorEditor : Editor
{
    private bool showColorCounts = true;
    private bool showGeneratedLevels = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelDataGenerator generator = (LevelDataGenerator)target;

        GUILayout.Space(20);
        GUILayout.Label("Configuration", EditorStyles.boldLabel);

        // Bottle Color Counts
        showColorCounts = EditorGUILayout.Foldout(showColorCounts, 
            "Bottle Color Counts (" + generator.bottleColorCounts?.Count + ")");
        
        if (showColorCounts)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < generator.bottleColorCounts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField("Color " + i, GUILayout.Width(60));
                generator.bottleColorCounts[i] = EditorGUILayout.IntField(
                    generator.bottleColorCounts[i], GUILayout.Width(100));
                
                EditorGUILayout.LabelField("bottles", GUILayout.Width(60));

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    generator.RemoveColorCount(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Color", GUILayout.Height(25)))
            {
                generator.AddColorCount();
            }

            EditorGUI.indentLevel--;
        }

        GUILayout.Space(20);
        GUILayout.Label("Generation & Save", EditorStyles.boldLabel);

        if (GUILayout.Button("GENERATE LEVELS", GUILayout.Height(35)))
        {
            generator.GenerateLevels();
        }

        if (GUILayout.Button("SAVE TO JSON", GUILayout.Height(35)))
        {
            generator.SaveToJSON();
        }

        if (GUILayout.Button("LOAD FROM JSON", GUILayout.Height(35)))
        {
            generator.LoadFromJSON();
        }

        if (GUILayout.Button("CLEAR ALL", GUILayout.Height(35)))
        {
            generator.ClearLevels();
        }

        // Display generated levels
        GUILayout.Space(20);
        showGeneratedLevels = EditorGUILayout.Foldout(showGeneratedLevels, 
            "Generated Levels Preview (" + generator.generatedLevels?.levels?.Count + ")");
        
        if (showGeneratedLevels && generator.generatedLevels?.levels != null)
        {
            EditorGUI.indentLevel++;

            foreach (LevelData level in generator.generatedLevels.levels)
            {
                EditorGUILayout.LabelField("Level " + level.levelNumber + " (" + level.bottles.Count + " bottles)", 
                    EditorStyles.boldLabel);
            }

            EditorGUI.indentLevel--;
        }
    }
}
#endif