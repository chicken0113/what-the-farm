using UnityEditor;
using UnityEngine;
public static class UnityMigrationBatchCheck
{
    public static void PlayExisting()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        UnityMigrationPlayCheck.Run();
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Use the live editor validation menu.");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        UnityMigrationBuilder.Install();
        UnityMigrationPlayCheck.Run();
    }
}
