using Facepunch;
using Oxide.Core;
using System;
using System.Collections.Generic;
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

        class Armament : MonoBehaviour
        {
            private static HelicopterType baseHeliType;
            private MiniCopter baseHelicopter;


            private Vector3 position;
            private Quaternion rotation;
            private BaseEntity samRightEntity;
            private SamSite samRight;
            private float lastTargetVisibleTime;
            private BaseCombatEntity currentTarget;
            private float lockOnTime;
            private float scanRadius = 300f;
            private BaseEntity wingLeft;
            private BaseEntity wingRight;
            private BaseEntity tubeRight1;
            private BaseEntity tubeRight1Parent;
            private BaseEntity tubeRight2Parent;
            private BaseEntity tubeRight2;
            private BaseEntity tubeleft1Parent;
            private BaseEntity tubeleft1;
            private BaseEntity tubeleft2;
            private BaseEntity barrelleft1;
            private BaseEntity barrelright1;
            private BaseEntity wingRightTilted;
            private BaseEntity wingLeftTilted;
            private BaseEntity tube1L;
            private BaseEntity tube2L;
            private BaseEntity tube3L;
            private BaseEntity tube4L;
            private BaseEntity tube5L;
            private BaseEntity tube6L;
            private BaseEntity tube1R;
            private BaseEntity tube2R;
            private BaseEntity tube3R;
            private BaseEntity tube4R;
            private BaseEntity tube5R;
            private BaseEntity tube6R;

            private BaseEntity turretLeft2 = new BaseEntity();
            private BaseEntity turretRight2 = new BaseEntity();
            private AutoTurret leftTurret = new AutoTurret();
            private AutoTurret rightTurret = new AutoTurret();

            private BaseEntity gunLeft;
            private GunTrap guntrapLeft;
            private BaseEntity gunRight;
            private GunTrap guntrapRight;
            private AutoTurret turretRight2AT;
            private BaseEntity wingLeftBottom;
            private BaseEntity wingRightBottom;
            private BaseEntity wingLeftTop;
            private BaseEntity wingRightTop;

            private bool turretsSpawned;

            List<Vector3> Tubes = new List<Vector3>();
            List<BaseEntity> TubesEntities = new List<BaseEntity>();
            List<BaseEntity> TubesParents = new List<BaseEntity>();
            private int currentTubeIndex;
            private BaseEntity backDoor;
            private BaseEntity storage;
            private StorageContainer storageContainer;
            private DropBox storageDropBox;
            private bool canFireRockets;
            private float nextActionTime;
            private float period = 1;

            private float lastShot;

            private enum HelicopterType
            {
                Transport,
                Mini
            }
            private void Awake()
            {

                SetType();
                baseHelicopter = GetComponent<MiniCopter>();

                lastShot = Time.time;

                position = baseHelicopter.transform.position;
                rotation = baseHelicopter.transform.rotation;

                SpawnArmament();

                currentTubeIndex = 0;
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
                        SpawnGuns();
                        break;

                    case HelicopterType.Transport:
                        SpawnRockets();
                        break;
                }
            }

            void FixedUpdate()
            {
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
                    if (leftTurret != null) { KeepFacingFront(leftTurret); }
                }
                catch { }

                try
                {
                    if (rightTurret != null) { KeepFacingFront(rightTurret); }
                }
                catch { }

                try
                {
                    ResetAmmo();
                }
                catch { }
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
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    FireTurretsRockets(player);
                }
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    FireTurretsGuns(player);
                }
            }

            internal void SpawnRockets()
            {
                float yAdjustment = -.01f;
                //spawn wings
                barrelright1 = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/loot_barrel_1.prefab", position, rotation, false);
                barrelright1.SetParent(baseHelicopter);
                barrelright1.transform.localPosition = new Vector3(3f, 1.15f + yAdjustment, 0f);
                barrelright1.transform.localEulerAngles = new Vector3(0, 90, 90);
                barrelright1.Spawn();
                AddEntityToData(barrelright1, barrelright1.transform.position);

                barrelleft1 = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/loot_barrel_1.prefab", position, rotation, false);
                barrelleft1.SetParent(baseHelicopter);
                barrelleft1.transform.localPosition = new Vector3(-3f, 1.15f + yAdjustment, 0f);
                barrelleft1.transform.localEulerAngles = new Vector3(0, 90, 90);
                barrelleft1.Spawn();
                AddEntityToData(barrelleft1, barrelleft1.transform.position);

                wingRight = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab", position, rotation, false);
                wingRight.SetParent(baseHelicopter);
                wingRight.transform.localPosition = new Vector3(3.5f, 1.5f, 0.5f); //(2f,1f,0f);
                wingRight.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                wingRight?.Spawn();
                MakeDoorsInactive(wingRight.GetComponent<Door>());
                AddEntityToData(wingRight, wingRight.transform.position);

                wingRightTilted = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.vent.prefab", position, rotation, false);
                wingRightTilted.SetParent(baseHelicopter);
                wingRightTilted.transform.localPosition = new Vector3(2f, 1.5f, 0.5f); //(2f,1f,0f);
                wingRightTilted.transform.localEulerAngles = new Vector3(0f, 0f, 130f);
                wingRightTilted?.Spawn();
                MakeDoorsInactive(wingRightTilted.GetComponent<Door>());
                AddEntityToData(wingRightTilted, wingRightTilted.transform.position);

                wingLeft = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab", position, rotation, false);
                wingLeft.SetParent(baseHelicopter);
                wingLeft.transform.localPosition = new Vector3(-3.5f, 1.5f, 0.5f); //(2f,1f,0f);
                wingLeft.transform.localEulerAngles = new Vector3(0, 0, 270);
                wingLeft?.Spawn();
                MakeDoorsInactive(wingLeft.GetComponent<Door>());
                AddEntityToData(wingLeft, wingLeft.transform.position);

                wingLeftTilted = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.vent.prefab", position, rotation, false);
                wingLeftTilted.SetParent(baseHelicopter);
                wingLeftTilted.transform.localPosition = new Vector3(-2f, 1.5f, 0.5f); //(2f,1f,0f);
                wingLeftTilted.transform.localEulerAngles = new Vector3(0f, 0f, 230f);
                wingLeftTilted?.Spawn();
                MakeDoorsInactive(wingLeftTilted.GetComponent<Door>());
                AddEntityToData(wingLeftTilted, wingLeftTilted.transform.position);

                //spawn guns 
                float offset_left_x = -3.15f;
                float offset_right_x = 2.85f;
                float spread1 = 0.4f;
                float spread2 = 0.4f;
                float spread3 = 0.2f;

                tube1L = SpawnArmamaent(new Vector3(-spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube1R = SpawnArmamaent(new Vector3(-spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube2L = SpawnArmamaent(new Vector3(spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube2R = SpawnArmamaent(new Vector3(spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube3L = SpawnArmamaent(new Vector3(-spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube3R = SpawnArmamaent(new Vector3(-spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube4L = SpawnArmamaent(new Vector3(spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube4R = SpawnArmamaent(new Vector3(spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube5L = SpawnArmamaent(new Vector3(-spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube5R = SpawnArmamaent(new Vector3(-spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube6L = SpawnArmamaent(new Vector3(spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter);
                tube6R = SpawnArmamaent(new Vector3(spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), baseHelicopter);

                //spawn back door
                var backDoor = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab", position, rotation, true);
                backDoor.SetParent(baseHelicopter, 0);
                backDoor.transform.localPosition = new Vector3(-0.6f, 0.7f, -3.2f); //(2f,1f,0f);
                backDoor.transform.localEulerAngles = new Vector3(0f, 90f, 40f);
                backDoor?.Spawn();
                backDoor.enableSaving = false;
                AddEntityToData(backDoor, backDoor.transform.position);

                var backDoor2 = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab", position, rotation, true);
                backDoor2.SetParent(baseHelicopter, 0);
                backDoor2.transform.localPosition = new Vector3(0.6f, 2.5f, -1.7f); //(2f,1f,0f);
                backDoor2.transform.localEulerAngles = new Vector3(180f, 0f, 0f) + new Vector3(0f, 90f, -40f);
                backDoor2?.Spawn();
                backDoor2.GetComponent<Door>().canHandOpen = true;
                backDoor2.GetComponent<Door>().isSecurityDoor = false;
                AddEntityToData(backDoor2, backDoor2.transform.position);

                //spawn rocket containers
                try
                {
                    storage = GameManager.server.CreateEntity("assets/prefabs/deployable/dropbox/dropbox.deployed.prefab", position, rotation, true);
                    storage.SetParent(baseHelicopter, 0);
                    storage.transform.localPosition = new Vector3(1f, 1.35f, 1.7f);
                    storage.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
                    storage?.Spawn();
                    AddEntityToData(storage, storage.transform.position);

                    storageContainer = storage.GetComponent<DropBox>();
                    storageContainer.isLootable = false;
                }
                catch { }

                try
                {
                    turretRight2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", position, rotation, true);
                    turretRight2.transform.localEulerAngles = new Vector3(0, 0, 90);
                    turretRight2.transform.localPosition = new Vector3(0f, 1.5f, -0.2f);
                    turretRight2.SetParent(wingRight, 0);
                    turretRight2?.Spawn();
                    AddEntityToData(turretRight2, turretRight2.transform.position);

                    rightTurret = turretRight2.GetComponent<AutoTurret>();
                    rightTurret.SetPeacekeepermode(true);
                }
                catch (Exception e) { instance.Puts($"Right Turret not spawned: {e}"); }

                try
                {
                    turretLeft2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", position, rotation, true);
                    turretLeft2.transform.localEulerAngles = new Vector3(0, 0, -90);
                    turretLeft2.transform.localPosition = new Vector3(0f, 1.5f, -0.2f);
                    turretLeft2.SetParent(wingLeft, 0);
                    turretLeft2?.Spawn();
                    AddEntityToData(turretLeft2, turretLeft2.transform.position);

                    leftTurret = turretLeft2.GetComponent<AutoTurret>();
                    leftTurret.SetPeacekeepermode(true);
                }
                catch (Exception e) { instance.Puts($"Left Turret not spawned: {e}"); }

                try { rightTurret.UpdateFromInput(100, 1); } catch { }
                try { leftTurret.UpdateFromInput(100, 1); } catch { }

                turretsSpawned = true;
            }

            private void SpawnGuns()
            {
                spawnWings(this.position, this.rotation);

                turretRight1 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, this.rotation, true);
                turretRight1.transform.localEulerAngles = new Vector3(0, 0, 90);
                turretRight1.transform.localPosition = new Vector3(0f, 1f, 0f);
                turretRight1?.Spawn();
                turretRight1.SetParent(wingRight, 0);
                AddEntityToData(turretRight1, turretRight1.transform.position);

                weaponRight1AT = turretRight1.GetComponent<AutoTurret>();
                weaponRight1AT.SetPeacekeepermode(true);

                turretRight2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, this.rotation, true);
                turretRight2.transform.localEulerAngles = new Vector3(0, 0, 90);
                turretRight2.transform.localPosition = new Vector3(0f, 2f, 0f);
                turretRight2?.Spawn();
                turretRight2.SetParent(wingRight, 0);
                AddEntityToData(turretRight2, turretRight2.transform.position);

                weaponRight2AT = turretRight2.GetComponent<AutoTurret>();
                weaponRight2AT.SetPeacekeepermode(true);

                turretLeft1 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, this.rotation, true);
                turretLeft1.transform.localEulerAngles = new Vector3(0, 0, -90);
                turretLeft1.transform.localPosition = new Vector3(0f, 1f, 0f);
                turretLeft1?.Spawn();
                turretLeft1.SetParent(wingLeft, 0);
                AddEntityToData(turretLeft1, turretLeft1.transform.position);

                weaponLeft1AT = turretLeft1.GetComponent<AutoTurret>();
                weaponLeft1AT.SetPeacekeepermode(true);

                turretLeft2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, this.rotation, true);
                turretLeft2.transform.localEulerAngles = new Vector3(0, 0, -90);
                turretLeft2.transform.localPosition = new Vector3(0f, 2f, 0f);
                turretLeft2?.Spawn();
                turretLeft2.SetParent(wingLeft, 0);
                AddEntityToData(turretLeft2, turretLeft2.transform.position);

                weaponLeft2AT = turretLeft2.GetComponent<AutoTurret>();
                weaponLeft2AT.SetPeacekeepermode(true);

                turretsSpawned = true;
                PowerUp();
            }
            public void spawnWings(Vector3 pos, Quaternion rot)
            {
                wingLeftBottom = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingLeftBottom.transform.localEulerAngles = new Vector3(90, 0, 90);
                wingLeftBottom.transform.localPosition = new Vector3(-0.3f, 0.2f, 0f);
                wingLeftBottom?.Spawn();
                wingLeftBottom.SetParent(baseHelicopter, 0);
                AddEntityToData(wingLeftBottom, wingLeftBottom.transform.position);

                wingRightBottom = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingRightBottom.transform.localEulerAngles = new Vector3(90, 0, 270);
                wingRightBottom.transform.localPosition = new Vector3(0.3f, 0.2f, 0f);
                wingRightBottom?.Spawn();
                wingRightBottom.SetParent(baseHelicopter, 0);
                AddEntityToData(wingRightBottom, wingRightBottom.transform.position);

                wingLeftTop = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingLeftTop.transform.localEulerAngles = new Vector3(90, 0, 90);
                wingLeftTop.transform.localPosition = new Vector3(-0.3f, 1.2f, 0f);
                wingLeftTop?.Spawn();
                wingLeftTop.SetParent(baseHelicopter, 0);
                AddEntityToData(wingLeftTop, wingLeftTop.transform.position);

                wingRightTop = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingRightTop.transform.localEulerAngles = new Vector3(90, 0, 270);
                wingRightTop.transform.localPosition = new Vector3(0.3f, 1.2f, 0f);
                wingRightTop?.Spawn();
                wingRightTop.SetParent(baseHelicopter, 0);
                AddEntityToData(wingRightTop, wingRightTop.transform.position);

                wingLeft = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingLeft.transform.localEulerAngles = new Vector3(90, 0, 90);
                wingLeft.transform.localPosition = new Vector3(-0.3f, 0.75f, 0f);
                wingLeft?.Spawn();
                wingLeft.SetParent(baseHelicopter, 0);
                AddEntityToData(wingLeft, wingLeft.transform.position);

                wingRight = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.single.prefab", pos, rot, true);
                wingRight.transform.localEulerAngles = new Vector3(90, 0, 270);
                wingRight.transform.localPosition = new Vector3(0.3f, 0.75f, 0f);
                wingRight?.Spawn();
                wingRight.SetParent(baseHelicopter, 0);
                AddEntityToData(wingRight, wingRight.transform.position);
            }
            public void PowerUp()
            {
                try { weaponRight1AT.UpdateFromInput(100, 1); } catch { }
                try { weaponRight2AT.UpdateFromInput(100, 1); } catch { }
                try { weaponLeft1AT.UpdateFromInput(100, 1); } catch { }
                try { weaponLeft2AT.UpdateFromInput(100, 1); } catch { }
            }
            public void PowerDown()
            {
                try { weaponRight1AT.UpdateFromInput(0, 1); } catch { }
                try { weaponRight2AT.UpdateFromInput(0, 1); } catch { }
                try { weaponLeft1AT.UpdateFromInput(0, 1); } catch { }
                try { weaponLeft2AT.UpdateFromInput(0, 1); } catch { }
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

            private void KeepFacingFront(AutoTurret turret)
            {
                try
                {
                    if (turret != null && turret?.IsOnline() == true)
                    {
                        turret?.Reload();
                        turret.aimDir = turret.transform.forward;
                        turret?.SendAimDir();
                        turret?.UpdateAiming();
                    }
                }
                catch { }
            }

            private void MakeDoorsInactive(Door door)
            {
                door.canHandOpen = false;
                door.canNpcOpen = false;
                door.canTakeCloser = false;
                door.canTakeKnocker = false;
                door.canTakeLock = false;
            }

            private BaseEntity SpawnArmamaent(Vector3 position, Vector3 rotation, BaseEntity parent)
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

            private Vector3 FindTarget(Vector3 target, BasePlayer player)
            {
                RaycastHit hitInfo;

                if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, Mathf.Infinity, -1063040255))
                {
                }
                Vector3 hitpoint = hitInfo.point;
                return hitpoint;
            }
            private Vector3 target;
            private BaseEntity turretRight1;
            private AutoTurret weaponRight1AT;
            private AutoTurret weaponRight2AT;
            private BaseEntity turretLeft1;
            private AutoTurret weaponLeft1AT;
            private AutoTurret weaponLeft2AT;

            public void FireTurretsGuns(BasePlayer player)
            {

                try
                {
                    if (leftTurret.IsOnline() == true)
                    {
                        leftTurret.Reload();
                        leftTurret.FireAttachedGun(FindTarget(target, player), ConVar.PatrolHelicopter.bulletAccuracy);
                    }
                }
                catch { }

                try
                {
                    if (rightTurret.IsOnline() == true)
                    {
                        rightTurret.Reload();
                        rightTurret.FireAttachedGun(FindTarget(target, player), ConVar.PatrolHelicopter.bulletAccuracy);
                    }
                }
                catch { }
            }
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            try
            {
                var copter = player.GetMountedVehicle().GetComponent<Armament>();
                copter.HelicopterInput(input, player);
            }
            catch
            {

            }
        }

    }
}