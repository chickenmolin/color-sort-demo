using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private string jsonFilePath = "Assets/Data/levels.json";
    [SerializeField] private GameObject bottlePrefab;
    [SerializeField] private Color[] bottleColors = new Color[4];
    [SerializeField] private float bottleSpacing = 2f;
    [SerializeField] private int bottlesPerRow = 3;
    [SerializeField] private float rowHeight = 3f;
    [SerializeField] private Vector3 startPosition = Vector3.zero;

    private LevelListData levelData;
    private List<GameObject> currentLevelBottles = new List<GameObject>();

    private void Start()
    {
        LoadLevelsFromJSON();
    }

    public void LoadLevelsFromJSON()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, jsonFilePath);

        // Thử load từ persistentDataPath trước
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Application.streamingAssetsPath, jsonFilePath);
        }

        if (File.Exists(fullPath))
        {
            string json = File.ReadAllText(fullPath);
            levelData = JsonUtility.FromJson<LevelListData>(json);
            Debug.Log("Loaded " + levelData.levels.Count + " levels from JSON");
        }
        else
        {
            Debug.LogError("Level file not found at: " + fullPath);
        }
    }

    public void RenderLevel(int levelNumber)
    {
        ClearCurrentLevel();

        // Tìm level
        LevelData targetLevel = null;
        foreach (LevelData level in levelData.levels)
        {
            if (level.levelNumber == levelNumber)
            {
                targetLevel = level;
                break;
            }
        }

        if (targetLevel == null)
        {
            Debug.LogError("Level " + levelNumber + " not found!");
            return;
        }

        // Render bottles
        for (int i = 0; i < targetLevel.bottles.Count; i++)
        {
            BottleData bottleData = targetLevel.bottles[i];

            int row = i / bottlesPerRow;
            int col = i % bottlesPerRow;
            Vector3 position = startPosition + new Vector3(col * bottleSpacing, -row * rowHeight, 0);

            GameObject bottleGO = Instantiate(bottlePrefab, position, Quaternion.identity);
            bottleGO.name = "Bottle_" + i;
            bottleGO.tag = "bottle";

            BottleController bc = bottleGO.GetComponent<BottleController>();
            if (bc != null)
            {
                bc.numberOfColorsInBottle = bottleData.colorCount;

                // Set colors
                System.Reflection.FieldInfo colorField = typeof(BottleController)
                    .GetField("bottleColors",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (colorField != null)
                {
                    Color[] colors = new Color[4];
                    for (int c = 0; c < bottleData.colorCount; c++)
                    {
                        int colorIdx = bottleData.colorIndices[c];
                        colors[c] = bottleColors[colorIdx];
                    }
                    colorField.SetValue(bc, colors);
                }

                bc.UpdateTopColorValue();

                // Update shader
                System.Reflection.FieldInfo srField = typeof(BottleController)
                    .GetField("bottleMaskSR",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (srField != null)
                {
                    SpriteRenderer sr = (SpriteRenderer)srField.GetValue(bc);
                    if (sr != null)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            int colorIdx = c < bottleData.colorCount ? bottleData.colorIndices[c] : 0;
                            sr.material.SetColor("_Color0" + (c + 1), bottleColors[colorIdx]);
                        }
                    }
                }
            }

            currentLevelBottles.Add(bottleGO);
        }

        Debug.Log("Rendered Level " + levelNumber + " with " + targetLevel.bottles.Count + " bottles");
    }

    public void ClearCurrentLevel()
    {
        foreach (GameObject bottle in currentLevelBottles)
        {
            Destroy(bottle);
        }
        currentLevelBottles.Clear();
    }

    public int GetTotalLevels()
    {
        return levelData?.levels?.Count ?? 0;
    }

    public LevelData GetLevelData(int levelNumber)
    {
        foreach (LevelData level in levelData.levels)
        {
            if (level.levelNumber == levelNumber)
                return level;
        }
        return null;
    }
}