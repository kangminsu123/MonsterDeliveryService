using UnityEngine;

public sealed class SlimeSlowEffect : MonoBehaviour
{
    readonly Transform[] drips = new Transform[10];
    readonly Transform[] patches = new Transform[6];
    void Awake()
    {
        for (int i = 0; i < patches.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / patches.Length;
            var p = new Vector3(Mathf.Cos(angle) * .51f, .64f + .18f * Mathf.Sin(i * 2), Mathf.Sin(angle) * .51f);
            patches[i] = SlimeGoo.Blob(transform, "StickyCoating", p, new Vector3(.45f, .35f, .22f));
            patches[i].localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
            SlimeGoo.Blob(patches[i], "WetHighlight", new Vector3(-.13f, .15f, -.4f), new Vector3(.19f, .14f, .12f), true);
        }
        for (int i = 0; i < drips.Length; i++)
            drips[i] = SlimeGoo.Blob(transform, "FallingGoo", Vector3.zero, new Vector3(.13f, .3f, .13f));
    }
    void Update()
    {
        // The actor floats above the street: coat the torso and stretch the goo below it.
        for (int i = 0; i < drips.Length; i++)
        {
            float phase = Mathf.Repeat(Time.time * .65f + i * .171f, 1);
            float angle = i * 2.39996f;
            float radius = .62f + .07f * Mathf.Sin(Time.time * 3 + i);
            drips[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, .85f - phase * 1.6f, Mathf.Sin(angle) * radius);
            drips[i].localScale = new Vector3(.13f, .2f + phase * .25f, .13f) * Mathf.Min(1, (1 - phase) * 6);
        }
        for (int i = 0; i < patches.Length; i++)
        {
            float s = 1 + .09f * Mathf.Sin(Time.time * 4 + i);
            patches[i].localScale = new Vector3(.45f * s, .35f / s, .22f);
        }
    }
}
