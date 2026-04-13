using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VNEditorUtility
{
    public readonly struct BeatReference
    {
        public BeatReference(string sceneId, string beatId)
        {
            SceneId = sceneId ?? string.Empty;
            BeatId = beatId ?? string.Empty;
        }

        public string SceneId { get; }
        public string BeatId { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(SceneId) ? BeatId : $"{SceneId}/{BeatId}";
    }

    public sealed class GraphValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;
    }

    public static List<BeatReference> CollectBeatReferences(SerializedObject serializedObject)
    {
        var result = new List<BeatReference>();
        if (serializedObject == null || serializedObject.targetObject == null)
        {
            return result;
        }

        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (VNScene scene in EnumerateContextScenes(serializedObject))
        {
            if (scene == null || scene.Beats == null)
            {
                continue;
            }

            string sceneId = scene.SceneId;
            for (int i = 0; i < scene.Beats.Count; i++)
            {
                VNManager.BeatData beat = scene.Beats[i];
                if (beat == null || string.IsNullOrWhiteSpace(beat.beatId))
                {
                    continue;
                }

                string beatId = beat.beatId.Trim();
                if (!unique.Add(beatId))
                {
                    continue;
                }

                result.Add(new BeatReference(sceneId, beatId));
            }
        }

        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static bool BeatExists(SerializedObject serializedObject, string beatId)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(beatId))
        {
            return false;
        }

        string targetId = beatId.Trim();
        List<BeatReference> beats = CollectBeatReferences(serializedObject);
        for (int i = 0; i < beats.Count; i++)
        {
            if (string.Equals(beats[i].BeatId, targetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static GraphValidationResult ValidateGraph(SerializedObject serializedObject)
    {
        GraphValidationResult result = new GraphValidationResult();
        if (serializedObject == null)
        {
            result.Errors.Add("SerializedObject is null.");
            return result;
        }

        SerializedProperty startBeatProperty = serializedObject.FindProperty("startBeatId");
        string startBeatId = startBeatProperty?.stringValue?.Trim() ?? string.Empty;

        List<VNScene> storyScenes = new List<VNScene>();
        SerializedProperty storyScenesProperty = serializedObject.FindProperty("storyScenes");
        if (storyScenesProperty != null && storyScenesProperty.isArray)
        {
            for (int i = 0; i < storyScenesProperty.arraySize; i++)
            {
                VNScene scene = storyScenesProperty.GetArrayElementAtIndex(i).objectReferenceValue as VNScene;
                if (scene != null)
                {
                    storyScenes.Add(scene);
                }
            }
        }

        ValidateGraph(startBeatId, storyScenes, result);
        return result;
    }

    public static void ValidateGraph(string startBeatId, IList<VNScene> storyScenes, GraphValidationResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        Dictionary<string, BeatNode> beats = new Dictionary<string, BeatNode>(StringComparer.Ordinal);
        Dictionary<string, List<string>> edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (int sceneIndex = 0; sceneIndex < storyScenes.Count; sceneIndex++)
        {
            VNScene scene = storyScenes[sceneIndex];
            if (scene == null)
            {
                result.Warnings.Add($"storyScenes[{sceneIndex}] is null.");
                continue;
            }

            IReadOnlyList<VNManager.BeatData> sceneBeats = scene.Beats;
            if (sceneBeats == null)
            {
                result.Warnings.Add($"Scene '{scene.SceneId}' has no beats list.");
                continue;
            }

            for (int beatIndex = 0; beatIndex < sceneBeats.Count; beatIndex++)
            {
                VNManager.BeatData beat = sceneBeats[beatIndex];
                if (beat == null)
                {
                    result.Warnings.Add($"Scene '{scene.SceneId}' has null beat at index {beatIndex}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(beat.beatId))
                {
                    result.Errors.Add($"Scene '{scene.SceneId}' has beat with empty beatId at index {beatIndex}.");
                    continue;
                }

                string beatId = beat.beatId.Trim();
                if (beats.ContainsKey(beatId))
                {
                    result.Errors.Add($"Duplicate beatId '{beatId}' in scene '{scene.SceneId}'.");
                    continue;
                }

                beats.Add(beatId, new BeatNode(scene.SceneId, beatId, beat));
                edges.Add(beatId, new List<string>());
            }
        }

        if (string.IsNullOrWhiteSpace(startBeatId))
        {
            result.Errors.Add("startBeatId is empty.");
        }
        else if (!beats.ContainsKey(startBeatId))
        {
            result.Errors.Add($"startBeatId '{startBeatId}' does not exist.");
        }

        foreach (BeatNode node in beats.Values)
        {
            ValidateBeat(node, beats, edges[node.BeatId], result);
        }

        if (!string.IsNullOrWhiteSpace(startBeatId) && beats.ContainsKey(startBeatId))
        {
            HashSet<string> visited = TraverseReachable(startBeatId, edges);
            foreach (string beatId in beats.Keys)
            {
                if (!visited.Contains(beatId))
                {
                    result.Warnings.Add($"Beat '{beatId}' is unreachable from startBeatId '{startBeatId}'.");
                }
            }
        }
    }

    private static void ValidateBeat(
        BeatNode node,
        Dictionary<string, BeatNode> beats,
        List<string> outgoing,
        GraphValidationResult result)
    {
        VNManager.BeatData beat = node.Beat;
        switch (beat.mode)
        {
            case VNManager.BeatMode.Dialogue:
                if (string.IsNullOrWhiteSpace(beat.nextBeatId))
                {
                    result.Errors.Add($"Dialogue beat '{node.BeatId}' has empty nextBeatId.");
                }
                else
                {
                    RegisterEdge(node, beat.nextBeatId, beats, outgoing, result, "nextBeatId");
                }

                if (beat.branchRules != null && beat.branchRules.Length > 0)
                {
                    result.Warnings.Add($"Dialogue beat '{node.BeatId}' contains branchRules.");
                }

                if (beat.choices != null && beat.choices.Length > 0)
                {
                    result.Warnings.Add($"Dialogue beat '{node.BeatId}' contains choices.");
                }
                break;

            case VNManager.BeatMode.Choice:
                if (beat.choices == null || beat.choices.Length == 0)
                {
                    result.Errors.Add($"Choice beat '{node.BeatId}' has no choices.");
                }
                else
                {
                    for (int i = 0; i < beat.choices.Length; i++)
                    {
                        VNManager.ChoiceData choice = beat.choices[i];
                        if (choice == null)
                        {
                            result.Errors.Add($"Choice beat '{node.BeatId}' has null choice at index {i}.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(choice.nextBeatId))
                        {
                            result.Errors.Add($"Choice beat '{node.BeatId}' has choice #{i + 1} with empty nextBeatId.");
                            continue;
                        }

                        RegisterEdge(node, choice.nextBeatId, beats, outgoing, result, $"choices[{i}].nextBeatId");
                    }
                }

                if (!string.IsNullOrWhiteSpace(beat.defaultNextBeatId))
                {
                    result.Warnings.Add($"Choice beat '{node.BeatId}' has unused defaultNextBeatId.");
                }
                break;

            case VNManager.BeatMode.Branch:
                bool hasBranchTarget = false;
                if (beat.branchRules != null)
                {
                    for (int i = 0; i < beat.branchRules.Length; i++)
                    {
                        VNManager.BranchRuleData rule = beat.branchRules[i];
                        if (rule == null)
                        {
                            result.Errors.Add($"Branch beat '{node.BeatId}' has null branch rule at index {i}.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(rule.nextBeatId))
                        {
                            result.Errors.Add($"Branch beat '{node.BeatId}' has branch rule #{i + 1} with empty nextBeatId.");
                            continue;
                        }

                        hasBranchTarget = true;
                        RegisterEdge(node, rule.nextBeatId, beats, outgoing, result, $"branchRules[{i}].nextBeatId");
                    }
                }

                if (!string.IsNullOrWhiteSpace(beat.defaultNextBeatId))
                {
                    hasBranchTarget = true;
                    RegisterEdge(node, beat.defaultNextBeatId, beats, outgoing, result, "defaultNextBeatId");
                }

                if (!hasBranchTarget)
                {
                    result.Errors.Add($"Branch beat '{node.BeatId}' has no outgoing branches.");
                }
                break;

            case VNManager.BeatMode.Ending:
                if (beat.endingToUnlock == VNManager.EndingType.None)
                {
                    result.Warnings.Add($"Ending beat '{node.BeatId}' has endingToUnlock = None.");
                }

                if (!string.IsNullOrWhiteSpace(beat.nextBeatId))
                {
                    result.Warnings.Add($"Ending beat '{node.BeatId}' has unused nextBeatId.");
                }

                if (!string.IsNullOrWhiteSpace(beat.defaultNextBeatId))
                {
                    result.Warnings.Add($"Ending beat '{node.BeatId}' has unused defaultNextBeatId.");
                }
                break;

            default:
                result.Errors.Add($"Beat '{node.BeatId}' uses unsupported mode '{beat.mode}'.");
                break;
        }
    }

    private static void RegisterEdge(
        BeatNode source,
        string targetBeatId,
        Dictionary<string, BeatNode> beats,
        List<string> outgoing,
        GraphValidationResult result,
        string fieldName)
    {
        string target = targetBeatId.Trim();
        outgoing.Add(target);

        if (!beats.ContainsKey(target))
        {
            result.Errors.Add($"Beat '{source.BeatId}' has invalid {fieldName} -> '{target}'.");
        }
    }

    private static HashSet<string> TraverseReachable(string startBeatId, Dictionary<string, List<string>> edges)
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        Stack<string> stack = new Stack<string>();
        stack.Push(startBeatId);

        while (stack.Count > 0)
        {
            string current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (!edges.TryGetValue(current, out List<string> targets))
            {
                continue;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!visited.Contains(targets[i]))
                {
                    stack.Push(targets[i]);
                }
            }
        }

        return visited;
    }

    private static IEnumerable<VNScene> EnumerateContextScenes(SerializedObject serializedObject)
    {
        HashSet<VNScene> scenes = new HashSet<VNScene>();

        SerializedProperty storyScenesProperty = serializedObject.FindProperty("storyScenes");
        if (storyScenesProperty != null && storyScenesProperty.isArray)
        {
            for (int i = 0; i < storyScenesProperty.arraySize; i++)
            {
                VNScene scene = storyScenesProperty.GetArrayElementAtIndex(i).objectReferenceValue as VNScene;
                if (scene != null)
                {
                    scenes.Add(scene);
                }
            }
        }

        if (scenes.Count > 0)
        {
            foreach (VNScene scene in scenes)
            {
                yield return scene;
            }

            yield break;
        }

        Component component = serializedObject.targetObject as Component;
        if (component == null)
        {
            yield break;
        }

        Scene unityScene = component.gameObject.scene;
        VNScene[] allScenes = Resources.FindObjectsOfTypeAll<VNScene>();
        for (int i = 0; i < allScenes.Length; i++)
        {
            VNScene scene = allScenes[i];
            if (scene == null || EditorUtility.IsPersistent(scene))
            {
                continue;
            }

            if (scene.gameObject.scene == unityScene && scenes.Add(scene))
            {
                yield return scene;
            }
        }
    }

    private readonly struct BeatNode
    {
        public BeatNode(string sceneId, string beatId, VNManager.BeatData beat)
        {
            SceneId = sceneId;
            BeatId = beatId;
            Beat = beat;
        }

        public string SceneId { get; }
        public string BeatId { get; }
        public VNManager.BeatData Beat { get; }
    }
}
