using UnityEngine;
using UnityEngine.UI;
using TMPro;
// Define the enum outside the class so it's accessible everywhere
public enum DebugType
{
    Movement,
    Inventory,
    Vehicle,
    Player,
    Combat,
    UI,
    Audio,
    Network,
    Animation
}

public class DebugManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private bool showOnScreenText = true;

    [Header("Debug Types")]
    [SerializeField] private bool showAllDebug = false;
    [SerializeField] private bool showMovementDebug = false;
    [SerializeField] private bool showInventoryDebug = false;
    [SerializeField] private bool showVehicleDebug = false;
    [SerializeField] private bool showPlayerDebug = false;
    [SerializeField] private bool showCombatDebug = false;
    [SerializeField] private bool showUIDebug = false;
    [SerializeField] private bool showAudioDebug = false;
    [SerializeField] private bool showNetworkDebug = false;
    [SerializeField] private bool showAnimationDebug = false;
    
    // Singleton instance
    public static DebugManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // Method to check if a specific debug type should be shown
    private bool ShouldShowDebugType(DebugType debugType)
    {
        if (showAllDebug) return true;
        
        return debugType switch
        {
            DebugType.Movement => showMovementDebug,
            DebugType.Inventory => showInventoryDebug,
            DebugType.Vehicle => showVehicleDebug,
            DebugType.Player => showPlayerDebug,
            DebugType.Combat => showCombatDebug,
            DebugType.UI => showUIDebug,
            DebugType.Audio => showAudioDebug,
            DebugType.Network => showNetworkDebug,
            DebugType.Animation => showAnimationDebug,
            _ => false
        };
    }
    
    public void SetDebugText(string text)
    {
        if (debugText != null && showOnScreenText)
        {
            debugText.text = text;
        }
    }
    
    public void Log(string message, DebugType debugType = DebugType.Player)
    {
        if (ShouldShowDebugType(debugType))
        {
            Debug.Log($"[{debugType}] {message}");
        }
    }
} 