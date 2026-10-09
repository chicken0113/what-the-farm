using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmStageExit : MonoBehaviour
    {
        [SerializeField] private string nextScene = "StageTwo";
        [SerializeField] private bool requireFirstStageClear = true;
        [SerializeField] private FarmFirstStage firstStage;
        public bool CanTravel => !string.IsNullOrEmpty(nextScene) && (!requireFirstStageClear || (firstStage != null && firstStage.Cleared));
        public void Configure(FarmFirstStage stage) => firstStage = stage;
        public bool TryTravel(FarmPrototype world, LocalFarmer player)
        {
            if (!CanTravel) { world.SetMessage("Defeat the hostile merchant before entering the next stage."); return false; }
            if (!Application.CanStreamedLevelBeLoaded(nextScene)) { world.SetMessage("Next stage is missing from the build settings."); return false; }
            FarmTravel.Capture(world, player);
            SceneManager.LoadScene(nextScene);
            return true;
        }
    }
}
