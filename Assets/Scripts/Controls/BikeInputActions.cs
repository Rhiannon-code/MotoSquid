using MotoSquid.DevTools;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace MotoSquid.Controls
{
/// <summary>
/// Provides programmatic access to <see cref="InputActionAsset" />, <see cref="InputActionMap" />, <see cref="InputAction" /> and <see cref="InputControlScheme" /> Instances defined in asset "Assets/MotoSquid/Data/Controls/BikeInputActions.inputactions"
/// </summary>
/// <remarks>
/// This class is source generated and any manual edits will be discarded if the associated asset is reimported or modified
/// </remarks>
/// <example>
/// <code>
/// </code>
/// </example>
public partial class @BikeInputActions: IInputActionCollection2, IDisposable
{
    /// <summary>
    /// Provides access to the underlying asset instance
    /// </summary>
    public InputActionAsset asset { get; }

    /// <summary>
    /// Constructs a new instance
    /// </summary>
    public @BikeInputActions()
    {
        asset = InputActionAsset.FromJson(@"{
    ""version"": 1,
    ""name"": ""BikeInputActions"",
    ""maps"": [
        {
            ""name"": ""Bike"",
            ""id"": ""b1a2c3d4-e5f6-7890-abcd-ef1234567890"",
            ""actions"": [
                {
                    ""name"": ""Accelerate"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000001"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Reverse"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000002"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""HandBrake"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000003"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""SteeringLeft"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000004"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""SteeringRight"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000005"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Wheelie"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000006"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Boost"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000007"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Reset"",
                    ""type"": ""Button"",
                    ""id"": ""a0000001-0000-0000-0000-000000000008"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000001"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Accelerate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000002"",
                    ""path"": ""<Gamepad>/rightTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Accelerate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000003"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Reverse"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000004"",
                    ""path"": ""<Gamepad>/leftTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Reverse"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000005"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""HandBrake"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000006"",
                    ""path"": ""<Gamepad>/buttonEast"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""HandBrake"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000007"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""SteeringLeft"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000008"",
                    ""path"": ""<Gamepad>/leftStick/left"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""SteeringLeft"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000009"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""SteeringRight"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000a"",
                    ""path"": ""<Gamepad>/leftStick/right"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""SteeringRight"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000b"",
                    ""path"": ""<Keyboard>/leftShift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Wheelie"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000c"",
                    ""path"": ""<Gamepad>/leftStick/down"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Wheelie"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000d"",
                    ""path"": ""<Keyboard>/capsLock"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Boost"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000e"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Boost"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-00000000000f"",
                    ""path"": ""<Keyboard>/r"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Reset"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b0000001-0000-0000-0000-000000000010"",
                    ""path"": ""<Gamepad>/select"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Reset"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""Camera"",
            ""id"": ""c1a2c3d4-e5f6-7890-abcd-ef1234567890"",
            ""actions"": [
                {
                    ""name"": ""SwitchCamera"",
                    ""type"": ""Button"",
                    ""id"": ""c0000001-0000-0000-0000-000000000001"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""CameraLook"",
                    ""type"": ""Value"",
                    ""id"": ""8d2fa968-3cea-4c7a-a69e-e99f2e1df116"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Look Behind"",
                    ""type"": ""Button"",
                    ""id"": ""cade6fe1-c7c0-4550-ad1c-987cbee3f483"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""c0000002-0000-0000-0000-000000000001"",
                    ""path"": ""<Keyboard>/c"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""SwitchCamera"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c0000002-0000-0000-0000-000000000002"",
                    ""path"": ""<Gamepad>/dpad/left"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""SwitchCamera"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""0db8c0e0-477d-4bcf-a792-bcef93006796"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": "";Keyboard & Mouse"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""2a11ca11-0034-45a4-8878-b7304a624de7"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": "";Gamepad"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""0e2111e2-2fb0-4ea0-87bc-1f1fde05789c"",
                    ""path"": ""<Keyboard>/v"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": "";Keyboard & Mouse"",
                    ""action"": ""Look Behind"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""bb0b3ea9-0b7a-4f92-a98e-a0f2acc0995e"",
                    ""path"": ""<Gamepad>/rightStickPress"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": "";Gamepad"",
                    ""action"": ""Look Behind"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""Combat"",
            ""id"": ""d1a2c3d4-e5f6-7890-abcd-ef1234567890"",
            ""actions"": [
                {
                    ""name"": ""AttackLeft"",
                    ""type"": ""Button"",
                    ""id"": ""d0000001-0000-0000-0000-000000000001"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""AttackRight"",
                    ""type"": ""Button"",
                    ""id"": ""d0000001-0000-0000-0000-000000000002"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""d0000002-0000-0000-0000-000000000001"",
                    ""path"": ""<Keyboard>/q"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""AttackLeft"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d0000002-0000-0000-0000-000000000002"",
                    ""path"": ""<Gamepad>/leftShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""AttackLeft"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d0000002-0000-0000-0000-000000000003"",
                    ""path"": ""<Keyboard>/e"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""AttackRight"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d0000002-0000-0000-0000-000000000004"",
                    ""path"": ""<Gamepad>/rightShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""AttackRight"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""Menu"",
            ""id"": ""e1a2c3d4-e5f6-7890-abcd-ef1234567890"",
            ""actions"": [
                {
                    ""name"": ""Pause"",
                    ""type"": ""Button"",
                    ""id"": ""e0000001-0000-0000-0000-000000000001"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Skip"",
                    ""type"": ""Button"",
                    ""id"": ""50e16213-8a01-4784-b34f-48af0fdd730e"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Navigate"",
                    ""type"": ""Value"",
                    ""id"": ""e0000001-0000-0000-0000-000000000002"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Submit"",
                    ""type"": ""Button"",
                    ""id"": ""e0000001-0000-0000-0000-000000000003"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Cancel"",
                    ""type"": ""Button"",
                    ""id"": ""e0000001-0000-0000-0000-000000000004"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-000000000001"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-000000000002"",
                    ""path"": ""<Gamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""46f26d2a-7a58-41df-a8ed-4d1e47572e17"",
                    ""path"": ""<Keyboard>/n"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Skip"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""Arrows"",
                    ""id"": ""e0000002-0000-0000-0000-000000000003"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Navigate"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""e0000002-0000-0000-0000-000000000004"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""e0000002-0000-0000-0000-000000000005"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""e0000002-0000-0000-0000-000000000006"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""e0000002-0000-0000-0000-000000000007"",
                    ""path"": ""<Keyboard>/rightArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-000000000008"",
                    ""path"": ""<Gamepad>/dpad"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-000000000009"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Navigate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-00000000000a"",
                    ""path"": ""<Keyboard>/enter"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Submit"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-00000000000b"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Submit"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-00000000000c"",
                    ""path"": ""<Keyboard>/backspace"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard & Mouse"",
                    ""action"": ""Cancel"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e0000002-0000-0000-0000-00000000000d"",
                    ""path"": ""<Gamepad>/buttonEast"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Cancel"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""Keyboard & Mouse"",
            ""bindingGroup"": ""Keyboard & Mouse"",
            ""devices"": [
                {
                    ""devicePath"": ""<Keyboard>"",
                    ""isOptional"": false,
                    ""isOR"": false
                },
                {
                    ""devicePath"": ""<Mouse>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        },
        {
            ""name"": ""Gamepad"",
            ""bindingGroup"": ""Gamepad"",
            ""devices"": [
                {
                    ""devicePath"": ""<Gamepad>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        }
    ]
}");
        // Bike
        m_Bike = asset.FindActionMap("Bike", throwIfNotFound: true);
        m_Bike_Accelerate = m_Bike.FindAction("Accelerate", throwIfNotFound: true);
        m_Bike_Reverse = m_Bike.FindAction("Reverse", throwIfNotFound: true);
        m_Bike_HandBrake = m_Bike.FindAction("HandBrake", throwIfNotFound: true);
        m_Bike_SteeringLeft = m_Bike.FindAction("SteeringLeft", throwIfNotFound: true);
        m_Bike_SteeringRight = m_Bike.FindAction("SteeringRight", throwIfNotFound: true);
        m_Bike_Wheelie = m_Bike.FindAction("Wheelie", throwIfNotFound: true);
        m_Bike_Boost = m_Bike.FindAction("Boost", throwIfNotFound: true);
        m_Bike_Reset = m_Bike.FindAction("Reset", throwIfNotFound: true);
        // Camera
        m_Camera = asset.FindActionMap("Camera", throwIfNotFound: true);
        m_Camera_SwitchCamera = m_Camera.FindAction("SwitchCamera", throwIfNotFound: true);
        m_Camera_CameraLook = m_Camera.FindAction("CameraLook", throwIfNotFound: true);
        m_Camera_LookBehind = m_Camera.FindAction("Look Behind", throwIfNotFound: true);
        // Combat
        m_Combat = asset.FindActionMap("Combat", throwIfNotFound: true);
        m_Combat_AttackLeft = m_Combat.FindAction("AttackLeft", throwIfNotFound: true);
        m_Combat_AttackRight = m_Combat.FindAction("AttackRight", throwIfNotFound: true);
        // Menu
        m_Menu = asset.FindActionMap("Menu", throwIfNotFound: true);
        m_Menu_Pause = m_Menu.FindAction("Pause", throwIfNotFound: true);
        m_Menu_Skip = m_Menu.FindAction("Skip", throwIfNotFound: true);
        m_Menu_Navigate = m_Menu.FindAction("Navigate", throwIfNotFound: true);
        m_Menu_Submit = m_Menu.FindAction("Submit", throwIfNotFound: true);
        m_Menu_Cancel = m_Menu.FindAction("Cancel", throwIfNotFound: true);
    }

    ~@BikeInputActions()
    {
        UnityEngine.Debug.Assert(!m_Bike.enabled, "This will cause a leak and performance issues, BikeInputActions.Bike.Disable() has not been called.");
        UnityEngine.Debug.Assert(!m_Camera.enabled, "This will cause a leak and performance issues, BikeInputActions.Camera.Disable() has not been called.");
        UnityEngine.Debug.Assert(!m_Combat.enabled, "This will cause a leak and performance issues, BikeInputActions.Combat.Disable() has not been called.");
        UnityEngine.Debug.Assert(!m_Menu.enabled, "This will cause a leak and performance issues, BikeInputActions.Menu.Disable() has not been called.");
    }

    /// <summary>
    /// Destroys this asset and all associated <see cref="InputAction"/> instances
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindingMask" />
    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.devices" />
    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.controlSchemes" />
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Contains(InputAction)" />
    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.GetEnumerator()" />
    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    /// <inheritdoc cref="IEnumerable.GetEnumerator()" />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Enable()" />
    public void Enable()
    {
        asset.Enable();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.Disable()" />
    public void Disable()
    {
        asset.Disable();
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.bindings" />
    public IEnumerable<InputBinding> bindings => asset.bindings;

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.FindAction(string, bool)" />
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    /// <inheritdoc cref="UnityEngine.InputSystem.InputActionAsset.FindBinding(InputBinding, out InputAction)" />
    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    // Bike
    private readonly InputActionMap m_Bike;
    private List<IBikeActions> m_BikeActionsCallbackInterfaces = new List<IBikeActions>();
    private readonly InputAction m_Bike_Accelerate;
    private readonly InputAction m_Bike_Reverse;
    private readonly InputAction m_Bike_HandBrake;
    private readonly InputAction m_Bike_SteeringLeft;
    private readonly InputAction m_Bike_SteeringRight;
    private readonly InputAction m_Bike_Wheelie;
    private readonly InputAction m_Bike_Boost;
    private readonly InputAction m_Bike_Reset;
    /// <summary>
    /// Provides access to input actions defined in input action map "Bike"
    /// </summary>
    public struct BikeActions
    {
        private @BikeInputActions m_Wrapper;

        /// <summary>
        /// Construct a new instance of the input action map wrapper class
        /// </summary>
        public BikeActions(@BikeInputActions wrapper) { m_Wrapper = wrapper; }
        /// <summary>
        /// Provides access to the underlying input action "Bike/Accelerate"
        /// </summary>
        public InputAction @Accelerate => m_Wrapper.m_Bike_Accelerate;
        /// <summary>
        /// Provides access to the underlying input action "Bike/Reverse"
        /// </summary>
        public InputAction @Reverse => m_Wrapper.m_Bike_Reverse;
        /// <summary>
        /// Provides access to the underlying input action "Bike/HandBrake"
        /// </summary>
        public InputAction @HandBrake => m_Wrapper.m_Bike_HandBrake;
        /// <summary>
        /// Provides access to the underlying input action "Bike/SteeringLeft"
        /// </summary>
        public InputAction @SteeringLeft => m_Wrapper.m_Bike_SteeringLeft;
        /// <summary>
        /// Provides access to the underlying input action "Bike/SteeringRight"
        /// </summary>
        public InputAction @SteeringRight => m_Wrapper.m_Bike_SteeringRight;
        /// <summary>
        /// Provides access to the underlying input action "Bike/Wheelie"
        /// </summary>
        public InputAction @Wheelie => m_Wrapper.m_Bike_Wheelie;
        /// <summary>
        /// Provides access to the underlying input action "Bike/Boost"
        /// </summary>
        public InputAction @Boost => m_Wrapper.m_Bike_Boost;
        /// <summary>
        /// Provides access to the underlying input action "Bike/Reset"
        /// </summary>
        public InputAction @Reset => m_Wrapper.m_Bike_Reset;
        /// <summary>
        /// Provides access to the underlying input action map instance
        /// </summary>
        public InputActionMap Get() { return m_Wrapper.m_Bike; }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
        public void Enable() { Get().Enable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
        public void Disable() { Get().Disable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
        public bool enabled => Get().enabled;
        /// <summary>
        /// Implicitly converts an <see ref="BikeActions" /> to an <see ref="InputActionMap" /> instance.
        /// </summary>
        public static implicit operator InputActionMap(BikeActions set) { return set.Get(); }
        /// <summary>
        /// Adds <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map
        /// </summary>
        /// <param name="instance">Callback instance.</param>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c> or <paramref name="instance"/> have already been added this method does nothing
        /// </remarks>
        /// <seealso cref="BikeActions" />
        public void AddCallbacks(IBikeActions instance)
        {
            if (instance == null || m_Wrapper.m_BikeActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_BikeActionsCallbackInterfaces.Add(instance);
            @Accelerate.started += instance.OnAccelerate;
            @Accelerate.performed += instance.OnAccelerate;
            @Accelerate.canceled += instance.OnAccelerate;
            @Reverse.started += instance.OnReverse;
            @Reverse.performed += instance.OnReverse;
            @Reverse.canceled += instance.OnReverse;
            @HandBrake.started += instance.OnHandBrake;
            @HandBrake.performed += instance.OnHandBrake;
            @HandBrake.canceled += instance.OnHandBrake;
            @SteeringLeft.started += instance.OnSteeringLeft;
            @SteeringLeft.performed += instance.OnSteeringLeft;
            @SteeringLeft.canceled += instance.OnSteeringLeft;
            @SteeringRight.started += instance.OnSteeringRight;
            @SteeringRight.performed += instance.OnSteeringRight;
            @SteeringRight.canceled += instance.OnSteeringRight;
            @Wheelie.started += instance.OnWheelie;
            @Wheelie.performed += instance.OnWheelie;
            @Wheelie.canceled += instance.OnWheelie;
            @Boost.started += instance.OnBoost;
            @Boost.performed += instance.OnBoost;
            @Boost.canceled += instance.OnBoost;
            @Reset.started += instance.OnReset;
            @Reset.performed += instance.OnReset;
            @Reset.canceled += instance.OnReset;
        }

        /// <summary>
        /// Removes <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map
        /// </summary>
        /// <remarks>
        /// Calling this method when <paramref name="instance" /> have not previously been registered has no side effects
        /// </remarks>
        /// <seealso cref="BikeActions" />
        private void UnregisterCallbacks(IBikeActions instance)
        {
            @Accelerate.started -= instance.OnAccelerate;
            @Accelerate.performed -= instance.OnAccelerate;
            @Accelerate.canceled -= instance.OnAccelerate;
            @Reverse.started -= instance.OnReverse;
            @Reverse.performed -= instance.OnReverse;
            @Reverse.canceled -= instance.OnReverse;
            @HandBrake.started -= instance.OnHandBrake;
            @HandBrake.performed -= instance.OnHandBrake;
            @HandBrake.canceled -= instance.OnHandBrake;
            @SteeringLeft.started -= instance.OnSteeringLeft;
            @SteeringLeft.performed -= instance.OnSteeringLeft;
            @SteeringLeft.canceled -= instance.OnSteeringLeft;
            @SteeringRight.started -= instance.OnSteeringRight;
            @SteeringRight.performed -= instance.OnSteeringRight;
            @SteeringRight.canceled -= instance.OnSteeringRight;
            @Wheelie.started -= instance.OnWheelie;
            @Wheelie.performed -= instance.OnWheelie;
            @Wheelie.canceled -= instance.OnWheelie;
            @Boost.started -= instance.OnBoost;
            @Boost.performed -= instance.OnBoost;
            @Boost.canceled -= instance.OnBoost;
            @Reset.started -= instance.OnReset;
            @Reset.performed -= instance.OnReset;
            @Reset.canceled -= instance.OnReset;
        }

        /// <summary>
        /// Unregisters <param cref="instance" /> and unregisters all input action callbacks via <see cref="BikeActions.UnregisterCallbacks(IBikeActions)" />.
        /// </summary>
        /// <seealso cref="BikeActions.UnregisterCallbacks(IBikeActions)" />
        public void RemoveCallbacks(IBikeActions instance)
        {
            if (m_Wrapper.m_BikeActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        /// <summary>
        /// Replaces all existing callback instances and previously registered input action callbacks associated with them with callbacks provided via <param cref="instance" />.
        /// </summary>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c>, calling this method will only unregister all existing callbacks but not register any new callbacks
        /// </remarks>
        /// <seealso cref="BikeActions.AddCallbacks(IBikeActions)" />
        /// <seealso cref="BikeActions.RemoveCallbacks(IBikeActions)" />
        /// <seealso cref="BikeActions.UnregisterCallbacks(IBikeActions)" />
        public void SetCallbacks(IBikeActions instance)
        {
            foreach (var item in m_Wrapper.m_BikeActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_BikeActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    /// <summary>
    /// Provides a new <see cref="BikeActions" /> instance referencing this action map
    /// </summary>
    public BikeActions @Bike => new BikeActions(this);

    // Camera
    private readonly InputActionMap m_Camera;
    private List<ICameraActions> m_CameraActionsCallbackInterfaces = new List<ICameraActions>();
    private readonly InputAction m_Camera_SwitchCamera;
    private readonly InputAction m_Camera_CameraLook;
    private readonly InputAction m_Camera_LookBehind;
    /// <summary>
    /// Provides access to input actions defined in input action map "Camera"
    /// </summary>
    public struct CameraActions
    {
        private @BikeInputActions m_Wrapper;

        /// <summary>
        /// Construct a new instance of the input action map wrapper class
        /// </summary>
        public CameraActions(@BikeInputActions wrapper) { m_Wrapper = wrapper; }
        /// <summary>
        /// Provides access to the underlying input action "Camera/SwitchCamera"
        /// </summary>
        public InputAction @SwitchCamera => m_Wrapper.m_Camera_SwitchCamera;
        /// <summary>
        /// Provides access to the underlying input action "Camera/CameraLook"
        /// </summary>
        public InputAction @CameraLook => m_Wrapper.m_Camera_CameraLook;
        /// <summary>
        /// Provides access to the underlying input action "Camera/LookBehind"
        /// </summary>
        public InputAction @LookBehind => m_Wrapper.m_Camera_LookBehind;
        /// <summary>
        /// Provides access to the underlying input action map instance
        /// </summary>
        public InputActionMap Get() { return m_Wrapper.m_Camera; }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
        public void Enable() { Get().Enable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
        public void Disable() { Get().Disable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
        public bool enabled => Get().enabled;
        /// <summary>
        /// Implicitly converts an <see ref="CameraActions" /> to an <see ref="InputActionMap" /> instance.
        /// </summary>
        public static implicit operator InputActionMap(CameraActions set) { return set.Get(); }
        /// <summary>
        /// Adds <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map
        /// </summary>
        /// <param name="instance">Callback instance.</param>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c> or <paramref name="instance"/> have already been added this method does nothing
        /// </remarks>
        /// <seealso cref="CameraActions" />
        public void AddCallbacks(ICameraActions instance)
        {
            if (instance == null || m_Wrapper.m_CameraActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_CameraActionsCallbackInterfaces.Add(instance);
            @SwitchCamera.started += instance.OnSwitchCamera;
            @SwitchCamera.performed += instance.OnSwitchCamera;
            @SwitchCamera.canceled += instance.OnSwitchCamera;
            @CameraLook.started += instance.OnCameraLook;
            @CameraLook.performed += instance.OnCameraLook;
            @CameraLook.canceled += instance.OnCameraLook;
            @LookBehind.started += instance.OnLookBehind;
            @LookBehind.performed += instance.OnLookBehind;
            @LookBehind.canceled += instance.OnLookBehind;
        }

        /// <summary>
        /// Removes <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map
        /// </summary>
        /// <remarks>
        /// Calling this method when <paramref name="instance" /> have not previously been registered has no side effects
        /// </remarks>
        /// <seealso cref="CameraActions" />
        private void UnregisterCallbacks(ICameraActions instance)
        {
            @SwitchCamera.started -= instance.OnSwitchCamera;
            @SwitchCamera.performed -= instance.OnSwitchCamera;
            @SwitchCamera.canceled -= instance.OnSwitchCamera;
            @CameraLook.started -= instance.OnCameraLook;
            @CameraLook.performed -= instance.OnCameraLook;
            @CameraLook.canceled -= instance.OnCameraLook;
            @LookBehind.started -= instance.OnLookBehind;
            @LookBehind.performed -= instance.OnLookBehind;
            @LookBehind.canceled -= instance.OnLookBehind;
        }

        /// <summary>
        /// Unregisters <param cref="instance" /> and unregisters all input action callbacks via <see cref="CameraActions.UnregisterCallbacks(ICameraActions)" />.
        /// </summary>
        /// <seealso cref="CameraActions.UnregisterCallbacks(ICameraActions)" />
        public void RemoveCallbacks(ICameraActions instance)
        {
            if (m_Wrapper.m_CameraActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        /// <summary>
        /// Replaces all existing callback instances and previously registered input action callbacks associated with them with callbacks provided via <param cref="instance" />.
        /// </summary>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c>, calling this method will only unregister all existing callbacks but not register any new callbacks.
        /// </remarks>
        /// <seealso cref="CameraActions.AddCallbacks(ICameraActions)" />
        /// <seealso cref="CameraActions.RemoveCallbacks(ICameraActions)" />
        /// <seealso cref="CameraActions.UnregisterCallbacks(ICameraActions)" />
        public void SetCallbacks(ICameraActions instance)
        {
            foreach (var item in m_Wrapper.m_CameraActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_CameraActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    /// <summary>
    /// Provides a new <see cref="CameraActions" /> instance referencing this action map.
    /// </summary>
    public CameraActions @Camera => new CameraActions(this);

    // Combat
    private readonly InputActionMap m_Combat;
    private List<ICombatActions> m_CombatActionsCallbackInterfaces = new List<ICombatActions>();
    private readonly InputAction m_Combat_AttackLeft;
    private readonly InputAction m_Combat_AttackRight;
    /// <summary>
    /// Provides access to input actions defined in input action map "Combat".
    /// </summary>
    public struct CombatActions
    {
        private @BikeInputActions m_Wrapper;

        /// <summary>
        /// Construct a new instance of the input action map wrapper class.
        /// </summary>
        public CombatActions(@BikeInputActions wrapper) { m_Wrapper = wrapper; }
        /// <summary>
        /// Provides access to the underlying input action "Combat/AttackLeft".
        /// </summary>
        public InputAction @AttackLeft => m_Wrapper.m_Combat_AttackLeft;
        /// <summary>
        /// Provides access to the underlying input action "Combat/AttackRight".
        /// </summary>
        public InputAction @AttackRight => m_Wrapper.m_Combat_AttackRight;
        /// <summary>
        /// Provides access to the underlying input action map instance.
        /// </summary>
        public InputActionMap Get() { return m_Wrapper.m_Combat; }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
        public void Enable() { Get().Enable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
        public void Disable() { Get().Disable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
        public bool enabled => Get().enabled;
        /// <summary>
        /// Implicitly converts an <see ref="CombatActions" /> to an <see ref="InputActionMap" /> instance.
        /// </summary>
        public static implicit operator InputActionMap(CombatActions set) { return set.Get(); }
        /// <summary>
        /// Adds <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <param name="instance">Callback instance.</param>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c> or <paramref name="instance"/> have already been added this method does nothing.
        /// </remarks>
        /// <seealso cref="CombatActions" />
        public void AddCallbacks(ICombatActions instance)
        {
            if (instance == null || m_Wrapper.m_CombatActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_CombatActionsCallbackInterfaces.Add(instance);
            @AttackLeft.started += instance.OnAttackLeft;
            @AttackLeft.performed += instance.OnAttackLeft;
            @AttackLeft.canceled += instance.OnAttackLeft;
            @AttackRight.started += instance.OnAttackRight;
            @AttackRight.performed += instance.OnAttackRight;
            @AttackRight.canceled += instance.OnAttackRight;
        }

        /// <summary>
        /// Removes <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <remarks>
        /// Calling this method when <paramref name="instance" /> have not previously been registered has no side-effects.
        /// </remarks>
        /// <seealso cref="CombatActions" />
        private void UnregisterCallbacks(ICombatActions instance)
        {
            @AttackLeft.started -= instance.OnAttackLeft;
            @AttackLeft.performed -= instance.OnAttackLeft;
            @AttackLeft.canceled -= instance.OnAttackLeft;
            @AttackRight.started -= instance.OnAttackRight;
            @AttackRight.performed -= instance.OnAttackRight;
            @AttackRight.canceled -= instance.OnAttackRight;
        }

        /// <summary>
        /// Unregisters <param cref="instance" /> and unregisters all input action callbacks via <see cref="CombatActions.UnregisterCallbacks(ICombatActions)" />.
        /// </summary>
        /// <seealso cref="CombatActions.UnregisterCallbacks(ICombatActions)" />
        public void RemoveCallbacks(ICombatActions instance)
        {
            if (m_Wrapper.m_CombatActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        /// <summary>
        /// Replaces all existing callback instances and previously registered input action callbacks associated with them with callbacks provided via <param cref="instance" />.
        /// </summary>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c>, calling this method will only unregister all existing callbacks but not register any new callbacks.
        /// </remarks>
        /// <seealso cref="CombatActions.AddCallbacks(ICombatActions)" />
        /// <seealso cref="CombatActions.RemoveCallbacks(ICombatActions)" />
        /// <seealso cref="CombatActions.UnregisterCallbacks(ICombatActions)" />
        public void SetCallbacks(ICombatActions instance)
        {
            foreach (var item in m_Wrapper.m_CombatActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_CombatActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    /// <summary>
    /// Provides a new <see cref="CombatActions" /> instance referencing this action map.
    /// </summary>
    public CombatActions @Combat => new CombatActions(this);

    // Menu
    private readonly InputActionMap m_Menu;
    private List<IMenuActions> m_MenuActionsCallbackInterfaces = new List<IMenuActions>();
    private readonly InputAction m_Menu_Pause;
    private readonly InputAction m_Menu_Skip;
    private readonly InputAction m_Menu_Navigate;
    private readonly InputAction m_Menu_Submit;
    private readonly InputAction m_Menu_Cancel;
    /// <summary>
    /// Provides access to input actions defined in input action map "Menu".
    /// </summary>
    public struct MenuActions
    {
        private @BikeInputActions m_Wrapper;

        /// <summary>
        /// Construct a new instance of the input action map wrapper class.
        /// </summary>
        public MenuActions(@BikeInputActions wrapper) { m_Wrapper = wrapper; }
        /// <summary>
        /// Provides access to the underlying input action "Menu/Pause".
        /// </summary>
        public InputAction @Pause => m_Wrapper.m_Menu_Pause;
        /// <summary>
        /// Provides access to the underlying input action "Menu/Skip".
        /// </summary>
        public InputAction @Skip => m_Wrapper.m_Menu_Skip;
        /// <summary>
        /// Provides access to the underlying input action "Menu/Navigate".
        /// </summary>
        public InputAction @Navigate => m_Wrapper.m_Menu_Navigate;
        /// <summary>
        /// Provides access to the underlying input action "Menu/Submit".
        /// </summary>
        public InputAction @Submit => m_Wrapper.m_Menu_Submit;
        /// <summary>
        /// Provides access to the underlying input action "Menu/Cancel".
        /// </summary>
        public InputAction @Cancel => m_Wrapper.m_Menu_Cancel;
        /// <summary>
        /// Provides access to the underlying input action map instance.
        /// </summary>
        public InputActionMap Get() { return m_Wrapper.m_Menu; }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Enable()" />
        public void Enable() { Get().Enable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.Disable()" />
        public void Disable() { Get().Disable(); }
        /// <inheritdoc cref="UnityEngine.InputSystem.InputActionMap.enabled" />
        public bool enabled => Get().enabled;
        /// <summary>
        /// Implicitly converts an <see ref="MenuActions" /> to an <see ref="InputActionMap" /> instance.
        /// </summary>
        public static implicit operator InputActionMap(MenuActions set) { return set.Get(); }
        /// <summary>
        /// Adds <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <param name="instance">Callback instance.</param>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c> or <paramref name="instance"/> have already been added this method does nothing.
        /// </remarks>
        /// <seealso cref="MenuActions" />
        public void AddCallbacks(IMenuActions instance)
        {
            if (instance == null || m_Wrapper.m_MenuActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_MenuActionsCallbackInterfaces.Add(instance);
            @Pause.started += instance.OnPause;
            @Pause.performed += instance.OnPause;
            @Pause.canceled += instance.OnPause;
            @Skip.started += instance.OnSkip;
            @Skip.performed += instance.OnSkip;
            @Skip.canceled += instance.OnSkip;
            @Navigate.started += instance.OnNavigate;
            @Navigate.performed += instance.OnNavigate;
            @Navigate.canceled += instance.OnNavigate;
            @Submit.started += instance.OnSubmit;
            @Submit.performed += instance.OnSubmit;
            @Submit.canceled += instance.OnSubmit;
            @Cancel.started += instance.OnCancel;
            @Cancel.performed += instance.OnCancel;
            @Cancel.canceled += instance.OnCancel;
        }

        /// <summary>
        /// Removes <see cref="InputAction.started"/>, <see cref="InputAction.performed"/> and <see cref="InputAction.canceled"/> callbacks provided via <param cref="instance" /> on all input actions contained in this map.
        /// </summary>
        /// <remarks>
        /// Calling this method when <paramref name="instance" /> have not previously been registered has no side-effects.
        /// </remarks>
        /// <seealso cref="MenuActions" />
        private void UnregisterCallbacks(IMenuActions instance)
        {
            @Pause.started -= instance.OnPause;
            @Pause.performed -= instance.OnPause;
            @Pause.canceled -= instance.OnPause;
            @Skip.started -= instance.OnSkip;
            @Skip.performed -= instance.OnSkip;
            @Skip.canceled -= instance.OnSkip;
            @Navigate.started -= instance.OnNavigate;
            @Navigate.performed -= instance.OnNavigate;
            @Navigate.canceled -= instance.OnNavigate;
            @Submit.started -= instance.OnSubmit;
            @Submit.performed -= instance.OnSubmit;
            @Submit.canceled -= instance.OnSubmit;
            @Cancel.started -= instance.OnCancel;
            @Cancel.performed -= instance.OnCancel;
            @Cancel.canceled -= instance.OnCancel;
        }

        /// <summary>
        /// Unregisters <param cref="instance" /> and unregisters all input action callbacks via <see cref="MenuActions.UnregisterCallbacks(IMenuActions)" />.
        /// </summary>
        /// <seealso cref="MenuActions.UnregisterCallbacks(IMenuActions)" />
        public void RemoveCallbacks(IMenuActions instance)
        {
            if (m_Wrapper.m_MenuActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        /// <summary>
        /// Replaces all existing callback instances and previously registered input action callbacks associated with them with callbacks provided via <param cref="instance" />.
        /// </summary>
        /// <remarks>
        /// If <paramref name="instance" /> is <c>null</c>, calling this method will only unregister all existing callbacks but not register any new callbacks.
        /// </remarks>
        /// <seealso cref="MenuActions.AddCallbacks(IMenuActions)" />
        /// <seealso cref="MenuActions.RemoveCallbacks(IMenuActions)" />
        /// <seealso cref="MenuActions.UnregisterCallbacks(IMenuActions)" />
        public void SetCallbacks(IMenuActions instance)
        {
            foreach (var item in m_Wrapper.m_MenuActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_MenuActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    /// <summary>
    /// Provides a new <see cref="MenuActions" /> instance referencing this action map.
    /// </summary>
    public MenuActions @Menu => new MenuActions(this);
    private int m_KeyboardMouseSchemeIndex = -1;
    /// <summary>
    /// Provides access to the input control scheme.
    /// </summary>
    /// <seealso cref="UnityEngine.InputSystem.InputControlScheme" />
    public InputControlScheme KeyboardMouseScheme
    {
        get
        {
            if (m_KeyboardMouseSchemeIndex == -1) m_KeyboardMouseSchemeIndex = asset.FindControlSchemeIndex("Keyboard & Mouse");
            return asset.controlSchemes[m_KeyboardMouseSchemeIndex];
        }
    }
    private int m_GamepadSchemeIndex = -1;
    /// <summary>
    /// Provides access to the input control scheme.
    /// </summary>
    /// <seealso cref="UnityEngine.InputSystem.InputControlScheme" />
    public InputControlScheme GamepadScheme
    {
        get
        {
            if (m_GamepadSchemeIndex == -1) m_GamepadSchemeIndex = asset.FindControlSchemeIndex("Gamepad");
            return asset.controlSchemes[m_GamepadSchemeIndex];
        }
    }
    /// <summary>
    /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "Bike" which allows adding and removing callbacks.
    /// </summary>
    /// <seealso cref="BikeActions.AddCallbacks(IBikeActions)" />
    /// <seealso cref="BikeActions.RemoveCallbacks(IBikeActions)" />
    public interface IBikeActions
    {
        /// <summary>
        /// Method invoked when associated input action "Accelerate" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnAccelerate(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Reverse" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnReverse(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "HandBrake" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnHandBrake(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "SteeringLeft" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSteeringLeft(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "SteeringRight" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSteeringRight(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Wheelie" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnWheelie(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Boost" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnBoost(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Reset" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnReset(InputAction.CallbackContext context);
    }
    /// <summary>
    /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "Camera" which allows adding and removing callbacks.
    /// </summary>
    /// <seealso cref="CameraActions.AddCallbacks(ICameraActions)" />
    /// <seealso cref="CameraActions.RemoveCallbacks(ICameraActions)" />
    public interface ICameraActions
    {
        /// <summary>
        /// Method invoked when associated input action "SwitchCamera" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSwitchCamera(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "CameraLook" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnCameraLook(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Look Behind" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnLookBehind(InputAction.CallbackContext context);
    }
    /// <summary>
    /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "Combat" which allows adding and removing callbacks.
    /// </summary>
    /// <seealso cref="CombatActions.AddCallbacks(ICombatActions)" />
    /// <seealso cref="CombatActions.RemoveCallbacks(ICombatActions)" />
    public interface ICombatActions
    {
        /// <summary>
        /// Method invoked when associated input action "AttackLeft" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnAttackLeft(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "AttackRight" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnAttackRight(InputAction.CallbackContext context);
    }
    /// <summary>
    /// Interface to implement callback methods for all input action callbacks associated with input actions defined by "Menu" which allows adding and removing callbacks.
    /// </summary>
    /// <seealso cref="MenuActions.AddCallbacks(IMenuActions)" />
    /// <seealso cref="MenuActions.RemoveCallbacks(IMenuActions)" />
    public interface IMenuActions
    {
        /// <summary>
        /// Method invoked when associated input action "Pause" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnPause(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Skip" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSkip(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Navigate" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnNavigate(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Submit" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnSubmit(InputAction.CallbackContext context);
        /// <summary>
        /// Method invoked when associated input action "Cancel" is either <see cref="UnityEngine.InputSystem.InputAction.started" />, <see cref="UnityEngine.InputSystem.InputAction.performed" /> or <see cref="UnityEngine.InputSystem.InputAction.canceled" />.
        /// </summary>
        /// <seealso cref="UnityEngine.InputSystem.InputAction.started" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.performed" />
        /// <seealso cref="UnityEngine.InputSystem.InputAction.canceled" />
        void OnCancel(InputAction.CallbackContext context);
    }
}
}
