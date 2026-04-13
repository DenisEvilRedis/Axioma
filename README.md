# Axioma

**Axioma** is a visual novel built with Unity as part of the **MIP-19** academic course project.

The project focuses on interactive storytelling: scene-based narrative flow, player choices, branching logic, and multiple endings. The story and most gameplay behavior are configured directly inside the Unity scene and Inspector rather than through an external JSON pipeline.

## Project Overview

- Genre: visual novel
- Engine: Unity 6
- Project type: educational course project
- Core gameplay: dialogue, choices, branching, ending logic, UI-driven narrative flow

## Tech Stack

- Unity: `6000.4.1f1`
- Render Pipeline: `URP`
- UI: `UGUI`
- Input: `com.unity.inputsystem`
- Additional packages:
  - `com.unity.ai.navigation`
  - `com.unity.timeline`
  - `com.unity.visualscripting`

## Opening The Project

1. Open **Unity Hub**
2. Add the project folder:
   `C:\Users\Denis\Unity Project\Axioma`
3. Open it with Unity version `6000.4.1f1`

Main working scene:

`Assets/Scenes/StartNV.unity`

## Repository Structure

- `Assets/Scenes` — project scenes
- `Assets/Scripts` — core gameplay and UI logic
- `Assets/Editor` — editor tools and custom inspector helpers
- `Packages` — Unity package configuration
- `ProjectSettings` — Unity project settings
- `Fonts` — project font assets

Generated folders such as `Library`, `Logs`, `obj`, `Build`, `.vs`, and temporary local output are excluded from Git.

## Key Scripts

- `VNManager.cs` — main visual novel controller
- `VNScene.cs` — serializable story scene data
- `VNDialogAuto.cs` — auto-reading mode for dialogue
- `MenuManager.cs` — menu, overlays, and navigation screens
- `AudioManager.cs` — audio playback and sound categories
- `MusicManager.cs` — background music management
- `EndingSequencePlayer.cs` — ending playback logic
- `EndingScoreDebugOverlay.cs` — debug overlay for ending score tracking

## Narrative Structure

The story is built from serialized scene data managed by `VNManager`. Core beat types include:

- dialogue beats
- player choices
- logic branches
- ending nodes

The project also tracks route-related values, flags, and unlocked endings during progression.

## Notes

- The main production scene is `StartNV.unity`
- `SampleScene.unity` is not the primary gameplay scene
- Many story changes are made through the Unity Inspector, so updates may affect scene files in addition to C# scripts

## Status

This repository contains the source code and project configuration for the Unity version of **Axioma**.
