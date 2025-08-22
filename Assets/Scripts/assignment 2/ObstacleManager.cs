using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ObstacleManager : MonoBehaviour
{
    [Header("Obstacle Settings")]
    public GridData gridData;
    public GameObject obstaclePrefab;

    [Header("Obstacle Appearance")]
    public Material obstacleMaterial;
    public float obstacleHeight = 0.5f;
    public float obstacleScale = 0.8f;

    [Header("Runtime Settings")]
    public bool spawnOnStart = true;
    public bool updateTileWalkability = true;

    // Singleton pattern
    public static ObstacleManager Instance { get; private set; }

    // Runtime data
    private Dictionary<Vector2Int, GameObject> spawnedObstacles;
    private Transform obstacleParent;
    private bool isInitialized = false;

    // Events
    public System.Action<Vector2Int> OnObstacleSpawned;
    public System.Action<Vector2Int> OnObstacleRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        spawnedObstacles = new Dictionary<Vector2Int, GameObject>();
    }

    private void Start()
    {
        CreateObstacleParent();

        if (spawnOnStart && gridData != null)
        {
            // Wait for GridManager to initialize before spawning obstacles
            StartCoroutine(WaitForGridManagerAndSpawn());
        }
    }

    private IEnumerator WaitForGridManagerAndSpawn()
    {
        // Wait until GridManager is properly initialized
        while (GridManager.Instance == null || !IsGridManagerReady())
        {
            yield return null; // Wait one frame
        }

        // Small additional delay to ensure everything is set up
        yield return new WaitForEndOfFrame();

        SpawnAllObstacles();
        isInitialized = true;
    }

    private bool IsGridManagerReady()
    {
        if (GridManager.Instance == null) return false;

        // Test if we can get a tile (this means the grid is generated)
        try
        {
            Tile testTile = GridManager.Instance.GetTile(0, 0);
            return testTile != null;
        }
        catch
        {
            return false;
        }
    }

    private void CreateObstacleParent()
    {
        obstacleParent = new GameObject("Obstacles").transform;
        obstacleParent.SetParent(transform);
        obstacleParent.localPosition = Vector3.zero;
    }

    public void SpawnAllObstacles()
    {
        if (gridData == null)
        {
            Debug.LogWarning("ObstacleManager: No GridData assigned!");
            return;
        }

        if (!IsGridManagerReady())
        {
            Debug.LogWarning("ObstacleManager: GridManager not ready yet!");
            return;
        }

        ClearAllObstacles();

        Vector2Int[] obstaclePositions = gridData.GetObstaclePositions();

        foreach (Vector2Int pos in obstaclePositions)
        {
            SpawnObstacleAtPosition(pos.x, pos.y);
        }

        Debug.Log($"ObstacleManager: Spawned {obstaclePositions.Length} obstacles");
    }

    public void SpawnObstacleAtPosition(int x, int y)
    {
        Vector2Int gridPos = new Vector2Int(x, y);

        if (spawnedObstacles.ContainsKey(gridPos))
        {
            Debug.LogWarning($"Obstacle already exists at ({x}, {y})");
            return;
        }

        if (!IsGridManagerReady())
        {
            Debug.LogError("GridManager not ready for obstacle spawning!");
            return;
        }

        Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(x, y);
        worldPosition.y += obstacleHeight;

        GameObject obstacle = CreateObstacle(worldPosition);
        spawnedObstacles[gridPos] = obstacle;

        // Update tile walkability
        if (updateTileWalkability)
        {
            Tile tile = GridManager.Instance.GetTile(x, y);
            if (tile != null)
            {
                tile.SetWalkable(false);
                tile.SetOccupied(true);
            }
        }

        OnObstacleSpawned?.Invoke(gridPos);
    }

    public void RemoveObstacleAtPosition(int x, int y)
    {
        Vector2Int gridPos = new Vector2Int(x, y);

        if (spawnedObstacles.TryGetValue(gridPos, out GameObject obstacle))
        {
            DestroyImmediate(obstacle);
            spawnedObstacles.Remove(gridPos);

            // Update tile walkability (only if GridManager is ready)
            if (updateTileWalkability && IsGridManagerReady())
            {
                Tile tile = GridManager.Instance.GetTile(x, y);
                if (tile != null)
                {
                    tile.SetWalkable(true);
                    tile.SetOccupied(false);
                }
            }

            OnObstacleRemoved?.Invoke(gridPos);
        }
    }

    public void ClearAllObstacles()
    {
        // Clear spawned obstacles first
        foreach (var kvp in spawnedObstacles)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
            }
        }

        spawnedObstacles.Clear();

        // Reset all tile walkability (only if GridManager is ready)
        if (updateTileWalkability && IsGridManagerReady())
        {
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    Tile tile = GridManager.Instance.GetTile(x, y);
                    if (tile != null)
                    {
                        tile.SetWalkable(true);
                        tile.SetOccupied(false);
                    }
                }
            }
        }
    }

    private GameObject CreateObstacle(Vector3 position)
    {
        GameObject obstacle;

        if (obstaclePrefab != null)
        {
            obstacle = Instantiate(obstaclePrefab, position, Quaternion.identity, obstacleParent);
        }
        else
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obstacle.transform.position = position;
            obstacle.transform.SetParent(obstacleParent);
            obstacle.name = "Obstacle";

            if (obstacleMaterial != null)
            {
                obstacle.GetComponent<Renderer>().material = obstacleMaterial;
            }
            else
            {
                // Create default red material
                Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                defaultMat.color = Color.red;
                obstacle.GetComponent<Renderer>().material = defaultMat;
            }
        }

        obstacle.transform.localScale = Vector3.one * obstacleScale;

        return obstacle;
    }

    public bool IsObstacleAtPosition(int x, int y)
    {
        return spawnedObstacles.ContainsKey(new Vector2Int(x, y));
    }

    public GameObject GetObstacleAtPosition(int x, int y)
    {
        spawnedObstacles.TryGetValue(new Vector2Int(x, y), out GameObject obstacle);
        return obstacle;
    }

    public void RefreshFromGridData()
    {
        if (gridData != null && isInitialized)
        {
            SpawnAllObstacles();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && spawnedObstacles != null && isInitialized)
        {
            RefreshFromGridData();
        }
    }
}
