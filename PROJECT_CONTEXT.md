# Project Context

## Goal

This project is a Unity-based robotic-assisted surgery (RAS) simulation. The immediate goal is to mirror a physical arm inside the simulation so the virtual gripper responds in a believable and controllable way.

## Current System

- A physical arm sends live joint-angle telemetry over USB.
- Unity receives that data through `PacketProcessor`.
- The simulation uses those incoming joint angles to drive the virtual tool/gripper.

## Current Control Strategy

The original approach attempted fuller forward-kinematics style behavior, but it has not been producing movement that is stable or visually usable enough for this phase of the project.

The current approach is intentionally simpler:

- Treat each incoming joint angle as a direct driver for a specific visual behavior.
- Use practical mappings like "if this angle changes, move/rotate this part this way."
- Prioritize controllable, debuggable motion over mathematically complete kinematics.

## Current Focus

Right now the main task is improving the movement quality of the gripper in the simulation.

The most important short-term priorities are:

- Make the gripper motion feel intuitive and smooth.
- Keep the tool visible in the camera view.
- Prevent exaggerated left/right motion that causes clipping or breaks immersion.
- Preserve enough responsiveness that the simulated tool still feels tied to the physical hardware.

## Relevant Scripts

- `Assets/KinematicsFollower.cs`
  - Main runtime follower for applying arm and wrist motion to the simulated tool.
  - Uses either live USB-fed joint data or mock test sliders.
- `Assets/Scripts/Gripper/TransformKinematics.cs`
  - Alternate direct-hierarchy joint driving approach for the digital twin skeleton.
- `Assets/Grabber.cs`
  - Handles interactive claw/grab behavior.
- `Assets/MouseFollower.cs`
  - Utility script for mouse-based transform following.

## Important Design Note

For `KinematicsFollower`, joint behavior is being tuned for visual usability rather than strict physical correctness.

In particular:

- Joint 1 should not simply rotate the entire gripper left/right.
- Instead, it should create a small horizontal drift.
- As the drift increases, the gripper should also turn more in that same direction.
- The horizontal motion must remain clamped so the tool does not move too far left or right and clip into the camera field of view.

## Near-Term Development Direction

- Continue refining per-joint motion mappings.
- Tune multipliers and clamps in-editor using mock data first.
- Validate against live USB telemetry once the visual behavior feels stable.
- Favor readable, adjustable mappings over hard-to-debug kinematic complexity for now.
