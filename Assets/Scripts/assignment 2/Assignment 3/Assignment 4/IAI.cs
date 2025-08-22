using UnityEngine;
using System.Collections.Generic;

public interface IAI
{
    /// <summary>
    /// Called when it's this AI's turn to act
    /// </summary>
    void TakeTurn();

    /// <summary>
    /// Request the AI to move to a specific target position
    /// </summary>
    /// <param name="targetPosition">Grid position to move to</param>
    void MoveToTarget(Vector2Int targetPosition);

    /// <summary>
    /// Check if the AI is currently performing an action
    /// </summary>
    bool IsPerformingAction { get; }

    /// <summary>
    /// Get the current grid position of this AI unit
    /// </summary>
    Vector2Int CurrentPosition { get; }

    /// <summary>
    /// Called when the AI's turn is complete
    /// </summary>
    System.Action OnTurnComplete { get; set; }

    /// <summary>
    /// Initialize the AI with starting parameters
    /// </summary>
    /// <param name="startPosition">Starting grid position</param>
    void Initialize(Vector2Int startPosition);

    /// <summary>
    /// Get the priority of this AI (for turn order)
    /// </summary>
    int GetPriority();

    /// <summary>
    /// Check if this AI can take a turn
    /// </summary>
    bool CanTakeTurn();
}
