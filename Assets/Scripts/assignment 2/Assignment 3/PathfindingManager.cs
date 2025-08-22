using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PathfindingManager : MonoBehaviour
{
    [Header("Pathfinding Settings")]
    public bool allowDiagonalMovement = false;
    public float diagonalCost = 1.414f; // sqrt(2)
    public float straightCost = 1f;

    [Header("Debug Visualization")]
    public bool showDebugPath = true;
    public Color pathColor = Color.yellow;
    public float debugPathDuration = 2f;

    // Singleton pattern
    public static PathfindingManager Instance { get; private set; }

    // Initialization state
    private bool isInitialized = false;

    // A* algorithm data structures
    private class PathNode
    {
        public Vector2Int position;
        public float gCost; // Distance from start
        public float hCost; // Distance to target (heuristic)
        public float fCost => gCost + hCost; // Total cost
        public PathNode parent;
        public bool isWalkable;

        public PathNode(Vector2Int pos, bool walkable)
        {
            position = pos;
            isWalkable = walkable;
            gCost = float.MaxValue;
            hCost = 0;
            parent = null;
        }
    }

    private PathNode[,] pathGrid;
    private List<PathNode> openList;
    private HashSet<PathNode> closedSet;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Don't initialize path grid here - wait for GridManager
    }

    private void Start()
    {
        StartCoroutine(WaitForGridManagerAndInitialize());
    }

    private IEnumerator WaitForGridManagerAndInitialize()
    {
        // Wait until GridManager is properly initialized
        while (!IsGridManagerReady())
        {
            yield return null; // Wait one frame
        }

        // Small additional delay to ensure everything is set up
        yield return new WaitForEndOfFrame();

        InitializePathGrid();
        isInitialized = true;

        Debug.Log("PathfindingManager: Initialized successfully");
    }

    private bool IsGridManagerReady()
    {
        if (GridManager.Instance == null)
        {
            return false;
        }

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

    private void InitializePathGrid()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("PathfindingManager: GridManager not found!");
            return;
        }

        int width = GridManager.Instance.gridWidth;
        int height = GridManager.Instance.gridHeight;

        pathGrid = new PathNode[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isWalkable = IsPositionWalkable(x, y);
                pathGrid[x, y] = new PathNode(new Vector2Int(x, y), isWalkable);
            }
        }

        Debug.Log($"PathfindingManager: Initialized {width}x{height} pathfinding grid");
    }

    private bool IsPositionWalkable(int x, int y)
    {
        if (!GridManager.Instance.IsValidGridPosition(x, y))
            return false;

        // Check if tile exists and is walkable
        Tile tile = GridManager.Instance.GetTile(x, y);
        if (tile == null || !tile.isWalkable)
            return false;

        // Check for obstacles
        if (ObstacleManager.Instance != null && ObstacleManager.Instance.IsObstacleAtPosition(x, y))
            return false;

        return true;
    }

    public List<Vector2Int> FindPath(Vector2Int startPos, Vector2Int targetPos)
    {
        // Wait for initialization if not ready
        if (!isInitialized)
        {
            Debug.LogWarning("PathfindingManager: Not initialized yet, cannot find path");
            return new List<Vector2Int>();
        }

        // Validate positions
        if (!IsValidPosition(startPos) || !IsValidPosition(targetPos))
        {
            Debug.LogWarning($"PathfindingManager: Invalid start ({startPos}) or target ({targetPos}) position");
            return new List<Vector2Int>();
        }

        // Check if target is walkable
        if (!IsPositionWalkable(targetPos.x, targetPos.y))
        {
            Debug.LogWarning($"PathfindingManager: Target position ({targetPos}) is not walkable");
            return new List<Vector2Int>();
        }

        // If already at target, return empty path
        if (startPos == targetPos)
        {
            return new List<Vector2Int>();
        }

        // Refresh walkability data
        RefreshWalkabilityData();

        // Run A* algorithm
        List<Vector2Int> path = AStar(startPos, targetPos);

        // Debug visualization
        if (showDebugPath && path.Count > 0)
        {
            VisualizePathDebug(path);
        }

        return path;
    }

    private List<Vector2Int> AStar(Vector2Int startPos, Vector2Int targetPos)
    {
        PathNode startNode = pathGrid[startPos.x, startPos.y];
        PathNode targetNode = pathGrid[targetPos.x, targetPos.y];

        openList = new List<PathNode> { startNode };
        closedSet = new HashSet<PathNode>();

        // Reset all nodes
        ResetPathNodes();

        startNode.gCost = 0;
        startNode.hCost = CalculateDistance(startPos, targetPos);

        while (openList.Count > 0)
        {
            // Get node with lowest fCost
            PathNode currentNode = openList.OrderBy(x => x.fCost).ThenBy(x => x.hCost).First();

            openList.Remove(currentNode);
            closedSet.Add(currentNode);

            // Found target
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            // Check neighbors
            foreach (PathNode neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.isWalkable || closedSet.Contains(neighbor))
                    continue;

                float movementCost = CalculateMovementCost(currentNode.position, neighbor.position);
                float newGCost = currentNode.gCost + movementCost;

                if (newGCost < neighbor.gCost || !openList.Contains(neighbor))
                {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = CalculateDistance(neighbor.position, targetPos);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }

        // No path found
        Debug.LogWarning($"PathfindingManager: No path found from {startPos} to {targetPos}");
        return new List<Vector2Int>();
    }

    private void ResetPathNodes()
    {
        for (int x = 0; x < pathGrid.GetLength(0); x++)
        {
            for (int y = 0; y < pathGrid.GetLength(1); y++)
            {
                pathGrid[x, y].gCost = float.MaxValue;
                pathGrid[x, y].hCost = 0;
                pathGrid[x, y].parent = null;
            }
        }
    }

    private List<PathNode> GetNeighbors(PathNode node)
    {
        List<PathNode> neighbors = new List<PathNode>();
        Vector2Int pos = node.position;

        // Cardinal directions
        Vector2Int[] cardinalDirections = {
            new Vector2Int(0, 1),   // Up
            new Vector2Int(1, 0),   // Right
            new Vector2Int(0, -1),  // Down
            new Vector2Int(-1, 0)   // Left
        };

        // Add cardinal neighbors
        foreach (Vector2Int dir in cardinalDirections)
        {
            Vector2Int newPos = pos + dir;
            if (IsValidPosition(newPos))
            {
                neighbors.Add(pathGrid[newPos.x, newPos.y]);
            }
        }

        // Add diagonal neighbors if enabled
        if (allowDiagonalMovement)
        {
            Vector2Int[] diagonalDirections = {
                new Vector2Int(1, 1),   // Up-Right
                new Vector2Int(1, -1),  // Down-Right
                new Vector2Int(-1, -1), // Down-Left
                new Vector2Int(-1, 1)   // Up-Left
            };

            foreach (Vector2Int dir in diagonalDirections)
            {
                Vector2Int newPos = pos + dir;
                if (IsValidPosition(newPos))
                {
                    // Check for diagonal movement blocking
                    if (IsDiagonalMovementValid(pos, newPos))
                    {
                        neighbors.Add(pathGrid[newPos.x, newPos.y]);
                    }
                }
            }
        }

        return neighbors;
    }

    private bool IsDiagonalMovementValid(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;

        // Check adjacent tiles for blocking
        Vector2Int horizontal = from + new Vector2Int(diff.x, 0);
        Vector2Int vertical = from + new Vector2Int(0, diff.y);

        return IsValidPosition(horizontal) && pathGrid[horizontal.x, horizontal.y].isWalkable &&
               IsValidPosition(vertical) && pathGrid[vertical.x, vertical.y].isWalkable;
    }

    private float CalculateMovementCost(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;

        // Diagonal movement
        if (Mathf.Abs(diff.x) == 1 && Mathf.Abs(diff.y) == 1)
            return diagonalCost;

        // Straight movement
        return straightCost;
    }

    private float CalculateDistance(Vector2Int a, Vector2Int b)
    {
        Vector2Int diff = new Vector2Int(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        if (allowDiagonalMovement)
        {
            // Diagonal distance (Octile distance)
            int min = Mathf.Min(diff.x, diff.y);
            int max = Mathf.Max(diff.x, diff.y);
            return min * diagonalCost + (max - min) * straightCost;
        }
        else
        {
            // Manhattan distance
            return (diff.x + diff.y) * straightCost;
        }
    }

    private List<Vector2Int> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    private bool IsValidPosition(Vector2Int pos)
    {
        return pathGrid != null &&
               pos.x >= 0 && pos.x < pathGrid.GetLength(0) &&
               pos.y >= 0 && pos.y < pathGrid.GetLength(1);
    }

    private void RefreshWalkabilityData()
    {
        if (!isInitialized || pathGrid == null) return;

        for (int x = 0; x < pathGrid.GetLength(0); x++)
        {
            for (int y = 0; y < pathGrid.GetLength(1); y++)
            {
                pathGrid[x, y].isWalkable = IsPositionWalkable(x, y);
            }
        }
    }

    private void VisualizePathDebug(List<Vector2Int> path)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 start = GridManager.Instance.GridToWorldPosition(path[i].x, path[i].y) + Vector3.up * 0.1f;
            Vector3 end = GridManager.Instance.GridToWorldPosition(path[i + 1].x, path[i + 1].y) + Vector3.up * 0.1f;

            Debug.DrawLine(start, end, pathColor, debugPathDuration);
        }
    }

    public void RefreshPathfindingData()
    {
        if (isInitialized)
        {
            RefreshWalkabilityData();
        }
    }

    public bool IsPathfindingReady()
    {
        return isInitialized;
    }
}
