using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float tileSize = 1f;
    public float tileSpacing = 0.1f;

    [Header("Tile Prefab")]
    public GameObject tilePrefab;

    [Header("Materials")]
    public Material defaultTileMaterial;
    public Material hoverTileMaterial;

    [Header("Grid Positioning")]
    public Vector3 gridOffset = Vector3.zero;

    // Singleton pattern
    public static GridManager Instance { get; private set; }

    // Grid storage
    private Tile[,] grid;
    private Transform gridParent;

    // Events (renamed to avoid conflict)
    public System.Action<Tile> TileHoveredEvent;
    public System.Action<Tile> TileUnhoveredEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        CreateGridParent();
        GenerateGrid();
        SetupEventHandlers();
    }

    private void CreateGridParent()
    {
        gridParent = new GameObject("Grid").transform;
        gridParent.SetParent(transform);
        gridParent.localPosition = Vector3.zero;
    }

    private void GenerateGrid()
    {
        grid = new Tile[gridWidth, gridHeight];

        Vector3 gridCenter = new Vector3(
            (gridWidth - 1) * (tileSize + tileSpacing) * 0.5f,
            0f,
            (gridHeight - 1) * (tileSize + tileSpacing) * 0.5f
        );

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 position = new Vector3(
                    x * (tileSize + tileSpacing) - gridCenter.x,
                    0f,
                    y * (tileSize + tileSpacing) - gridCenter.z
                ) + gridOffset;

                GameObject tileObject = CreateTile(position);
                Tile tile = tileObject.GetComponent<Tile>();

                tile.Initialize(x, y, position);
                tile.defaultMaterial = defaultTileMaterial;
                tile.hoverMaterial = hoverTileMaterial;

                grid[x, y] = tile;
            }
        }

        Debug.Log($"Grid generated: {gridWidth}x{gridHeight} tiles");
    }

    private GameObject CreateTile(Vector3 position)
    {
        GameObject tile;

        if (tilePrefab != null)
        {
            tile = Instantiate(tilePrefab, position, Quaternion.identity, gridParent);
        }
        else
        {
            tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.position = position;
            tile.transform.localScale = Vector3.one * tileSize;
            tile.transform.SetParent(gridParent);
        }

        if (!tile.GetComponent<Tile>())
            tile.AddComponent<Tile>();

        if (!tile.GetComponent<Collider>())
            tile.AddComponent<BoxCollider>();

        return tile;
    }

    private void SetupEventHandlers()
    {
        TileHoveredEvent += HandleTileHovered;
        TileUnhoveredEvent += HandleTileUnhovered;
    }

    private void HandleTileHovered(Tile tile)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTileInfo(tile);
        }
    }

    private void HandleTileUnhovered(Tile tile)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearTileInfo();
        }
    }

    public Tile GetTile(int x, int y)
    {
        if (IsValidGridPosition(x, y))
            return grid[x, y];
        return null;
    }

    public Tile GetTile(Vector2Int gridPos)
    {
        return GetTile(gridPos.x, gridPos.y);
    }

    public bool IsValidGridPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    public Vector3 GridToWorldPosition(int x, int y)
    {
        if (IsValidGridPosition(x, y))
            return grid[x, y].worldPosition;
        return Vector3.zero;
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3 gridCenter = new Vector3(
            (gridWidth - 1) * (tileSize + tileSpacing) * 0.5f,
            0f,
            (gridHeight - 1) * (tileSize + tileSpacing) * 0.5f
        );

        Vector3 localPos = worldPos - gridOffset + gridCenter;

        int x = Mathf.RoundToInt(localPos.x / (tileSize + tileSpacing));
        int y = Mathf.RoundToInt(localPos.z / (tileSize + tileSpacing));

        return new Vector2Int(x, y);
    }

    public List<Tile> GetNeighbors(int x, int y, bool includeDiagonal = false)
    {
        List<Tile> neighbors = new List<Tile>();

        Vector2Int[] directions = includeDiagonal
            ? new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                                new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1) }
            : new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int dir in directions)
        {
            int newX = x + dir.x;
            int newY = y + dir.y;

            if (IsValidGridPosition(newX, newY))
                neighbors.Add(grid[newX, newY]);
        }

        return neighbors;
    }

    // Public methods for tiles to call (renamed to avoid conflict)
    public void OnTileHovered(Tile tile)
    {
        TileHoveredEvent?.Invoke(tile);
    }

    public void OnTileUnhovered(Tile tile)
    {
        TileUnhoveredEvent?.Invoke(tile);
    }
    public void RefreshTileStates()
    {
        if (ObstacleManager.Instance != null)
        {
            ObstacleManager.Instance.RefreshFromGridData();
        }
    }

}
