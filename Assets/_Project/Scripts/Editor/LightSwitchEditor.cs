using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightSwitch))]
public class LightSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Các nút Debug bỏ qua giới hạn gas để kiểm tra trực tiếp độ sáng.",
            MessageType.None);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Nhấn Play để bật các nút kiểm tra ánh sáng.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            LightSwitch lightSwitch = (LightSwitch)target;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Turn On"))
                lightSwitch.DebugTurnLightOn();

            if (GUILayout.Button("Turn Off"))
                lightSwitch.DebugTurnLightOff();

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Toggle Light"))
                lightSwitch.DebugToggleLight();
        }
    }
}
