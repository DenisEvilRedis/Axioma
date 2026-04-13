using System.Collections.Generic;
using UnityEngine;

public sealed class VNScene : MonoBehaviour
{
    [SerializeField] private string sceneId = "scene";
    [SerializeField] private List<VNManager.BeatData> beats = new List<VNManager.BeatData>();

    public string SceneId => sceneId;
    public IReadOnlyList<VNManager.BeatData> Beats => beats;

    private void OnValidate()
    {
        if (beats == null)
        {
            beats = new List<VNManager.BeatData>();
        }
    }
}
