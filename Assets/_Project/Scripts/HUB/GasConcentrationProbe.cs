using UnityEngine;

public class GasConcentrationProbe : MonoBehaviour
{
    public GasSystem gas;
    public ParticleSystem gasFog;   // <-- thêm cái này
    public Vector2 pos = new Vector2(20, 20);
    public int fontSize = 28;

    void Awake()
    {
        if (!gas) gas = FindFirstObjectByType<GasSystem>();
        if (!gasFog) gasFog = GameObject.Find("GasFogPS")?.GetComponent<ParticleSystem>();
    }

    void OnGUI()
    {
        if (!gas) return;

        float em = -1f;
        int pCount = -1;

        if (gasFog)
        {
            em = gasFog.emission.rateOverTime.constant; // ok nếu rateOverTime là constant
            pCount = gasFog.particleCount;
        }

        string text =
            $"Gas01: {gas.gas01:0.00}  (L{gas.GasLevel()})\n" +
            $"Vent01: {gas.vent01:0.00}  Leak: {gas.leakActive}\n" +
            $"Emission: {em:0}  Particles: {pCount}";

        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        var rect = new Rect(pos.x, pos.y, 520, 95);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(rect, text, style);
    }
}