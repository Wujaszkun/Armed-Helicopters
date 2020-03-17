using Facepunch;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Armed Helicopters", "Wujaszkun", "0.2.5")]
    [Description("Armament for scrap transport helicopter and Minicopter")]
    class ArmedHelicopters : RustPlugin
    {
        public static ArmedHelicopters instance;
        public ScrapTransportHelicopter copter;
        private List<Armament> armamentList = new List<Armament>();
        private List<MiniCopter> helicopterList = new List<MiniCopter>();
        private bool isLoggingEnabled = true;

        [ChatCommand("armtransportcopter")]
        void ArmTransportCopters(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                ReloadCopterInformation();
                ArmHelicopter();
            }
        }

        void OnServerInitialized()
        {
            instance = this;
            ReloadCopterInformation();
            ArmHelicopter();
        }

        void Unload()
        {
            foreach (var copter in GameObject.FindObjectsOfType<Armament>())
            {
                if (copter != null)
                {
                    copter.DespawnAllEntities();
                    GameObject.Destroy(copter);
                }
            }
        }

        private void ReloadCopterInformation()
        {
            helicopterList = new List<MiniCopter>(GameObject.FindObjectsOfType<MiniCopter>());
            armamentList = new List<Armament>(GameObject.FindObjectsOfType<Armament>());
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.gameObject.GetComponent<MiniCopter>() != null)
            {
                ReloadCopterInformation();
                ArmHelicopter();
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            ReloadCopterInformation();


        }

        private void Log(string message)
        {
            if (isLoggingEnabled)
            {
                Puts(message);
            }
        }

        private void ArmHelicopter()
        {
            foreach (var heliBaseEnt in helicopterList)
            {
                if (heliBaseEnt.GetComponent<MiniCopter>() != null && heliBaseEnt.GetComponent<Armament>() == null)
                {
                    heliBaseEnt.gameObject.AddComponent<Armament>();
                }
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            try
            {
                var copter = player.GetMountedVehicle().GetComponent<Armament>();
                copter.HelicopterInput(input, player);
            }
            catch { }
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            PrintToChat("Dismounted");
            if (!player.isMounted)
            {
                DestroyUI(player);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hud");

            CuiHelper.DestroyUi(player, "pitch");
            CuiHelper.DestroyUi(player, "roll");
            CuiHelper.DestroyUi(player, "yaw");
        }

        class Armament : MonoBehaviour
        {
            private static HelicopterType baseHeliType;
            private MiniCopter baseHelicopter;


            private Vector3 position;
            private Quaternion rotation;

            private BaseEntity wingLeft;
            private BaseEntity wingRight;


            private AutoTurret leftTurret = new AutoTurret();
            private AutoTurret rightTurret = new AutoTurret();
            private bool turretsSpawned;

            List<Vector3> Tubes = new List<Vector3>();
            List<BaseEntity> TubesEntities = new List<BaseEntity>();
            List<BaseEntity> TubesParents = new List<BaseEntity>();
            List<AutoTurret> Turrets = new List<AutoTurret>();
            private int currentTubeIndex;
            private BaseEntity backDoor;
            private StorageContainer storageContainer;

            private bool canFireRockets;
            private float nextActionTime;
            private float period = 1;

            private float lastShot;

            private Dictionary<string, string> gunList = new Dictionary<string, string>();

            private enum HelicopterType
            {
                Transport,
                Mini
            }

            private void Awake()
            {
                turretsSpawned = false;
                SetType();
                baseHelicopter = GetComponent<MiniCopter>();
                lastShot = Time.time;

                position = baseHelicopter.transform.position;
                rotation = baseHelicopter.transform.rotation;

                SpawnArmament();

                currentTubeIndex = 0;
                ChangeGun(index);
                instance.Puts("Initialized");
                instance.Puts(baseHeliType.ToString());
                instance.Puts(baseHelicopter.GetType().ToString());
            }

            private void SetType()
            {
                baseHeliType = GetComponent<ScrapTransportHelicopter>() != null ? HelicopterType.Transport : HelicopterType.Mini;
            }
            private void SpawnArmament()
            {
                switch (baseHeliType)
                {
                    case HelicopterType.Mini:
                        SpawnWings();
                        SpawnGuns();
                        break;

                    case HelicopterType.Transport:
                        SpawnRockets();
                        break;
                }
                ChangeGun(0);
            }

            void FixedUpdate()
            {

                //instance.PrintToChat($"================");
                //instance.PrintToChat($"Pitch: {NormalizeX().ToString("0.0")}");
                //instance.PrintToChat($"Roll: {NormalizeZ().ToString("0.0")}");
                //instance.PrintToChat($"Direction: {this.baseHelicopter.transform.rotation.eulerAngles.y.ToString()}");

                ShowIU("Pitch: " + NormalizeX().ToString("0.0"), 
                    "Roll: " + NormalizeZ().ToString("0.0"),
                    "Yaw: " + this.baseHelicopter.transform.rotation.eulerAngles.y.ToString(), baseHelicopter.GetDriver());

                try
                {
                    ResetReloadTime();
                }
                catch { }

                try
                {
                    if (storageContainer.inventory.itemList.Count == 0) canFireRockets = false;
                }
                catch { }

                try
                {
                    if (Time.time > nextActionTime)
                    {
                        nextActionTime = Time.time + period;
                        if (storageContainer.inventory.itemList.Count > 0) canFireRockets = true;
                    }
                }
                catch { }

                try
                {
                    switch (baseHeliType)
                    {
                        case HelicopterType.Mini:
                            try
                            {
                                foreach (var turret in Turrets)
                                {
                                    if (FindTarget(baseHelicopter.GetDriver()) != Vector3.zero)
                                    {
                                        KeepFacingFrontMini(turret, FindTarget(baseHelicopter.GetDriver()) - turret.muzzlePos.position);
                                    }
                                    else
                                    {
                                        KeepFacingFrontMini(turret, baseHelicopter.transform.forward);
                                    }
                                }
                            }
                            catch { }
                            break;

                        case HelicopterType.Transport:
                            try
                            {
                                foreach (var turret in Turrets)
                                {
                                    KeepFacingFrontTransport(turret);
                                }
                            }
                            catch { }
                            break;
                    }
                }
                catch { instance.Puts("Eyes not working"); }

                try
                {
                    ResetAmmo();
                }
                catch { }
            }

            private float NormalizeX()
            {
                var x = this.baseHelicopter.transform.rotation.eulerAngles.x;
                if (x < 90) x = -x;
                if (x > 270) x = 360 - x;
                return x;
            }
            private float NormalizeZ()
            {
                var z = this.baseHelicopter.transform.rotation.eulerAngles.z;
                if (z < 90) z = -z;
                if (z > 270) z = 360 - z;
                return z;
            }
            private void ResetReloadTime()
            {
                if (!canGunShoot)
                {
                    instance.PrintToChat((nextShootTime - Time.time).ToString());
                }

                if (!canGunShoot && Time.time >= nextShootTime)
                {
                    canGunShoot = true;
                }
            }

            public Dictionary<uint, string> spawnedEntityList = new Dictionary<uint, string>();
            public Dictionary<uint, BaseEntity> spawnedBaseEntityList = new Dictionary<uint, BaseEntity>();

            private void AddEntityToData(BaseEntity entity, Vector3 position)
            {
                if (!spawnedEntityList.ContainsKey(entity.net.ID))
                {
                    spawnedEntityList.Add(entity.net.ID, entity.ShortPrefabName);
                }

                if (!spawnedBaseEntityList.ContainsKey(entity.net.ID))
                {
                    spawnedBaseEntityList.Add(entity.net.ID, entity);
                }
            }
            public void DespawnAllEntities()
            {
                foreach (var entity in spawnedBaseEntityList)
                {
                    try
                    {
                        entity.Value.Kill();
                        spawnedEntityList.Remove(entity.Key);
                    }

                    catch (Exception e)
                    {
                        instance.Puts("Couldn't delete entity " + entity.Key + " " + e.ToString());
                    }
                }
            }

            public void HelicopterInput(InputState inputState, BasePlayer player)
            {
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    //FireTurretsRockets(player);
                    FireTurretsGuns(player);
                }

                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    //FireTurretsGuns(player);
                }

                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.WasJustPressed(BUTTON.RELOAD))
                {
                    if (index == gunList.Count)
                    {
                        index = 0;
                    }
                    else
                    {
                        index++;
                    }
                    ChangeGun(index);
                }
            }

            private ItemDefinition ChangeGun(int index)
            {
                gunList = new Dictionary<string, string>();

                gunList.Add("rifle.ak", "ammo.rifle");
                gunList.Add("rifle.bolt", "ammo.rifle.hv");
                gunList.Add("rifle.l96", "ammo.rifle.hv");
                gunList.Add("rifle.lr300", "ammo.rifle");
                gunList.Add("lmg.m249", "ammo.rifle");
                gunList.Add("rifle.m39", "ammo.rifle");
                gunList.Add("pistol.m92", "ammo.pistol");
                gunList.Add("smg.mp5", "ammo.pistol");
                gunList.Add("pistol.python", "ammo.pistol");
                gunList.Add("pistol.revolver", "ammo.pistol");
                gunList.Add("pistol.semiauto", "ammo.pistol");
                gunList.Add("smg.2", "ammo.pistol");
                gunList.Add("smg.thompson", "ammo.pistol");

                gunList.Add("shotgun.double", "ammo.shotgun.fire");
                gunList.Add("shotgun.pump", "ammo.shotgun");
                gunList.Add("shotgun.spas12", "ammo.shotgun.slug");

                try
                {
                    foreach (var turret in Turrets)
                    {
                        turret.inventory.Clear();
                        ItemManager.CreateByName(gunList.ElementAt(index).Key, 1).MoveToContainer(turret.inventory, 0);
                        ItemManager.CreateByName(gunList.ElementAt(index).Value, 1000).MoveToContainer(turret.inventory, 1);
                        turret.UpdateAttachedWeapon();
                        turret.Reload();
                    }
                }
                catch (Exception e)
                { instance.Puts(e.Message); }

                return ItemManager.FindItemDefinition(gunList.ElementAt(index).Key);
            }

            internal void SpawnRockets()
            {
                if (baseHelicopter == null) instance.Puts("Base heli is null");

                float yAdjustment = -.01f;

                SpawnBaseEntity(new Vector3(3f, 1.15f + yAdjustment, 0f), new Vector3(0, 90, 90), baseHelicopter, "assets/bundled/prefabs/radtown/loot_barrel_1.prefab");
                SpawnBaseEntity(new Vector3(-3f, 1.15f + yAdjustment, 0f), new Vector3(0, 90, 90), baseHelicopter, "assets/bundled/prefabs/radtown/loot_barrel_1.prefab");

                SpawnBaseEntity(new Vector3(3.5f, 1.5f, 0.5f), new Vector3(0f, 0f, 90f), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab");
                SpawnBaseEntity(new Vector3(-3.5f, 1.5f, 0.5f), new Vector3(0, 0, 270), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab");

                SpawnBaseEntity(new Vector3(2f, 1.5f, 0.5f), new Vector3(0f, 0f, 130f), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.vent.prefab");
                SpawnBaseEntity(new Vector3(-2f, 1.5f, 0.5f), new Vector3(0f, 0f, 230f), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.vent.prefab");

                float offset_left_x = -3.15f;
                float offset_right_x = 2.85f;
                float spread1 = 0.4f;
                float spread2 = 0.4f;
                float spread3 = 0.2f;

                TubesEntities = new List<BaseEntity>
                {
                    SpawnArmament(new Vector3(-spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(-spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(-spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter),

                    SpawnArmament(new Vector3(-spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(-spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(-spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter),
                    SpawnArmament(new Vector3(spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter)
                };

                SpawnBaseEntity(new Vector3(-0.6f, 0.7f, -3.2f), new Vector3(0f, 90f, 40f), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab");
                SpawnBaseEntity(new Vector3(0.6f, 2.5f, -1.7f), new Vector3(180f, 0f, 0f) + new Vector3(0f, 90f, -40f), baseHelicopter, "assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab");

                Turrets = new List<AutoTurret>
                {
                    SpawnTurret(new Vector3(2f, 1.4f, 0.5f), new Vector3(180, 0, 0), baseHelicopter),
                    SpawnTurret(new Vector3(-2f, 1.4f, 0.5f), new Vector3(180, 0, 0), baseHelicopter)
                };
                PowerUp();

                turretsSpawned = true;
            }

            private AutoTurret SpawnTurret(Vector3 position, Vector3 rotationEuler, BaseEntity parent)
            {
                var entity = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, this.rotation, true);
                if (parent != null)
                {
                    entity.SetParent(parent, 0);
                    entity.transform.localEulerAngles = rotationEuler;
                    entity.transform.localPosition = position;
                }
                entity?.Spawn();

                AddEntityToData(entity, entity.transform.position);
                entity.GetComponent<AutoTurret>().SetPeacekeepermode(true);
                Turrets.Add(entity.GetComponent<AutoTurret>());
                return entity.GetComponent<AutoTurret>();
            }

            private BaseEntity SpawnBaseEntity(Vector3 position, Vector3 rotationEuler, BaseEntity parent, string prefab)
            {
                var entity = GameManager.server.CreateEntity(prefab, baseHelicopter.transform.position, baseHelicopter.transform.rotation, true);

                if (parent != null)
                {
                    entity.SetParent(parent, 0);
                    entity.transform.localEulerAngles = rotationEuler;
                    entity.transform.localPosition = position;
                }
                entity?.Spawn();
                AddEntityToData(entity, entity.transform.position);
                MakeDoorsInactive(entity);

                return entity;
            }
            private void SpawnGuns()
            {
                Turrets = new List<AutoTurret> {
                    SpawnTurret(new Vector3(1f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter),
                    SpawnTurret(new Vector3(2f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter),
                    SpawnTurret(new Vector3(-1f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter),
                    SpawnTurret(new Vector3(-2f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter)
                };

                turretsSpawned = true;
                PowerUp();
            }

            private BaseEntity SpawnArmament(Vector3 position, Vector3 rotation, BaseEntity parent)
            {
                var entityParent = GameManager.server.CreateEntity("assets/prefabs/tools/pager/pager.entity.prefab", this.position, this.rotation, true);
                entityParent.Spawn();
                entityParent.SetParent(parent);
                entityParent.transform.localPosition = position;
                entityParent.transform.localEulerAngles = rotation;
                AddEntityToData(entityParent, entityParent.transform.position);

                var entityTube = GameManager.server.CreateEntity("assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", this.position, this.rotation, true);
                entityTube.SetParent(entityParent);
                entityTube?.Spawn();

                AddEntityToData(entityTube, entityTube.transform.position);
                TubesEntities.Add(entityTube);
                TubesParents.Add(entityParent);
                return entityTube;
            }

            public void SpawnWings()
            {
                SpawnBaseEntity(new Vector3(-0.3f, 0.2f, 0f), new Vector3(90, 0, 90), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
                SpawnBaseEntity(new Vector3(0.3f, 0.2f, 0f), new Vector3(90, 0, 270), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");

                SpawnBaseEntity(new Vector3(-0.3f, 0.75f, 0f), new Vector3(90, 0, 90), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
                SpawnBaseEntity(new Vector3(0.3f, 0.75f, 0f), new Vector3(90, 0, 270), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
            }
            public void PowerUp()
            {
                foreach (var turret in Turrets)
                {
                    try { turret.UpdateFromInput(100, 1); } catch { }
                }
            }
            public void PowerDown()
            {
                foreach (var turret in Turrets)
                {
                    try { turret.UpdateFromInput(0, 1); } catch { }
                }
            }

            private void ResetAmmo()
            {
                if (Time.time > lastShot + 120 && storageContainer.inventory.itemList.Count == 0 && storageContainer.inventory.itemList.Count < 12)
                {
                    storageContainer.inventory.AddItem(ItemManager.FindItemDefinition("ammo.rocket.fire"), 12);
                    instance.Puts($"Current pilot: transportCopter.GetDriver().displayName");
                    baseHelicopter.GetDriver().ChatMessage("Rockets reloaded!");
                }
            }

            private void KeepFacingFrontMini(AutoTurret turret, Vector3 target)
            {
                try
                {
                    if (turret != null && turret?.IsOnline() == true)
                    {
                        turret.aimDir = target;
                        turret?.SendAimDir();
                        turret?.UpdateAiming();
                    }
                }
                catch { }
            }
            private void KeepFacingFrontTransport(AutoTurret turret)
            {
                try
                {
                    if (turret != null && turret?.IsOnline() == true)
                    {
                        turret?.Reload();
                        turret.aimDir = baseHelicopter.transform.forward;
                        turret?.SendAimDir();
                        turret?.UpdateAiming();
                    }
                }
                catch { }
            }

            private void MakeDoorsInactive(BaseEntity entity)
            {
                var door = entity.GetComponent<Door>();
                if (door != null)
                {
                    door.canHandOpen = false;
                    door.canNpcOpen = false;
                    door.canTakeCloser = false;
                    door.canTakeKnocker = false;
                    door.canTakeLock = false;
                }
            }

            private Vector3 GetDirection(float accuracy)
            {
                return (Vector3)(Quaternion.Euler(UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f)), UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f)), UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f))) * baseHelicopter.transform.forward);
            }

            private string GetProjectileFromItem(Item item)
            {
                if (item.info.shortname == "ammo.rocket.basic")
                {
                    return "assets/prefabs/ammo/rocket/rocket_basic.prefab";
                }
                if (item.info.shortname == "ammo.rocket.fire")
                {
                    return "assets/prefabs/ammo/rocket/rocket_fire.prefab";
                }
                if (item.info.shortname == "ammo.rocket.hv")
                {
                    return "assets/prefabs/ammo/rocket/rocket_hv.prefab";
                }
                return "";
            }

            internal void FireTurretsRockets(BasePlayer player)
            {
                string projectile = GetProjectileFromItem(storageContainer.inventory.itemList[0]);

                if (projectile != "")
                {
                    storageContainer.inventory.itemList[0].UseItem();

                    float offset_left_x = -3.15f;
                    float offset_right_x = 2.85f;
                    float spread1 = 0.4f;
                    float spread2 = 0.4f;
                    float spread3 = 0.2f;
                    float z = 1f;

                    Tubes.Clear();

                    Tubes.Add(new Vector3(-spread1 + offset_left_x, 1.4f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread1 + offset_right_x, 1.4f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread1 + offset_left_x, 1.4f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread1 + offset_left_x, 1.4f, z) + baseHelicopter.transform.localPosition);

                    Tubes.Add(new Vector3(-spread2 + offset_right_x, 1.1f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread2 + offset_left_x, 1.1f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread2 + offset_right_x, 1.1f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread2 + offset_left_x, 1.1f, z) + baseHelicopter.transform.localPosition);

                    Tubes.Add(new Vector3(-spread3 + offset_left_x, 0.85f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread3 + offset_right_x, 0.85f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread3 + offset_left_x, 0.85f, z) + baseHelicopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread3 + offset_right_x, 0.85f, z) + baseHelicopter.transform.localPosition);

                    Vector3 originR = TubesParents[currentTubeIndex].transform.position;

                    var direction = GetDirection(4f);

                    var rocketsR = GameManager.server.CreateEntity(projectile, originR);

                    if (rocketsR != null)
                    {
                        if (currentTubeIndex == 11)
                        {
                            currentTubeIndex = 0;
                        }
                        else
                        {
                            currentTubeIndex++;
                        }
                        rocketsR.SendMessage("InitializeVelocity", (Vector3)(direction * 75f));
                        rocketsR.Spawn();
                        lastShot = Time.time;
                        var itemAmount = storageContainer.inventory.FindItemsByItemName("ammo.rocket.fire").amount;
                        if (itemAmount > 0) { baseHelicopter.GetDriver().ChatMessage($"Rockets left: {storageContainer.inventory.FindItemsByItemName("ammo.rocket.fire").amount}"); }
                    }
                }
            }

            private Vector3 FindTarget(BasePlayer player)
            {
                RaycastHit hitInfo;

                if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, Mathf.Infinity, -1063040255))
                {
                }
                Vector3 hitpoint = hitInfo.point;
                return hitpoint;
            }

            private Vector3 target;
            private int index = 0;
            private float nextShootTime;
            private bool canGunShoot = true;
            private CuiElementContainer elementContainer;

            public void FireTurretsGuns(BasePlayer player)
            {
                try
                {
                    foreach (var turret in Turrets)
                    {
                        if (turret.IsOnline() == true && canGunShoot)
                        {
                            if (turret.GetAttachedWeapon().AmmoFraction() <= 0)
                            {
                                nextShootTime = Time.time + turret.GetAttachedWeapon().GetReloadDuration();
                                foreach (var turretToReload in Turrets)
                                {
                                    turretToReload.GetAttachedWeapon().TopUpAmmo();
                                }
                                canGunShoot = false;
                            }
                            turret.FireAttachedGun(target, ConVar.PatrolHelicopter.bulletAccuracy);
                        }
                    }
                }
                catch (Exception e) { instance.Puts($"FireTurretsGuns: {e.Message}"); };
            }



            private void ShowIU(string pitchValue, string rollValue, string yawValue, BasePlayer player)
            {
                var color = "0 0 255 1";
                var fontSize = 24;
                var align = TextAnchor.MiddleCenter;

                DestroyUi(player);

                CuiElementContainer indicators = new CuiElementContainer();

                indicators.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = fontSize, Align = align, Text = pitchValue },
                    RectTransform = { AnchorMin = "0.30 0.10", AnchorMax = "0.50 0.50" }
                }, "Hud", "pitch");

                indicators.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = fontSize, Align = align, Text = rollValue },
                    RectTransform = { AnchorMin = "0.10 0.10", AnchorMax = "0.30 0.50" }
                }, "Hud", "roll");

                indicators.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = fontSize, Align = align, Text = yawValue },
                    RectTransform = { AnchorMin = "0.20 0.10", AnchorMax = "0.4 0.50" }
                }, "Hud", "yaw");

                CuiHelper.AddUi(player, indicators);
            }

            public void DestroyUi(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "Hud");

                CuiHelper.DestroyUi(player, "pitch");
                CuiHelper.DestroyUi(player, "roll");
                CuiHelper.DestroyUi(player, "yaw");
            }
        }
    }
}