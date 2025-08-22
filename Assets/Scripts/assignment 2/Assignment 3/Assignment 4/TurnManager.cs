using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    [Header("Turn Settings")]
    public float turnDelay = 0.5f;
    public bool autoStartTurns = true;

    [Header("Debug")]
    public bool showTurnDebug = true;

    // Singleton pattern
    public static TurnManager Instance { get; private set; }

    // Turn management
    private List<IAI> registeredAIs;
    private PlayerController playerController;
    private bool isPlayerTurn = true;
    private bool isTurnInProgress = false;
    private int currentAIIndex = 0;

    // Events
    public System.Action OnPlayerTurnStarted;
    public System.Action OnPlayerTurnEnded;
    public System.Action OnAITurnStarted;
    public System.Action OnAITurnEnded;
    public System.Action<IAI> OnAITakingTurn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        registeredAIs = new List<IAI>();
    }

    private void Start()
    {
        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        // Wait for player controller to be ready
        while (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            yield return null;
        }

        // Subscribe to player movement events
        playerController.OnMovementCompleted += OnPlayerMovementCompleted;

        if (autoStartTurns)
        {
            StartPlayerTurn();
        }

        if (showTurnDebug)
        {
            Debug.Log("TurnManager: Initialized successfully");
        }
    }

    public void RegisterAI(IAI ai)
    {
        if (!registeredAIs.Contains(ai))
        {
            registeredAIs.Add(ai);

            // Subscribe to AI turn completion
            ai.OnTurnComplete += OnAITurnCompleted;

            // Sort AIs by priority
            registeredAIs = registeredAIs.OrderBy(a => a.GetPriority()).ToList();

            if (showTurnDebug)
            {
                Debug.Log($"TurnManager: Registered AI at position {ai.CurrentPosition}");
            }
        }
    }

    public void UnregisterAI(IAI ai)
    {
        if (registeredAIs.Contains(ai))
        {
            ai.OnTurnComplete -= OnAITurnCompleted;
            registeredAIs.Remove(ai);

            if (showTurnDebug)
            {
                Debug.Log($"TurnManager: Unregistered AI");
            }
        }
    }

    public void StartPlayerTurn()
    {
        // Remove the problematic condition that prevents player turn from starting

        isPlayerTurn = true;
        isTurnInProgress = false;

        // Enable player input
        if (playerController != null)
        {
            playerController.SetInputEnabled(true);
        }

        OnPlayerTurnStarted?.Invoke();

        if (showTurnDebug)
        {
            Debug.Log("TurnManager: Player turn started");
        }
    }


    private void OnPlayerMovementCompleted(Vector2Int finalPosition)
    {
        if (!isPlayerTurn) return;

        StartCoroutine(EndPlayerTurnWithDelay());
    }

    private IEnumerator EndPlayerTurnWithDelay()
    {
        yield return new WaitForSeconds(turnDelay);
        EndPlayerTurn();
    }

    public void EndPlayerTurn()
    {
        if (!isPlayerTurn || isTurnInProgress) return;

        isPlayerTurn = false;
        isTurnInProgress = true;

        // Disable player input during AI turns
        if (playerController != null)
        {
            playerController.SetInputEnabled(false);
        }

        OnPlayerTurnEnded?.Invoke();

        if (showTurnDebug)
        {
            Debug.Log("TurnManager: Player turn ended");
        }

        StartAITurns();
    }

    private void StartAITurns()
    {
        OnAITurnStarted?.Invoke();
        currentAIIndex = 0;

        if (registeredAIs.Count > 0)
        {
            StartCoroutine(ProcessAITurns());
        }
        else
        {
            if (showTurnDebug)
            {
                Debug.Log("TurnManager: No AIs registered, returning to player turn");
            }
            EndAITurns();
        }
    }

    private IEnumerator ProcessAITurns()
    {
        while (currentAIIndex < registeredAIs.Count)
        {
            // Get current AI and check if it still exists
            if (currentAIIndex >= registeredAIs.Count)
                break;

            IAI currentAI = registeredAIs[currentAIIndex];

            // Check if AI is valid and can take turn
            if (currentAI != null && currentAI.CanTakeTurn())
            {
                OnAITakingTurn?.Invoke(currentAI);

                if (showTurnDebug)
                {
                    Debug.Log($"TurnManager: AI {currentAIIndex} taking turn");
                }

                currentAI.TakeTurn();

                // Wait for AI to start performing action
                yield return new WaitForSeconds(0.1f);

                // Wait for AI to complete its turn
                float timeoutCounter = 0f;
                while (currentAI != null && currentAI.IsPerformingAction && timeoutCounter < 10f)
                {
                    timeoutCounter += Time.deltaTime;
                    yield return null;
                }

                // Safety check for timeout
                if (timeoutCounter >= 10f)
                {
                    Debug.LogWarning($"TurnManager: AI {currentAIIndex} turn timed out");
                }

                yield return new WaitForSeconds(turnDelay);
            }
            else
            {
                if (showTurnDebug)
                {
                    Debug.Log($"TurnManager: AI {currentAIIndex} cannot take turn or is null");
                }
            }

            currentAIIndex++;
        }

        EndAITurns();
    }

    private void OnAITurnCompleted()
    {
        // AI turn completed, handled in ProcessAITurns coroutine
        if (showTurnDebug)
        {
            Debug.Log("TurnManager: AI turn completed");
        }
    }

    private void EndAITurns()
    {
        OnAITurnEnded?.Invoke();

        if (showTurnDebug)
        {
            Debug.Log("TurnManager: All AI turns completed");
        }

        // Always return to player turn, even if no AIs exist
        StartPlayerTurn();
    }

    public bool IsPlayerTurn()
    {
        return isPlayerTurn && !isTurnInProgress;
    }

    public bool IsAITurn()
    {
        return !isPlayerTurn && isTurnInProgress;
    }

    public void ForceEndCurrentTurn()
    {
        if (isPlayerTurn)
        {
            EndPlayerTurn();
        }
        else
        {
            // Skip to end of AI turns
            StopAllCoroutines();
            EndAITurns();
        }
    }

    // Add this method to manually trigger a player turn
    [ContextMenu("Force Player Turn")]
    public void ForcePlayerTurn()
    {
        StopAllCoroutines();
        isPlayerTurn = true;
        isTurnInProgress = false;

        if (playerController != null)
        {
            playerController.SetInputEnabled(true);
        }

        if (showTurnDebug)
        {
            Debug.Log("TurnManager: Forced player turn");
        }
    }

    private void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.OnMovementCompleted -= OnPlayerMovementCompleted;
        }
    }
}
