using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI : MonoBehaviour, IAI
{
    [Header("AI Settings")]
    public float moveSpeed = 2f;
    public int priority = 1;
    public float rotationSpeed = 10f;

    [Header("Behavior Settings")]
    public float thinkingDelay = 0.5f;

    [Header("Visual Settings")]
    public Material enemyMaterial;
    public float bounceHeight = 0.1f;
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // IAI Interface Properties
    public bool IsPerformingAction { get; private set; }
    public Vector2Int CurrentPosition { get; private set; }
    public System.Action OnTurnComplete { get; set; }

    // Internal state
    private bool isInitialized = false;
    private PlayerController targetPlayer;
    private bool isMoving = false;
    private Vector3 cachedBasePosition;
    private bool needsPositionUpdate = true;

    // Components
    private Renderer enemyRenderer;

    private void Awake()
    {
        enemyRenderer = GetComponent<Renderer>();

        if (enemyMaterial != null && enemyRenderer != null)
        {
            enemyRenderer.material = enemyMaterial;
        }
    }

    private void Start()
    {
        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        // Wait for all managers to be ready
        while (GridManager.Instance == null ||
               PathfindingManager.Instance == null ||
               TurnManager.Instance == null ||
               !PathfindingManager.Instance.IsPathfindingReady())
        {
            yield return null;
        }

        // Find the player
        targetPlayer = FindObjectOfType<PlayerController>();
        if (targetPlayer == null)
        {
            Debug.LogError("EnemyAI: No PlayerController found!");
            yield break;
        }

        yield return new WaitForEndOfFrame();

        // Auto-initialize at a random valid position
        if (!isInitialized)
        {
            Vector2Int startPos = FindValidSpawnPosition();
            if (startPos != Vector2Int.one * -1)
            {
                Initialize(startPos);
            }
        }

        // Register with turn manager
        TurnManager.Instance.RegisterAI(this);

        Debug.Log($"EnemyAI: Initialized and ready at position {CurrentPosition}");
    }

    public void Initialize(Vector2Int startPosition)
    {
        if (!GridManager.Instance.IsValidGridPosition(startPosition.x, startPosition.y))
        {
            Debug.LogError($"EnemyAI: Invalid starting position {startPosition}");
            return;
        }

        CurrentPosition = startPosition;
        Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(startPosition.x, startPosition.y);
        worldPosition.y = 0.5f;

        transform.position = worldPosition;
        UpdateTileOccupancy(CurrentPosition, true);

        cachedBasePosition = worldPosition;
        needsPositionUpdate = false;
        isInitialized = true;

        Debug.Log($"EnemyAI: Positioned at {CurrentPosition}");
    }

    private Vector2Int FindValidSpawnPosition()
    {
        List<Vector2Int> validPositions = new List<Vector2Int>();

        for (int x = 0; x < GridManager.Instance.gridWidth; x++)
        {
            for (int y = 0; y < GridManager.Instance.gridHeight; y++)
            {
                Tile tile = GridManager.Instance.GetTile(x, y);
                if (tile != null && tile.isWalkable && !tile.isOccupied)
                {
                    validPositions.Add(new Vector2Int(x, y));
                }
            }
        }

        if (validPositions.Count > 0)
        {
            return validPositions[Random.Range(0, validPositions.Count)];
        }

        return Vector2Int.one * -1;
    }

    private void Update()
    {
        if (!isInitialized || IsPerformingAction) return;

        HandleVisualEffects();
    }

    public void TakeTurn()
    {
        if (IsPerformingAction || !isInitialized || isMoving)
        {
            Debug.LogWarning("EnemyAI: Cannot take turn - already performing action or not initialized");
            return;
        }

        Debug.Log($"EnemyAI: Starting turn from position {CurrentPosition}");
        StartCoroutine(ExecuteTurn());
    }

    private IEnumerator ExecuteTurn()
    {
        IsPerformingAction = true;
        isMoving = true;

        Debug.Log("EnemyAI: Thinking...");
        yield return new WaitForSeconds(thinkingDelay);

        Vector2Int targetPosition = CalculateOptimalMove();

        Debug.Log($"EnemyAI: Calculated target position: {targetPosition}");

        if (targetPosition != CurrentPosition)
        {
            Debug.Log($"EnemyAI: Moving from {CurrentPosition} to {targetPosition}");
            yield return StartCoroutine(MoveToPositionCoroutine(targetPosition));
        }
        else
        {
            Debug.Log("EnemyAI: Staying in current position");
            yield return new WaitForSeconds(0.5f); // Small delay even when not moving
        }

        IsPerformingAction = false;
        isMoving = false;

        Debug.Log("EnemyAI: Turn completed");
        OnTurnComplete?.Invoke();
    }

    private Vector2Int CalculateOptimalMove()
    {
        if (targetPlayer == null)
        {
            Debug.LogWarning("EnemyAI: No target player found!");
            return CurrentPosition;
        }

        Vector2Int playerPosition = targetPlayer.currentGridPosition;
        Debug.Log($"EnemyAI: Player is at {playerPosition}");

        // Check if already adjacent to player
        if (IsAdjacentToPlayer(CurrentPosition, playerPosition))
        {
            Debug.Log($"EnemyAI: Already adjacent to player, staying at {CurrentPosition}");
            return CurrentPosition;
        }

        // Get all adjacent positions to the player
        List<Vector2Int> adjacentToPlayer = GetAdjacentPositions(playerPosition);
        Debug.Log($"EnemyAI: Adjacent positions to player: {string.Join(", ", adjacentToPlayer)}");

        // Filter walkable and unoccupied positions
        List<Vector2Int> validAdjacent = new List<Vector2Int>();

        foreach (Vector2Int pos in adjacentToPlayer)
        {
            if (GridManager.Instance.IsValidGridPosition(pos.x, pos.y))
            {
                Tile tile = GridManager.Instance.GetTile(pos.x, pos.y);
                if (tile != null && tile.isWalkable && !tile.isOccupied)
                {
                    validAdjacent.Add(pos);
                }
            }
        }

        Debug.Log($"EnemyAI: Valid adjacent positions: {string.Join(", ", validAdjacent)}");

        if (validAdjacent.Count == 0)
        {
            Debug.LogWarning("EnemyAI: No valid adjacent positions, trying to get closer");

            // If no adjacent positions available, just try to get closer
            Vector2Int direction = new Vector2Int(
                playerPosition.x > CurrentPosition.x ? 1 : playerPosition.x < CurrentPosition.x ? -1 : 0,
                playerPosition.y > CurrentPosition.y ? 1 : playerPosition.y < CurrentPosition.y ? -1 : 0
            );

            Vector2Int nextPos = CurrentPosition + direction;

            if (GridManager.Instance.IsValidGridPosition(nextPos.x, nextPos.y))
            {
                Tile tile = GridManager.Instance.GetTile(nextPos.x, nextPos.y);
                if (tile != null && tile.isWalkable && !tile.isOccupied)
                {
                    return nextPos;
                }
            }

            return CurrentPosition;
        }

        // Find the closest valid adjacent position to our current position
        Vector2Int bestTarget = validAdjacent[0];
        float shortestDistance = Vector2Int.Distance(CurrentPosition, bestTarget);

        foreach (Vector2Int pos in validAdjacent)
        {
            float distance = Vector2Int.Distance(CurrentPosition, pos);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                bestTarget = pos;
            }
        }

        Debug.Log($"EnemyAI: Best target: {bestTarget}");

        // Find path to the target
        List<Vector2Int> path = PathfindingManager.Instance.FindPath(CurrentPosition, bestTarget);
        Debug.Log($"EnemyAI: Path length: {path.Count}");

        if (path.Count > 0)
        {
            // Move one step along the path
            Vector2Int nextStep = path[0];
            Debug.Log($"EnemyAI: Next step: {nextStep}");
            return nextStep;
        }

        Debug.LogWarning($"EnemyAI: No path found to target {bestTarget}");
        return CurrentPosition;
    }

    private List<Vector2Int> GetAdjacentPositions(Vector2Int center)
    {
        return new List<Vector2Int>
        {
            center + Vector2Int.up,     // North
            center + Vector2Int.right,  // East
            center + Vector2Int.down,   // South
            center + Vector2Int.left    // West
        };
    }

    private bool IsAdjacentToPlayer(Vector2Int position, Vector2Int playerPosition)
    {
        return GetAdjacentPositions(playerPosition).Contains(position);
    }

    public void MoveToTarget(Vector2Int targetPosition)
    {
        if (IsPerformingAction) return;

        StartCoroutine(MoveToPositionCoroutine(targetPosition));
    }

    private IEnumerator MoveToPositionCoroutine(Vector2Int targetGridPos)
    {
        Debug.Log($"EnemyAI: Starting movement to {targetGridPos}");

        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = GridManager.Instance.GridToWorldPosition(targetGridPos.x, targetGridPos.y);
        targetWorldPos.y = 0.5f;

        // Update grid occupancy
        UpdateTileOccupancy(CurrentPosition, false);
        UpdateTileOccupancy(targetGridPos, true);

        CurrentPosition = targetGridPos;

        float elapsed = 0;
        float duration = 1f / moveSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float curvedProgress = movementCurve.Evaluate(progress);

            Vector3 currentPos = Vector3.Lerp(startWorldPos, targetWorldPos, curvedProgress);
            currentPos.y = 0.5f + (Mathf.Sin(progress * Mathf.PI) * bounceHeight);

            transform.position = currentPos;

            // Rotate towards movement direction
            Vector3 direction = (targetWorldPos - startWorldPos).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        transform.position = targetWorldPos;
        needsPositionUpdate = true;

        Debug.Log($"EnemyAI: Movement completed, now at {CurrentPosition}");
    }

    private void UpdateTileOccupancy(Vector2Int gridPos, bool occupied)
    {
        Tile tile = GridManager.Instance.GetTile(gridPos.x, gridPos.y);
        if (tile != null)
        {
            tile.SetOccupied(occupied);
        }
    }

    private void HandleVisualEffects()
    {
        if (needsPositionUpdate)
        {
            cachedBasePosition = GridManager.Instance.GridToWorldPosition(CurrentPosition.x, CurrentPosition.y);
            cachedBasePosition.y = 0.5f;
            needsPositionUpdate = false;
        }

        // Subtle idle animation
        float bounce = Mathf.Sin(Time.time * 1.5f) * bounceHeight * 0.3f;
        transform.position = new Vector3(cachedBasePosition.x, cachedBasePosition.y + bounce, cachedBasePosition.z);
    }

    public int GetPriority()
    {
        return priority;
    }

    public bool CanTakeTurn()
    {
        return isInitialized && !IsPerformingAction && !isMoving;
    }

   /* private void OnDestroy()
    {
        Debug.Log("EnemyAI: Being destroyed");

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterAI(this);
        }

        if (isInitialized)
        {
            UpdateTileOccupancy(CurrentPosition, false);
        }
    }*/
}
