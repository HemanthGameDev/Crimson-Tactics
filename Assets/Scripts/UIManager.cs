using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text tileInfoText;
    public Text coordinatesText;
    public Text worldPositionText;
    public Text statusText;

    [Header("UI Panel")]
    public GameObject tileInfoPanel;

    // Singleton pattern
    public static UIManager Instance { get; private set; }

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
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (tileInfoPanel != null)
            tileInfoPanel.SetActive(false);

        ClearTileInfo();
    }

    public void UpdateTileInfo(Tile tile)
    {
        if (tile == null) return;

        if (tileInfoPanel != null)
            tileInfoPanel.SetActive(true);

        if (tileInfoText != null)
            tileInfoText.text = $"Tile: ({tile.gridPosition.x}, {tile.gridPosition.y})";

        if (coordinatesText != null)
            coordinatesText.text = $"Grid: ({tile.gridPosition.x}, {tile.gridPosition.y})";

        if (worldPositionText != null)
            worldPositionText.text = $"World: {tile.worldPosition:F2}";

        if (statusText != null)
        {
            string status = "";
            if (tile.isOccupied) status += "Occupied ";
            if (!tile.isWalkable) status += "Blocked ";
            if (string.IsNullOrEmpty(status)) status = "Available";

            statusText.text = $"Status: {status}";
        }
    }

    public void ClearTileInfo()
    {
        if (tileInfoPanel != null)
            tileInfoPanel.SetActive(false);

        if (tileInfoText != null)
            tileInfoText.text = "Hover over a tile";

        if (coordinatesText != null)
            coordinatesText.text = "Grid: --";

        if (worldPositionText != null)
            worldPositionText.text = "World: --";

        if (statusText != null)
            statusText.text = "Status: --";
    }
}
