using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class NpcMerchant : MonoBehaviour
    {
        [SerializeField] private string displayName = "Farm Buyer";
        [SerializeField, TextArea] private string[] dialogue =
        {
            "Welcome! I buy harvested crops and other items.",
            "Aim at me and press Q to throw your selected item. I'll pay its value in gold."
        };
        [SerializeField, Min(0)] private float saleMultiplier = 1f;
        [SerializeField, Min(1)] private float dialogueSeconds = 6f;
        [SerializeField] private FarmPrototype world;
        private int nextLine;
        private FarmGuardian combat;
        public bool IsHostile => combat != null && combat.IsHostile;
        public bool IsDefeated => combat != null && combat.IsDefeated;
        public void BindCombat(FarmGuardian guardian) => combat = guardian;
        public FarmGuardian EnsureCombat(FarmPrototype farm, FarmFirstStage encounter = null)
        {
            BindWorld(farm);
            if (combat == null) combat = GetComponent<FarmGuardian>();
            if (combat == null) combat = gameObject.AddComponent<FarmGuardian>();
            combat.Configure(farm, encounter, false);
            return combat;
        }
        private void Start() { if (FindWorld()) EnsureCombat(world); }

        public string DisplayName => displayName;
        public void BindWorld(FarmPrototype prototype) => world = prototype;

        private bool FindWorld()
        {
            if (world == null) world = FindFirstObjectByType<FarmPrototype>();
            return world != null;
        }

        public void Talk()
        {
            if (!FindWorld()) return;
            if (IsDefeated) return;
            if (IsHostile) { world.SetMessage("The hostile merchant will not trade or talk."); return; }
            string line = dialogue != null && dialogue.Length > 0
                ? dialogue[nextLine++ % dialogue.Length]
                : "Throw an item to me with Q to sell it.";
            world.ShowDialogue(displayName, line, dialogueSeconds);
        }

        public bool TrySell(FarmItem item)
        {
            if (item == null || IsHostile || IsDefeated || !FindWorld()) return false;
            int price = Mathf.Max(0, Mathf.RoundToInt(item.Value * saleMultiplier));
            if (price == 0 || !item.ClaimSale()) return false;
            world.AddGold(price);
            world.ShowDialogue(displayName, $"Bought {item.DisplayName} for {price} gold. Thank you!", dialogueSeconds);
            item.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(item.gameObject);
            else DestroyImmediate(item.gameObject);
            return true;
        }

        private void OnTriggerEnter(Collider other) => Receive(other);
        private void OnTriggerStay(Collider other) => Receive(other);
        private void Receive(Collider other) => TrySell(other.GetComponentInParent<FarmItem>());
    }
}
