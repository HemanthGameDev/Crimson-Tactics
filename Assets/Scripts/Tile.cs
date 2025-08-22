using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Tile Properties")]
    public Vector2Int gridPosition;
    public Vector3 worldPosition;
    public bool isOccupied = false;
    public bool isWalkable = true;

    [Header("Visual Feedback")]
    public Material defaultMaterial;
    public Material hoverMaterial;

    private Renderer tileRenderer;
    private bool isHovered = false;

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        worldPosition = transform.position;
    }

    public void Initialize(int x, int y, Vector3 worldPos)
    {
        gridPosition = new Vector2Int(x, y);
        worldPosition = worldPos;
        gameObject.name = $"Tile_({x},{y})";
    }

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }

    public void SetWalkable(bool walkable)
    {
        isWalkable = walkable;
    }

    public void OnMouseEnter()
    {
        if (!isHovered)
        {
            isHovered = true;
            if (hoverMaterial != null)
                tileRenderer.material = hoverMaterial;

            GridManager.Instance.OnTileHovered(this);
        }
    }

    public void OnMouseExit()
    {
        if (isHovered)
        {
            isHovered = false;
            if (defaultMaterial != null)
                tileRenderer.material = defaultMaterial;

            GridManager.Instance.OnTileUnhovered(this);
        }
    }

    public override string ToString()
    {
        return $"Tile ({gridPosition.x}, {gridPosition.y}) - World: {worldPosition} - Occupied: {isOccupied} - Walkable: {isWalkable}";
    }
}
