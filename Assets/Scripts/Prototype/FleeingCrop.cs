using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FleeingCrop : MonoBehaviour
    {
        private FarmPrototype world;
        private FarmPlot plot;
        private ItemKind sourceKind;
        private int generation;
        private int baseValue;
        private string customPriceId;
        private string harvestPriceId;
        private string growthPriceId;
        private float growthTime;
        private float growthProgress;
        private bool randomPlant;
        private float rolledSize;
        private string plantDisplayName;
        public float RolledSize => rolledSize;
        public string DisplayName => randomPlant ? plantDisplayName : $"Crop +{generation}";
        private float health;
        private float maxHealth;
        private float nextFleeDirectionTime;
        private Vector3 fleeDirection;
        private Renderer[] visuals;
        private readonly System.Collections.Generic.List<Material> ownedMaterials = new();
        private Collider[] bodies;
        private Vector3 initialScale;
        private Vector3 originalItemScale;
        private float baseOffset;
        private bool removed;
        private PlantableCorpse corpse;
        private GrowableTool growableTool;
        private float groundHeight;
        private float combatClock, nextAttack, attackRemaining = -1;
        private float attackRecoveryRemaining;
        [SerializeField] private PlantGrowthProfile growthProfile;
        [SerializeField] private float growthRatePercent = 100;
        public PlantGrowthProfile GrowthProfile => growthProfile;
        public float GrowthRatePercent => growthRatePercent;
        public bool IsPlanted => !removed && plot != null && plot.Contains(this);

        public bool IsMature => growthProgress >= growthTime;
        public bool IsWatered => plot != null && plot.IsWatered;
        public FarmPlot Plot => plot;
        public int Generation => generation;
        public int Value => randomPlant ? ItemPriceCatalog.SaleBySize(harvestPriceId, baseValue, CurrentPlantSize) : ItemPriceCatalog.Sale(harvestPriceId, generation, baseValue);
        private float CurrentPlantSize => transform.localScale.x / Mathf.Max(.0001f, initialScale.x);
        public void ConfigureRandomPlant(RandomPlantCatalog.Plant plant)
        {
            randomPlant = true;
            plantDisplayName = plant.displayName;
            customPriceId = harvestPriceId = growthPriceId = plant.priceId;
            originalItemScale = initialScale;
            rolledSize = ItemPriceCatalog.RollPlantSize(plant.priceId);
            ApplyPlantSize(.15f);
            gameObject.name = $"Growing {plantDisplayName}";
        }
        private void ApplyPlantSize(float size)
        {
            transform.localScale = initialScale * size;
            Vector3 position = transform.position; position.y = groundHeight + baseOffset * size; transform.position = position;
        }
        public float Health => health;
        public float MaxHealth => maxHealth;

        public void Configure(FarmPrototype prototype, FarmItem source, FarmPlot homePlot, float soilHeight)
        {
            world = prototype;
            corpse = GetComponent<PlantableCorpse>();
            growableTool = GetComponent<GrowableTool>();
            plot = homePlot;
            groundHeight = soilHeight;
            initialScale = transform.localScale;
            originalItemScale = source.OriginalScale;
            bodies = System.Array.FindAll(GetComponentsInChildren<Collider>(), body => body.enabled);
            sourceKind = source.Kind;
            generation = source.Generation;
            baseValue = source.BaseValue;
            customPriceId = source.CustomPriceId;
            growthPriceId = source.PriceId;
            harvestPriceId = !string.IsNullOrWhiteSpace(customPriceId) ? customPriceId :
                growableTool != null ? "shovel" : sourceKind == ItemKind.Seed ? "produce" : source.PriceId;
            growthProfile = corpse == null ? source.GrowthProfile : null;
            growthTime = growthProfile != null
                ? Mathf.Max(.1f, growthProfile.growthSeconds + generation * growthProfile.secondsPerGeneration)
                : Mathf.Max(1.5f, 4f + generation);
            maxHealth = 3f + generation * 2f;
            health = maxHealth;
            visuals = GetComponentsInChildren<Renderer>();
            if (corpse == null)
                foreach (Renderer renderer in visuals) ownedMaterials.AddRange(renderer.sharedMaterials);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in visuals)
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found)
                foreach (Collider body in bodies)
                {
                    if (!found) { bounds = body.bounds; found = true; }
                    else bounds.Encapsulate(body.bounds);
                }
            baseOffset = transform.position.y - bounds.min.y;
            Vector3 position = transform.position;
            position.y = groundHeight + baseOffset;
            transform.position = position;
            if (growableTool != null && corpse == null) growableTool.RiseFromSoil(groundHeight, 0);
            gameObject.name = corpse != null ? $"Recovering {source.DisplayName}" : $"Growing {source.DisplayName}";
        }

        private void Update()
        {
            if (removed || corpse != null) return;
            if (!IsMature)
            {
                Grow(Time.deltaTime);
                return;
            }

            if (growableTool != null) { TickCombat(Time.deltaTime); return; }
            Vector3 away = transform.position - world.PlayerPosition;
            away.y = 0f;
            if (away.sqrMagnitude > 36f || away.sqrMagnitude < 0.01f)
                return;

            if (Time.time >= nextFleeDirectionTime)
            {
                fleeDirection = (away.normalized + new Vector3(Random.Range(-0.35f, 0.35f), 0f,
                    Random.Range(-0.35f, 0.35f))).normalized;
                nextFleeDirectionTime = Time.time + 0.65f;
            }

            Vector3 next = transform.position + fleeDirection * (2f + generation * 0.35f) * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, -world.ArenaHalfSize, world.ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -world.ArenaHalfSize, world.ArenaHalfSize);
            transform.position = next;
            transform.Rotate(Vector3.up, 95f * Time.deltaTime, Space.World);
        }

        private bool ClearCombatPath(Vector3 from, Vector3 to, LocalFarmer player)
        {
            Vector3 difference = to-from;
            foreach(var hit in Physics.RaycastAll(from,difference.normalized,difference.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(player.transform)) return false;
            return true;
        }
        public void TickCombat(float elapsed)
        {
            if(elapsed<=0 || removed || corpse!=null || growableTool==null || !IsMature || world==null || world.Player==null) return;
            combatClock+=elapsed;
            var player=world.Player;
            Vector3 toward=player.transform.position-transform.position; toward.y=0;
            Quaternion facing=toward.sqrMagnitude>.001f ? Quaternion.LookRotation(toward) : Quaternion.Euler(0,transform.eulerAngles.y,0);
            if(player.IsDead || toward.magnitude>growableTool.AggroRange)
            { attackRemaining=-1; attackRecoveryRemaining=0; transform.rotation=facing; return; }
            if(attackRecoveryRemaining>0)
            {
                attackRecoveryRemaining=Mathf.Max(0,attackRecoveryRemaining-elapsed);
                float recover=1-attackRecoveryRemaining/growableTool.AttackRecovery;
                transform.rotation=facing*Quaternion.Euler(Vector3.Lerp(new Vector3(55,65,-20),Vector3.zero,Mathf.SmoothStep(0,1,recover)));
                if(attackRecoveryRemaining>0) return;
            }
            if(attackRemaining>=0)
            {
                attackRemaining-=elapsed;
                float progress=1-Mathf.Clamp01(attackRemaining/growableTool.AttackWindup);
                transform.rotation=facing*Quaternion.Euler(Vector3.Lerp(Vector3.zero,new Vector3(-30,-45,15),Mathf.SmoothStep(0,1,progress)));
                if(attackRemaining<=0)
                {
                    attackRemaining=-1; nextAttack=combatClock+growableTool.AttackInterval;
                    attackRecoveryRemaining=growableTool.AttackRecovery;
                    transform.rotation=facing*Quaternion.Euler(55,65,-20);
                    Physics.SyncTransforms();
                    if(toward.magnitude<=growableTool.AttackRange && ClearCombatPath(transform.position+Vector3.up*.4f,player.transform.position+Vector3.up*.75f,player))
                        player.ReceiveDamage(growableTool.AttackDamage);
                }
                return;
            }
            transform.rotation=facing;
            if(toward.magnitude>growableTool.AttackRange*.8f)
            {
                float step=Mathf.Min(growableTool.ChaseSpeed*elapsed,toward.magnitude-growableTool.AttackRange*.8f);
                Vector3 next=transform.position+toward.normalized*step;
                if(ClearCombatPath(transform.position+Vector3.up*.4f,next+Vector3.up*.4f,player)) transform.position=next;
            }
            if(toward.magnitude<=growableTool.AttackRange && combatClock>=nextAttack)
                attackRemaining=growableTool.AttackWindup;
        }

        public void Grow(float elapsed)
        {
            if (corpse != null || !IsPlanted || IsMature || elapsed <= 0) return;
            growthRatePercent = growthProfile != null
                ? growthProfile.Evaluate(plot.GetLight(transform.position, transform), plot.WaterAmount, plot.SoilType)
                : 100;
            if ((growthProfile == null || growthProfile.requireWaterToStart) && !plot.GrowthStarted) return;
            float seconds = ItemPriceCatalog.GrowthSeconds(growthPriceId, growthTime);
            growthProgress = Mathf.Min(growthTime, growthProgress + elapsed * growthTime / seconds);
            float baseSize = growthProfile != null
                ? growthProfile.matureSizeMultiplier + generation * growthProfile.sizePerGeneration
                : 2f + generation * .32f;
            float finalSize = randomPlant ? rolledSize : Mathf.Max(1, baseSize * growthRatePercent / 100);
            float size = Mathf.Lerp(randomPlant ? .15f : 1f, finalSize, growthProgress / growthTime);
            if (growableTool != null)
            {
                transform.localScale = initialScale;
                growableTool.RiseFromSoil(groundHeight, growthProgress / growthTime);
            }
            else
            {
                ApplyPlantSize(size);
            }
            if (IsMature)
            {
                gameObject.name = randomPlant ? $"Mature {plantDisplayName}" : $"Mature crop +{generation}";
                ReleaseSoil();
            }
        }

        private void ReleaseSoil()
        {
            if (plot != null) plot.Clear(this);
            plot = null;
        }
        public void FinishCorpseRecovery()
        {
            if (corpse == null) return;
            removed = true; ReleaseSoil(); Destroy(this);
        }

        private void OnDestroy()
        {
            ReleaseSoil();
            foreach (Material material in ownedMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        public void TakeHit(float damage)
        {
            if (corpse != null) { world.SetMessage($"Recovering: {corpse.Health:0}/{corpse.MaxHealth:0} HP"); return; }
            if (removed) return;
            if (!IsMature)
            {
                world.SetMessage("Wait until the crop is fully grown.");
                return;
            }

            health -= damage;
            if (health > 0f)
            {
                world.SetMessage($"Crop hit! {Mathf.CeilToInt(health)}/{Mathf.CeilToInt(maxHealth)} HP");
                return;
            }

            ItemKind resultKind = sourceKind == ItemKind.Seed ? ItemKind.Produce : sourceKind;
            // Turn the grown model into a pickup so its size, mesh and child transforms survive harvest.
            removed = true;
            ReleaseSoil();
            FarmItem harvested = gameObject.AddComponent<FarmItem>();
            harvested.Configure(resultKind, generation, baseValue);
            harvested.SetPriceId(customPriceId);
            if (randomPlant) harvested.SetPlantIdentity(plantDisplayName);
            harvested.SetOriginalScale(originalItemScale);
            harvested.SetGrowthProfile(growthProfile);
            harvested.MarkPlanted();
            harvested.OwnMaterials(ownedMaterials);
            ownedMaterials.Clear();
            transform.position += Vector3.up * .5f;
            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var collider in bodies) collider.enabled = true;
            world.SetMessage("Harvested! Use or sell it. Each item can only be planted once.");
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }
    }
}
