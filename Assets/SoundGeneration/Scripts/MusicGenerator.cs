using UnityEngine;
using System.Collections.Generic;

public class MusicGenerator : MonoBehaviour
{
    public BeatClock clock;
    public List<MusicLoop> loops = new();

    void OnEnable()
    {
        foreach (var l in loops)
        {
            if (!l) continue;
            l.enabled = true; // OnEnable schedules immediately
        }
    }
}
