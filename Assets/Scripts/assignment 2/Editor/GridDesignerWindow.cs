using UnityEngine;
using UnityEditor;

public class GridDesignerWindow : EditorWindow
{
    private GridData currentGridData;
    private Vector2 scrollPosition;
    private bool showMetadata = true;
    private bool showStats = true;

    // Visual settings
    private const float BUTTON_SIZE = 25f;
    private const float BUTTON_SPACING = 2f;
    private readonly Color OBSTACLE_COLOR = new Color(1f, 0.3f, 0.3f);
    private readonly Color FREE_COLOR = new Color(0.3f, 1f, 0.3f);
    private readonly Color BUTTON_BORDER = new Color(0.2f, 0.2f, 0.2f);

    [MenuItem("Tools/Tactics/Grid Designer")]
    public static void ShowWindow()
    {
        GridDesignerWindow window = GetWindow<GridDesignerWindow>();
        window.titleContent = new GUIContent("Grid Designer");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawGridDataSelector();

        if (currentGridData != null)
        {
            DrawMetadataSection();
            DrawStatsSection();
            DrawToolbar();
            DrawGrid();
        }
        else
        {
            DrawCreateNewSection();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField("🏗️ Tactics Grid Designer", headerStyle);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Design obstacle layouts for your tactical grid. Red = Obstacle, Green = Free",
            MessageType.Info
        );

        EditorGUILayout.Space(10);
    }

    private void DrawGridDataSelector()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Grid Data Asset:", GUILayout.Width(100));

        GridData newGridData = (GridData)EditorGUILayout.ObjectField(
            currentGridData,
            typeof(GridData),
            false
        );

        if (newGridData != currentGridData)
        {
            currentGridData = newGridData;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void DrawCreateNewSection()
    {
        EditorGUILayout.Space(20);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("No Grid Data Selected", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Select an existing GridData asset, or create a new one:");
        EditorGUILayout.Space(10);

        if (GUILayout.Button("Create New Grid Data", GUILayout.Height(30)))
        {
            CreateNewGridData();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMetadataSection()
    {
        showMetadata = EditorGUILayout.Foldout(showMetadata, "📋 Grid Information", true);

        if (showMetadata)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUI.BeginChangeCheck();

            currentGridData.gridName = EditorGUILayout.TextField("Grid Name:", currentGridData.gridName);
            currentGridData.description = EditorGUILayout.TextField("Description:", currentGridData.description);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Grid Size: {currentGridData.gridWidth} x {currentGridData.gridHeight}");
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(currentGridData);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);
    }

    private void DrawStatsSection()
    {
        showStats = EditorGUILayout.Foldout(showStats, "📊 Statistics", true);

        if (showStats)
        {
            EditorGUILayout.BeginVertical("box");

            int obstacleCount = currentGridData.GetObstacleCount();
            int totalTiles = currentGridData.gridWidth * currentGridData.gridHeight;
            int freeTiles = totalTiles - obstacleCount;
            float obstaclePercentage = (float)obstacleCount / totalTiles * 100f;

            EditorGUILayout.LabelField($"Total Tiles: {totalTiles}");
            EditorGUILayout.LabelField($"Obstacles: {obstacleCount} ({obstaclePercentage:F1}%)");
            EditorGUILayout.LabelField($"Free Tiles: {freeTiles}");

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal("toolbar");

        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton))
        {
            if (EditorUtility.DisplayDialog("Clear All Obstacles",
                "Are you sure you want to clear all obstacles?", "Yes", "Cancel"))
            {
                currentGridData.ClearAllObstacles();
            }
        }

        if (GUILayout.Button("Fill All", EditorStyles.toolbarButton))
        {
            if (EditorUtility.DisplayDialog("Fill All Obstacles",
                "Are you sure you want to fill all tiles with obstacles?", "Yes", "Cancel"))
            {
                currentGridData.FillAllObstacles();
            }
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Save Asset", EditorStyles.toolbarButton))
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Saved", "Grid data has been saved!", "OK");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void DrawGrid()
    {
        EditorGUILayout.LabelField("🎯 Grid Layout:", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.BeginVertical();

        for (int y = currentGridData.gridHeight - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();

            // Row label
            EditorGUILayout.LabelField($"{y}", GUILayout.Width(20));

            for (int x = 0; x < currentGridData.gridWidth; x++)
            {
                DrawGridButton(x, y);
            }

            EditorGUILayout.EndHorizontal();
        }

        // Column labels
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(20)); // Offset for row labels

        for (int x = 0; x < currentGridData.gridWidth; x++)
        {
            EditorGUILayout.LabelField($"{x}", GUILayout.Width(BUTTON_SIZE + BUTTON_SPACING));
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawGridButton(int x, int y)
    {
        bool isObstacle = currentGridData.IsObstacle(x, y);

        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = isObstacle ? OBSTACLE_COLOR : FREE_COLOR;

        string buttonText = isObstacle ? "█" : "○";

        if (GUILayout.Button(buttonText, GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
        {
            currentGridData.SetObstacle(x, y, !isObstacle);
        }

        GUI.backgroundColor = originalColor;
    }

    private void CreateNewGridData()
    {
        GridData newGridData = CreateInstance<GridData>();
        newGridData.gridName = "New Grid Layout";

        string path = EditorUtility.SaveFilePanelInProject(
            "Create New Grid Data",
            "NewGridData",
            "asset",
            "Choose where to save the new Grid Data asset"
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newGridData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            currentGridData = newGridData;

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newGridData;
        }
    }
}
