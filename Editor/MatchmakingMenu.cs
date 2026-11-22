using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sdx17.Matchmaking.Editor
{
    /// <summary>Menu helpers under Sdx17 &gt; Matchmaking.</summary>
    public static class MatchmakingMenu
    {
        [MenuItem("Sdx17/Matchmaking/Create Matchmaking Config")]
        public static void CreateMatchmakingConfig()
        {
            var config = ScriptableObject.CreateInstance<MatchmakingConfig>();

            const string folder = "Assets/Sdx17/Matchmaking";
            if (!AssetDatabase.IsValidFolder("Assets/Sdx17"))
                AssetDatabase.CreateFolder("Assets", "Sdx17");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Sdx17", "Matchmaking");

            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, "MatchmakingConfig.asset"));
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            Debug.Log($"[Sdx17 Matchmaking] Created MatchmakingConfig at {path}. Assign it to a MatchmakingService.");
        }

        [MenuItem("Sdx17/Matchmaking/Add Matchmaking Service to Scene")]
        public static void AddMatchmakingServiceToScene()
        {
            var go = new GameObject("MatchmakingService");
            go.AddComponent<MatchmakingService>();
            Selection.activeObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Add Matchmaking Service");
            Debug.Log("[Sdx17 Matchmaking] Added MatchmakingService to the scene. Assign a MatchmakingConfig and set LocalPlayer at runtime.");
        }
    }
}
