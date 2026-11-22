using UnityEditor;
using UnityEngine;

namespace Sdx17.Matchmaking.Editor
{
    /// <summary>Custom inspector for <see cref="MatchmakingConfig"/>: draws the fields plus live validation warnings so bad values are caught in the editor, not in a match.</summary>
    [CustomEditor(typeof(MatchmakingConfig))]
    public class MatchmakingConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (MatchmakingConfig)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (config.MinPlayersPerMatch > config.MaxPlayersPerMatch)
                EditorGUILayout.HelpBox("Min players per match is larger than max players per match. No match can ever form.", MessageType.Error);

            if (config.SkillTolerance > config.MaxSkillTolerance)
                EditorGUILayout.HelpBox("Skill tolerance is larger than max skill tolerance, so the tolerance can never grow past its starting value.", MessageType.Warning);

            if (config.QueueTimeoutSeconds < config.ReadyCheckTimeoutSeconds)
                EditorGUILayout.HelpBox("Queue timeout is shorter than the ready-check timeout. A match found late in the queue can time out during its own ready-check.", MessageType.Warning);

            if (!config.EnableReadyCheck)
                EditorGUILayout.HelpBox("Ready-check is disabled: matches start immediately when formed. Players who are away from keyboard will be pulled into games.", MessageType.Info);

            if (config.AllowedModes == null || config.AllowedModes.Length == 0)
                EditorGUILayout.HelpBox("No modes are allowed. Queueing will always fail.", MessageType.Error);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Effective tolerance after 30s wait: {config.ToleranceForWait(30f):F0} rating points.",
                EditorStyles.miniLabel);
        }
    }
}
