using Facepunch;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ArmedHelicopters/ArmedHelicopters", "Wujaszkun", "0.2.5")]
    [Description("Armament for scrap transport helicopter and Minicopter")]
    class ArmedHelicopters : RustPlugin
    {
        public static ArmedHelicopters instance;
        public ScrapTransportHelicopter copter;
        private BasePlayer player;
        private int temp;
        private List<ArmedTransportHelicopter> helicopterList = new List<ArmedTransportHelicopter>();
        private bool isLoggingEnabled = true;

        [ChatCommand("armtransportcopter")]
        void ArmTransportCopters(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                foreach (var entity in GameObject.FindObjectsOfType<ScrapTransportHelicopter>())
                {
                    if (entity.gameObject.GetComponent<ArmedTransportHelicopter>() == null)
                    {
                        var copter = entity.gameObject.AddComponent<ArmedTransportHelicopter>();
                        helicopterList.Add(copter as ArmedTransportHelicopter);
                    }
                }
            }
        }
        void OnServerInitialized()
        {
            instance = this;
        }
        void Unload()
        {
            foreach (var copter in GameObject.FindObjectsOfType<ArmedTransportHelicopter>())
            {
                if (copter != null)
                {
                    copter.DespawnAllEntities();
                    GameObject.Destroy(copter);
                }
            }
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.gameObject.GetComponent<ScrapTransportHelicopter>() != null)
            {
                var copter = entity.gameObject.AddComponent<ArmedTransportHelicopter>();
                helicopterList.Add(copter as ArmedTransportHelicopter);
            }
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.gameObject.GetComponent<ScrapTransportHelicopter>() != null)
            {
                var copter = entity.gameObject.AddComponent<ArmedTransportHelicopter>();
                try { helicopterList.Remove(copter as ArmedTransportHelicopter); }
                catch
                {
                    Log($"Copter {copter.gameObject.GetInstanceID()} not removed from list!");
                }
            }
        }
        private void Log(string message)
        {
            if (isLoggingEnabled)
            {
                Puts(message);
            }
        }
        class ArmedTransportHelicopter : MonoBehaviour
        {
            private PieMenu pieMenu;
            private ScrapTransportHelicopter transportCopter;
            private Vector3 pos;
            private Quaternion rot;
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
            private AutoTurret weaponLeft2AT = new AutoTurret();
            private AutoTurret weaponRight2AT = new AutoTurret();

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

            private float lastTimeRocketShot;
            private void Awake()
            {
                transportCopter = GetComponent<ScrapTransportHelicopter>();
                pieMenu = new PieMenu();
                lastTimeRocketShot = Time.time;
                var location = transportCopter.transform.position;
                var rotation = transportCopter.transform.rotation;

                pos = new Vector3(location.x, location.y, location.z);
                rot = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                AddRockets();
                currentTubeIndex = 0;
            }
            void FixedUpdate()
            {
                try
                {
                    if (storageContainer.inventory.itemList.Count == 0) canFireRockets = false;
                    if (Time.time > nextActionTime)
                    {
                        nextActionTime = Time.time + period;
                        if (storageContainer.inventory.itemList.Count > 0) canFireRockets = true;
                    }
                }
                catch { }
                try
                {
                    KeepFacingFront(weaponLeft2AT);
                    KeepFacingFront(weaponRight2AT);
                }
                catch
                {

                }
                try { ResetRockets(); }
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
                if (transportCopter.GetPlayerSeat(player) == 0  && inputState.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    FireTurretsRockets(player);
                }
                if (transportCopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    FireTurretsGuns(player);
                }
            }
            internal void AddRockets()
            {
                float yAdjustment = -.01f;
                //spawn wings
                barrelright1 = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/loot_barrel_1.prefab", pos, rot, false);
                barrelright1.SetParent(transportCopter);
                barrelright1.transform.localPosition = new Vector3(3f, 1.15f + yAdjustment, 0f);
                barrelright1.transform.localEulerAngles = new Vector3(0, 90, 90);
                barrelright1.Spawn();
                AddEntityToData(barrelright1, barrelright1.transform.position);

                barrelleft1 = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/loot_barrel_1.prefab", pos, rot, false);
                barrelleft1.SetParent(transportCopter);
                barrelleft1.transform.localPosition = new Vector3(-3f, 1.15f + yAdjustment, 0f);
                barrelleft1.transform.localEulerAngles = new Vector3(0, 90, 90);
                barrelleft1.Spawn();
                AddEntityToData(barrelleft1, barrelleft1.transform.position);

                wingRight = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab", pos, rot, false);
                wingRight.SetParent(transportCopter);
                wingRight.transform.localPosition = new Vector3(3.5f, 1.5f, 0.5f); //(2f,1f,0f);
                wingRight.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                wingRight?.Spawn();
                MakeDoorsInactive(wingRight.GetComponent<Door>());
                AddEntityToData(wingRight, wingRight.transform.position);

                wingRightTilted = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.vent.prefab", pos, rot, false);
                wingRightTilted.SetParent(transportCopter);
                wingRightTilted.transform.localPosition = new Vector3(2f, 1.5f, 0.5f); //(2f,1f,0f);
                wingRightTilted.transform.localEulerAngles = new Vector3(0f, 0f, 130f);
                wingRightTilted?.Spawn();
                MakeDoorsInactive(wingRightTilted.GetComponent<Door>());
                AddEntityToData(wingRightTilted, wingRightTilted.transform.position);

                wingLeft = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_a.prefab", pos, rot, false);
                wingLeft.SetParent(transportCopter);
                wingLeft.transform.localPosition = new Vector3(-3.5f, 1.5f, 0.5f); //(2f,1f,0f);
                wingLeft.transform.localEulerAngles = new Vector3(0, 0, 270);
                wingLeft?.Spawn();
                MakeDoorsInactive(wingLeft.GetComponent<Door>());
                AddEntityToData(wingLeft, wingLeft.transform.position);

                wingLeftTilted = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.vent.prefab", pos, rot, false);
                wingLeftTilted.SetParent(transportCopter);
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

                tube1L = SpawnArmamaent(new Vector3(-spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube1R = SpawnArmamaent(new Vector3(-spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube2L = SpawnArmamaent(new Vector3(spread1 + offset_left_x, 1.4f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube2R = SpawnArmamaent(new Vector3(spread1 + offset_right_x, 1.4f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube3L = SpawnArmamaent(new Vector3(-spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube3R = SpawnArmamaent(new Vector3(-spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube4L = SpawnArmamaent(new Vector3(spread2 + offset_left_x, 1.1f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube4R = SpawnArmamaent(new Vector3(spread2 + offset_right_x, 1.1f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube5L = SpawnArmamaent(new Vector3(-spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube5R = SpawnArmamaent(new Vector3(-spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube6L = SpawnArmamaent(new Vector3(spread3 + offset_left_x, 0.85f, 1f), new Vector3(0, 277, 130), transportCopter);
                tube6R = SpawnArmamaent(new Vector3(spread3 + offset_right_x, 0.85f, 1f), new Vector3(0, 277, 130), transportCopter);

                //spawn back door
                var backDoor = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab", pos, rot, true);
                backDoor.SetParent(transportCopter, 0);
                backDoor.transform.localPosition = new Vector3(-0.6f, 0.7f, -3.2f); //(2f,1f,0f);
                backDoor.transform.localEulerAngles = new Vector3(0f, 90f, 40f);
                backDoor?.Spawn();
                backDoor.enableSaving = false;
                AddEntityToData(backDoor, backDoor.transform.position);

                var backDoor2 = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_h.prefab", pos, rot, true);
                backDoor2.SetParent(transportCopter, 0);
                backDoor2.transform.localPosition = new Vector3(0.6f, 2.5f, -1.7f); //(2f,1f,0f);
                backDoor2.transform.localEulerAngles = new Vector3(180f, 0f, 0f) + new Vector3(0f, 90f, -40f);
                backDoor2?.Spawn();
                backDoor2.GetComponent<Door>().canHandOpen = true;
                backDoor2.GetComponent<Door>().isSecurityDoor = false;
                AddEntityToData(backDoor2, backDoor2.transform.position);

                //spawn rocket containers
                try
                {
                    storage = GameManager.server.CreateEntity("assets/prefabs/deployable/dropbox/dropbox.deployed.prefab", pos, rot, true);
                    storage.SetParent(transportCopter, 0);
                    storage.transform.localPosition = new Vector3(1f, 1.35f, 1.7f);
                    storage.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
                    storage?.Spawn();
                    AddEntityToData(storage, storage.transform.position);

                    storageContainer = storage.GetComponent<DropBox>();
                    storageContainer.isLootable = false;
                }
                catch { }

                //spawn guns
                try
                {
                    turretRight2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos, rot, true);
                    turretRight2.transform.localEulerAngles = new Vector3(0, 0, 90);
                    turretRight2.transform.localPosition = new Vector3(0f, 1.5f, -0.2f);
                    turretRight2.SetParent(wingRight, 0);
                    turretRight2?.Spawn();
                    AddEntityToData(turretRight2, turretRight2.transform.position);

                    weaponRight2AT = turretRight2.GetComponent<AutoTurret>();
                    weaponRight2AT.SetPeacekeepermode(true);
                }
                catch { }
                turretLeft2 = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos, rot, true);
                turretLeft2.transform.localEulerAngles = new Vector3(0, 0, -90);
                turretLeft2.transform.localPosition = new Vector3(0f, 1.5f, -0.2f);
                turretLeft2.SetParent(wingLeft, 0);
                turretLeft2?.Spawn();
                AddEntityToData(turretLeft2, turretLeft2.transform.position);

                weaponLeft2AT = turretLeft2.GetComponent<AutoTurret>();
                weaponLeft2AT.SetPeacekeepermode(true);

                try { weaponRight2AT.UpdateFromInput(100, 1); } catch { }
                try { weaponLeft2AT.UpdateFromInput(100, 1); } catch { }

                turretsSpawned = true;
            }
            private void ResetRockets()
            {
                if(Time.time > lastTimeRocketShot + 120 && storageContainer.inventory.itemList.Count == 0 && storageContainer.inventory.itemList.Count < 12)
                {
                    storageContainer.inventory.AddItem(ItemManager.FindItemDefinition("ammo.rocket.fire"), 12);
                    instance.Puts($"Current pilot: transportCopter.GetDriver().displayName");
                    transportCopter.GetDriver().ChatMessage("Rockets reloaded!");
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
                var entityParent = GameManager.server.CreateEntity("assets/prefabs/tools/pager/pager.entity.prefab", pos, rot, true);
                entityParent.Spawn();
                entityParent.SetParent(parent);
                entityParent.transform.localPosition = position;
                entityParent.transform.localEulerAngles = rotation;
                AddEntityToData(entityParent, entityParent.transform.position);

                var entityTube = GameManager.server.CreateEntity("assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", pos, rot, true);
                entityTube.SetParent(entityParent);
                entityTube?.Spawn();

                AddEntityToData(entityTube, entityTube.transform.position);
                TubesEntities.Add(entityTube);
                TubesParents.Add(entityParent);
                return entityTube;
            }

            private Vector3 GetDirection(float accuracy)
            {
                return (Vector3)(Quaternion.Euler(UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f)), UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f)), UnityEngine.Random.Range((float)(-accuracy * 0.5f), (float)(accuracy * 0.5f))) * transportCopter.transform.forward);
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

                    Tubes.Add(new Vector3(-spread1 + offset_left_x, 1.4f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread1 + offset_right_x, 1.4f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread1 + offset_left_x, 1.4f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread1 + offset_left_x, 1.4f, z) + transportCopter.transform.localPosition);

                    Tubes.Add(new Vector3(-spread2 + offset_right_x, 1.1f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread2 + offset_left_x, 1.1f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread2 + offset_right_x, 1.1f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread2 + offset_left_x, 1.1f, z) + transportCopter.transform.localPosition);

                    Tubes.Add(new Vector3(-spread3 + offset_left_x, 0.85f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(-spread3 + offset_right_x, 0.85f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread3 + offset_left_x, 0.85f, z) + transportCopter.transform.localPosition);
                    Tubes.Add(new Vector3(spread3 + offset_right_x, 0.85f, z) + transportCopter.transform.localPosition);

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
                        lastTimeRocketShot = Time.time;
                        var itemAmount = storageContainer.inventory.FindItemsByItemName("ammo.rocket.fire").amount;
                        if (itemAmount > 0) { transportCopter.GetDriver().ChatMessage($"Rockets left: {storageContainer.inventory.FindItemsByItemName("ammo.rocket.fire").amount}"); }

                    }
                }
            }
            private RaycastHit hitInfo;
            private Vector3 FindTarget(Vector3 target, BasePlayer player)
            {
                if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, Mathf.Infinity, -1063040255))
                {
                }
                Vector3 hitpoint = hitInfo.point;
                return hitpoint;
            }
            private Vector3 target;
            public void FireTurretsGuns(BasePlayer player)
            {
                try
                {
                    if (weaponLeft2AT.IsOnline() == true)
                    {
                        weaponLeft2AT.Reload();
                        weaponLeft2AT.FireGun(FindTarget(target, player), ConVar.PatrolHelicopter.bulletAccuracy);
                    }
                }
                catch { }

                try
                {
                    if (weaponRight2AT.IsOnline() == true)
                    {
                        weaponRight2AT.Reload();
                        weaponRight2AT.FireGun(FindTarget(target, player), ConVar.PatrolHelicopter.bulletAccuracy);
                    }
                }
                catch { }
            }
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            try
            {
                var copter = player.GetMountedVehicle().GetComponent<ArmedTransportHelicopter>();
                copter.HelicopterInput(input, player);
            }
            catch
            {

            }
        }

    }
}