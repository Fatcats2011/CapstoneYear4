using System;
using UnityEngine;

/// <summary>
/// What a player's scooter, emotes and camera read from whoever drives them. InputManager implements it for a controller
/// on this machine. Online play (Phase 3) connects each avatar to its driver at runtime through the components' DriveInput
/// </summary>
public interface IDriveInput
{
    /// <summary>Steering, -1 (left) to 1 (right): the left stick</summary>
    float Steer { get; }

    /// <summary>Throttle, 0 to 1: the right trigger</summary>
    float Accelerate { get; }

    /// <summary>Brake, then reverse, 0 to 1: the left trigger</summary>
    float Brake { get; }

    /// <summary>Camera turn, -1 to 1: the right stick's X</summary>
    float CameraX { get; }

    /// <summary>Camera height, -1 to 1: the right stick's Y</summary>
    float CameraY { get; }

    /// <summary>Looking behind: the right stick pressed in</summary>
    bool LookBehind { get; }

    /// <summary>The drift button (X on Xbox): true when pressed, false when released</summary>
    event Action<bool> DriftButton;

    /// <summary>The boost button (A on Xbox): true when pressed, false when released</summary>
    event Action<bool> BoostButton;

    /// <summary>The d-pad pushed one way, for emotes: (0, 1) up, (1, 0) right, (0, -1) down, (-1, 0) left</summary>
    event Action<Vector2> EmotePad;
}
