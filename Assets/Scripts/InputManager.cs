using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls; // ButtonControl

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public enum ActionType
    {
        Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6,
        Forward, Backward, Left, Right,
        Jump, Run, Crouch, Interact,
        Attack, ADS, Block,
        Inventory, Menu, FreeLook
    }

    // Public per-frame values
    public Vector2 Look { get; private set; } = Vector2.zero;
    public float Scroll { get; private set; } = 0f;

    Dictionary<ActionType, ButtonControl> bindings;

    void Awake()
    {
        // Constucts a binding map that you can check for each action
        // Will be easy to swap out custom keybinds if we want later
        
        // simple singleton
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        bindings = new Dictionary<ActionType, ButtonControl>
        {
            { ActionType.Forward,   kb.wKey },
            { ActionType.Backward,  kb.sKey },
            { ActionType.Left,      kb.aKey },
            { ActionType.Right,     kb.dKey },

            { ActionType.Jump,      kb.spaceKey },
            { ActionType.Run,       kb.leftShiftKey },  
            { ActionType.Crouch,    kb.cKey },
            { ActionType.Interact,  kb.eKey },

            { ActionType.Attack,    mouse.leftButton },
            { ActionType.ADS,       mouse.rightButton },
            { ActionType.Block,     mouse.rightButton },

            { ActionType.Inventory, kb.tabKey },
            { ActionType.Menu,      kb.escapeKey },
            { ActionType.FreeLook,  kb.vKey },

            { ActionType.Hotbar1,   kb.digit1Key },
            { ActionType.Hotbar2,   kb.digit2Key },
            { ActionType.Hotbar3,   kb.digit3Key },
            { ActionType.Hotbar4,   kb.digit4Key },
            { ActionType.Hotbar5,   kb.digit5Key },
            { ActionType.Hotbar6,   kb.digit6Key },
        };
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Look = mouse.delta.ReadValue();
            Scroll = mouse.scroll.ReadValue().y;
        }
        else
        {
            Look = Vector2.zero;
            Scroll = 0f;
        }
    }

    public bool IsActionDown(ActionType action)
    {
        return bindings.TryGetValue(action, out var button) && button != null && button.isPressed;
    }

    public bool IsActionJustDown(ActionType action)
    {
        return bindings.TryGetValue(action, out var button) && button != null && button.wasPressedThisFrame;
    }

    public bool IsActionUp(ActionType action)
    {
        return bindings.TryGetValue(action, out var button) && button != null && button.wasReleasedThisFrame;
    }
}
