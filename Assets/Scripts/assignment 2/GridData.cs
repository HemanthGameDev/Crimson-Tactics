using UnityEngine;

[CreateAssetMenu(fileName = "NewGridData", menuName = "Tactics/Grid Data")]
public class GridData : ScriptableObject
{
    [Header("Grid Configuration")]
    public int gridWidth = 10;
    public int gridHeight = 10;

    [Header("Obstacle Data")]
    [SerializeField]
    private bool[] obstacles = new bool[100];

    [Header("Metadata")]
    public string gridName = "Untitled Grid";
    public string description = "Grid layout for tactical scenarios";

    private void OnValidate()
    {
        int requiredSize = gridWidth * gridHeight;
        if (obstacles.Length != requiredSize)
        {
            System.Array.Resize(ref obstacles, requiredSize);
        }
    }

    public bool IsObstacle(int x, int y)
    {
        if (!IsValidPosition(x, y)) return false;
        return obstacles[GetIndex(x, y)];
    }

    public void SetObstacle(int x, int y, bool isObstacle)
    {
        if (!IsValidPosition(x, y)) return;
        obstacles[GetIndex(x, y)] = isObstacle;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    private int GetIndex(int x, int y)
    {
        return y * gridWidth + x;
    }

    public void ClearAllObstacles()
    {
        for (int i = 0; i < obstacles.Length; i++)
        {
            obstacles[i] = false;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void FillAllObstacles()
    {
        for (int i = 0; i < obstacles.Length; i++)
        {
            obstacles[i] = true;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public int GetObstacleCount()
    {
        int count = 0;
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i]) count++;
        }
        return count;
    }

    public Vector2Int[] GetObstaclePositions()
    {
        var obstacleList = new System.Collections.Generic.List<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (IsObstacle(x, y))
                {
                    obstacleList.Add(new Vector2Int(x, y));
                }
            }
        }

        return obstacleList.ToArray();
    }
}
