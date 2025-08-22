using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("Player Settings")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Visual Feedback")]
    public GameObject pathPreviewPrefab;
    public Material playerMaterial;
    public float bounceHeight = 0.1f;
    public float bounceSpeed = 2f;

    [Header("Input Settings")]
    public bool enableInput = true;
    public LayerMask tileLayerMask = -1;

    // Current state
    public Vector2Int currentGridPosition;
    public bool isMoving { get; private set; }
    private bool isInitialized = false;

    // Movement data
    private Queue<Vector2Int> movementQueue;
    private List<GameObject> pathPreviewObjects;

    // Performance optimization - cache positions
    private Vector3 cachedBasePosition;
    private bool needsPositionUpdate = true;

    // Components
    private Camera playerCamera;
    private Renderer playerRenderer;

    // Events
    public System.Action<Vector2Int> OnMovementStarted;
    public System.Action<Vector2Int> OnMovementCompleted;
    public System.Action<Vector2Int> OnDestinationReached;

    private void Awake()
    {
        movementQueue = new Queue<Vector2Int>();
        pathPreviewObjects = new List<GameObject>();
        playerRenderer = GetComponent<Renderer>();

        if (playerMaterial != null && playerRenderer != null)
        {
            playerRenderer.material = playerMaterial;
        }
    }

    private void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        StartCoroutine(WaitForGridInitialization());
    }

    private IEnumerator WaitForGridInitialization()
    {
        while (GridManager.Instance == null ||
               PathfindingManager.Instance == null ||
               !PathfindingManager.Instance.IsPathfindingReady())
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();

        InitializePlayer();
    }

    private void InitializePlayer()
    {
        // Find a suitable starting position (first walkable tile)
        for (int x = 0; x < GridManager.Instance.gridWidth; x++)
        {
            for (int y = 0; y < GridManager.Instance.gridHeight; y++)
            {
                Tile tile = GridManager.Instance.GetTile(x, y);
                if (tile != null && tile.isWalkable && !tile.isOccupied)
                {
                    PlacePlayerAtPosition(new Vector2Int(x, y));
                    isInitialized = true;
                    Debug.Log($"Player initialized at position ({x}, {y})");
                    return;
                }
            }
        }

        Debug.LogError("No valid starting position found for player!");
    }

    private void Update()
    {
        if (!isInitialized || !enableInput || isMoving) return;

        HandleInput();
        HandleVisualEffects();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int? targetPosition = GetMouseGridPosition();
            if (targetPosition.HasValue)
            {
                RequestMoveTo(targetPosition.Value);
            }
        }

        // Optional: Show path preview on hover
        if (Input.GetMouseButtonDown(1)) // Right click for preview
        {
            Vector2Int? targetPosition = GetMouseGridPosition();
            if (targetPosition.HasValue)
            {
                ShowPathPreview(targetPosition.Value);
            }
        }
    }

    private Vector2Int? GetMouseGridPosition()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tileLayerMask))
        {
            Tile tile = hit.collider.GetComponent<Tile>();
            if (tile != null)
            {
                return tile.gridPosition;
            }
        }

        return null;
    }

    public void RequestMoveTo(Vector2Int targetPosition)
    {
        if (isMoving || targetPosition == currentGridPosition) return;

        List<Vector2Int> path = PathfindingManager.Instance.FindPath(currentGridPosition, targetPosition);

        if (path.Count > 0)
        {
            ClearPathPreview();
            StartMovement(path);
        }
        else
        {
            Debug.LogWarning($"No path found to position ({targetPosition.x}, {targetPosition.y})");
        }
    }

    public void StartMovement(List<Vector2Int> path)
    {
        if (isMoving) return;

        movementQueue.Clear();
        foreach (Vector2Int position in path)
        {
            movementQueue.Enqueue(position);
        }

        if (movementQueue.Count > 0)
        {
            StartCoroutine(MovementCoroutine());
        }
    }

    private IEnumerator MovementCoroutine()
    {
        isMoving = true;
        OnMovementStarted?.Invoke(currentGridPosition);

        while (movementQueue.Count > 0)
        {
            Vector2Int nextPosition = movementQueue.Dequeue();
            yield return StartCoroutine(MoveToPosition(nextPosition));
        }

        isMoving = false;
        needsPositionUpdate = true; // Update cached position after movement
        OnDestinationReached?.Invoke(currentGridPosition);
        OnMovementCompleted?.Invoke(currentGridPosition);
    }

    private IEnumerator MoveToPosition(Vector2Int targetGridPos)
    {
        Vector3 startWorldPos = transform.position;
        Vector3 targetWorldPos = GridManager.Instance.GridToWorldPosition(targetGridPos.x, targetGridPos.y);
        targetWorldPos.y = 0.5f; // Set player height to 0.5

        // Update grid occupancy
        UpdateTileOccupancy(currentGridPosition, false);
        UpdateTileOccupancy(targetGridPos, true);

        currentGridPosition = targetGridPos;

        float elapsed = 0;
        float duration = 1f / moveSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float curvedProgress = movementCurve.Evaluate(progress);

            Vector3 currentPos = Vector3.Lerp(startWorldPos, targetWorldPos, curvedProgress);

            // Add slight bounce effect while maintaining height of 0.5
            currentPos.y = 0.5f + (Mathf.Sin(progress * Mathf.PI) * bounceHeight);

            transform.position = currentPos;

            // Optional: Rotate towards movement direction
            Vector3 direction = (targetWorldPos - startWorldPos).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        transform.position = targetWorldPos;
    }

    private void UpdateTileOccupancy(Vector2Int gridPos, bool occupied)
    {
        Tile tile = GridManager.Instance.GetTile(gridPos.x, gridPos.y);
        if (tile != null)
        {
            tile.SetOccupied(occupied);
        }
    }

    public void PlacePlayerAtPosition(Vector2Int gridPosition)
    {
        if (!GridManager.Instance.IsValidGridPosition(gridPosition.x, gridPosition.y))
        {
            Debug.LogError($"Invalid grid position: ({gridPosition.x}, {gridPosition.y})");
            return;
        }

        // Clear previous occupancy
        if (isInitialized)
        {
            UpdateTileOccupancy(currentGridPosition, false);
        }

        currentGridPosition = gridPosition;
        Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(gridPosition.x, gridPosition.y);
        worldPosition.y = 0.5f; // Set player height to 0.5

        transform.position = worldPosition;
        UpdateTileOccupancy(currentGridPosition, true);

        // Cache the base position for performance
        cachedBasePosition = worldPosition;
        needsPositionUpdate = false;
    }

    private void ShowPathPreview(Vector2Int targetPosition)
    {
        ClearPathPreview();

        List<Vector2Int> path = PathfindingManager.Instance.FindPath(currentGridPosition, targetPosition);

        if (path.Count > 0 && pathPreviewPrefab != null)
        {
            foreach (Vector2Int pos in path)
            {
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
                worldPos.y += 0.1f; // Slightly above ground

                GameObject preview = Instantiate(pathPreviewPrefab, worldPos, Quaternion.identity);
                pathPreviewObjects.Add(preview);
            }
        }
    }

    private void ClearPathPreview()
    {
        foreach (GameObject obj in pathPreviewObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        pathPreviewObjects.Clear();
    }

    private void HandleVisualEffects()
    {
        // Only update cached position if needed (performance optimization)
        if (needsPositionUpdate)
        {
            cachedBasePosition = GridManager.Instance.GridToWorldPosition(currentGridPosition.x, currentGridPosition.y);
            cachedBasePosition.y = 0.5f; // Base height of 0.5
            needsPositionUpdate = false;
        }

        // Subtle idle animation using cached position
        float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight * 0.2f;
        transform.position = new Vector3(cachedBasePosition.x, cachedBasePosition.y + bounce, cachedBasePosition.z);
    }

    public void SetInputEnabled(bool enabled)
    {
        enableInput = enabled;
    }

    private void OnDestroy()
    {
        ClearPathPreview();

        if (isInitialized)
        {
            UpdateTileOccupancy(currentGridPosition, false);
        }
    }
}
