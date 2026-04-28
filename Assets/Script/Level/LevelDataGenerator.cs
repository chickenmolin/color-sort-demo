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

        //XÓA ĐÚNG CÁC LEVEL ĐƯỢC TẠO LẠI
        generatedLevels.levels.RemoveAll(l =>
            l.levelNumber >= startLevelNumber &&
            l.levelNumber <= endLevelNumber);

        for (int levelNum = startLevelNumber; levelNum <= endLevelNumber; levelNum++)
        {
            LevelData newLevel = new LevelData { levelNumber = levelNum };

            int bottleIndex = 0;

            for (int colorIdx = 0; colorIdx < bottleColorCounts.Count; colorIdx++)
            {
                int countForThisColor = bottleColorCounts[colorIdx];

                for (int i = 0; i < countForThisColor; i++)
                {
                    if (bottleIndex >= totalBottlesPerLevel - emptyBottles)
                        break;

                    BottleData bottle = new BottleData();

                    int layersInThisBottle =
                        Random.Range(1, Mathf.Min(5, countForThisColor - i + 1));
                    bottle.colorCount = layersInThisBottle;

                    for (int layer = 0; layer < layersInThisBottle; layer++)
                        bottle.colorIndices[layer] = colorIdx;

                    newLevel.bottles.Add(bottle);
                    bottleIndex++;
                    i += layersInThisBottle - 1;
                }
            }

            // Empty bottles
            for (int i = 0; i < emptyBottles; i++)
                newLevel.bottles.Add(new BottleData());

            // Shuffle
            for (int i = newLevel.bottles.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                var tmp = newLevel.bottles[i];
                newLevel.bottles[i] = newLevel.bottles[r];
                newLevel.bottles[r] = tmp;
            }

            generatedLevels.levels.Add(newLevel);
        }

        EditorUtility.DisplayDialog("Success", $"Generated Levels Completed!", "OK");
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

#if UNITY_EDITOR  // Chỉ trong Editor, không ảnh hưởng build game
[CustomEditor(typeof(LevelDataGenerator))]
public class LevelDataGeneratorEditor : Editor
{
    private bool showColorCounts = true;    // Ẩn/hiện phần cấu hình màu
    private bool showGeneratedLevels = false; // Ẩn/hiện phần preview màn chơi

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Vẽ các field mặc định của Inspector
        LevelDataGenerator generator = (LevelDataGenerator)target;

        // === CẤU HÌNH SỐ CHAI THEO MÀU ===
        showColorCounts = EditorGUILayout.Foldout(showColorCounts, 
            "Bottle Color Counts (" + generator.bottleColorCounts?.Count + ")");
        
        if (showColorCounts)
        {
            for (int i = 0; i < generator.bottleColorCounts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Color " + i, GUILayout.Width(60));
                generator.bottleColorCounts[i] = EditorGUILayout.IntField(
                    generator.bottleColorCounts[i], GUILayout.Width(100)); // Chỉnh số chai
                if (GUILayout.Button("X", GUILayout.Width(25)))
                    generator.RemoveColorCount(i); // Xóa màu này
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Color", GUILayout.Height(25)))
                generator.AddColorCount(); // Thêm màu mới
        }

        // === CÁC NÚT THAO TÁC CHÍNH ===
        if (GUILayout.Button("GENERATE LEVELS", GUILayout.Height(35)))
            generator.GenerateLevels();   // Tạo màn chơi theo cấu hình

        if (GUILayout.Button("SAVE TO JSON", GUILayout.Height(35)))
            generator.SaveToJSON();       // Lưu ra file JSON

        if (GUILayout.Button("LOAD FROM JSON", GUILayout.Height(35)))
            generator.LoadFromJSON();     // Tải từ file JSON

        if (GUILayout.Button("CLEAR ALL", GUILayout.Height(35)))
            generator.ClearLevels();      // Xóa toàn bộ

        // === PREVIEW CÁC MÀN ĐÃ TẠO ===
        showGeneratedLevels = EditorGUILayout.Foldout(showGeneratedLevels, 
            "Generated Levels Preview (" + generator.generatedLevels?.levels?.Count + ")");
        
        if (showGeneratedLevels && generator.generatedLevels?.levels != null)
        {
            foreach (LevelData level in generator.generatedLevels.levels)
                EditorGUILayout.LabelField(  // Hiện tên + số chai mỗi màn
                    "Level " + level.levelNumber + " (" + level.bottles.Count + " bottles)", 
                    EditorStyles.boldLabel);
        }
    }
}
#endif
