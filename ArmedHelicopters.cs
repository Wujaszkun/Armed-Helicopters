using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Armed Helicopters", "Wujaszkun", "2.1.1")]
    [Description("Armament for scrap transport helicopter and Minicopter")]
    internal class ArmedHelicopters : RustPlugin
    {
        public static ArmedHelicopters instance;
        public ScrapTransportHelicopter copter;
        private List<MiniCopter> helicopterList = new List<MiniCopter>();
        private readonly bool isLoggingEnabled = true;
        public CuiElementContainer hLines = new CuiElementContainer();
        private CuiElementContainer mainIndicators = new CuiElementContainer();
        private CuiElementContainer crosshair = new CuiElementContainer();
        private CuiElementContainer currentWeapon = new CuiElementContainer();
        private CuiElementContainer ammoCount = new CuiElementContainer();
        private CuiElementContainer rocketCount = new CuiElementContainer();
        private CuiElementContainer speedIndicators = new CuiElementContainer();
        private CuiElementContainer heightIndicators = new CuiElementContainer();
        private CuiElementContainer lines = new CuiElementContainer();

        private bool verticalLineShow = true;

        [ChatCommand("armtransportcopter")]
        private void ArmTransportCopters(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                ReloadCopterInformation();
                ArmHelicopter();
            }
        }

        private void OnServerInitialized()
        {
            Puts("PluginInit");
            instance = this;
            ReloadCopterInformation();
            ArmHelicopter();
        }

        private void Unload()
        {
            Puts("Unload");
            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                DestroyUI(player);
            }

            var armedCopters = GameObject.FindObjectsOfType<Armament>();

            foreach (Armament copter in armedCopters)
            {
                if (copter != null)
                {
                    copter.DespawnAllEntities();
                    GameObject.Destroy(copter);
                }
            }

            instance.verticalLineShow = true;
        }

        private void ReloadCopterInformation()
        {
            helicopterList = new List<MiniCopter>(GameObject.FindObjectsOfType<MiniCopter>());
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.gameObject.GetComponent<MiniCopter>() != null)
            {
                ReloadCopterInformation();
                ArmHelicopter();
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            ReloadCopterInformation();

            if (entity.gameObject.GetComponent<Armament>() != null)
            {
                instance.PrintToChat("Item dropped");
                if (entity.gameObject.GetComponent<MiniCopter>().GetDriver() != null)
                {
                    DestroyUI(entity.gameObject.GetComponent<MiniCopter>().GetDriver());
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

        private void ArmHelicopter()
        {
            foreach (MiniCopter heliBaseEnt in helicopterList)
            {
                if (heliBaseEnt.GetComponent<MiniCopter>() != null && heliBaseEnt.GetComponent<Armament>() == null)
                {
                    heliBaseEnt.gameObject.AddComponent<Armament>();
                }
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            try
            {
                Armament copter = player.GetMountedVehicle().GetComponent<Armament>();
                copter.HelicopterInput(input, player);
            }
            catch { }
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (!player.isMounted)
            {
                instance.verticalLineShow = true;
                DestroyUI(player);
            }
        }

        private class Armament : MonoBehaviour
        {
            private static HelicopterType baseHeliType;
            private MiniCopter baseHelicopter;

            private Vector3 position;
            private Quaternion rotation;

            private bool canFireRockets = true;
            private readonly List<Vector3> Tubes = new List<Vector3>();
            private List<BaseEntity> TubesEntities = new List<BaseEntity>();
            private readonly List<BaseEntity> TubesParents = new List<BaseEntity>();

            private int currentTubeIndex;
            private float nextActionTime;
            private readonly float period = 1;

            private float lastShot;

            private AutoTurret turret1;
            private AutoTurret turret2;
            private AutoTurret turret3;
            private AutoTurret turret4;

            public Dictionary<uint, string> spawnedEntityList = new Dictionary<uint, string>();
            public Dictionary<uint, BaseEntity> spawnedBaseEntityList = new Dictionary<uint, BaseEntity>();
            private int numberOfRockets = 12;

            private Vector3 target;
            private int index = 0;
            private float nextShootTime;

            private bool canGunShoot = true;

            private float timeReloadGuns;
            private float timeReloadRockets;

            private float nextShootTimeRockets;

            private CuiElementContainer hLinesHeli = new CuiElementContainer();
            private Vector3 lastPosition = Vector3.zero;

            private int turretsCount;
            private List<Gun> gunList = new List<Gun>();
            private int previousCrosshairSize = 0;
            private string previousWeapon;
            private string previousAmmoCount;
            private int previousNUmberOfRockets;
            private float previousSpeedText;
            private float previousHValue;
            private float nextShootTimeGuns;
            private int showHUD = 0;

            #region Common
            private enum HelicopterType
            {
                Transport,
                Mini
            }

            private void Awake()
            {
                InitGunList();
                instance.Puts($"Guns listed: {gunList.Count}");

                SetType();
                instance.Puts($"Helicopter type: {baseHeliType}");

                baseHelicopter = this.gameObject.GetComponent<MiniCopter>();
                instance.Puts($"Base helicopter name: {baseHelicopter.name}");

                position = baseHelicopter.transform.position;
                rotation = baseHelicopter.transform.rotation;
                instance.Puts($"Base helicopter position: {position}");
                instance.Puts($"Base helicopter rotation: {rotation}");

                lastShot = Time.time;

                SpawnArmament();

                currentTubeIndex = 0;
                ChangeGun();
                instance.Puts("Initialized");
            }

            private void InitGunList()
            {
                gunList = new List<Gun>
                {
                    new Gun("AK Automatic Rifle","rifle.ak" ,"ammo.rifle", 72),
                    new Gun("Bolt Action Rifle","rifle.bolt" ,"ammo.rifle.hv", 30),
                    new Gun("L96 Sniper Rifle","rifle.l96" ,"ammo.rifle.hv", 30),
                    new Gun("LR300 Automatic Rfile","rifle.lr300" ,"ammo.rifle", 72),
                    new Gun("M249 Machinegun","lmg.m249" ,"ammo.rifle" , 100),
                    new Gun("M39 DMR" ,"rifle.m39" ,"ammo.rifle.hv" , 36),
                    new Gun("M92 Pistol","pistol.m92" ,"ammo.pistol", 48),
                    new Gun("MP5 Machine Pistol","smg.mp5" ,"ammo.pistol", 64),
                    new Gun("Python Revolver" ,"pistol.python" ,"ammo.pistol", 48),
                    new Gun("Revolver","pistol.revolver" ,"ammo.pistol", 48),
                    new Gun("Semiauto Pistol","pistol.semiauto" ,"ammo.pistol", 48),
                    new Gun("Custom Machine Pistol","smg.2" ,"ammo.pistol", 64),
                    new Gun("Thompson Machine Pistol","smg.thompson","ammo.pistol", 64),
                    new Gun("Double Barrel Shotgun","shotgun.double","ammo.shotgun.fire", 240),
                    new Gun("Pump Action Shotgun","shotgun.pump","ammo.shotgun", 240),
                    new Gun("Spas 12 Shotgun" ,"shotgun.spas12","ammo.shotgun.slug", 72)
                };
            }

            private void SetType()
            {
                baseHeliType = this.gameObject.name.Contains("transport") ? HelicopterType.Transport : HelicopterType.Mini;
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
                ChangeGun();
            }

            private void FixedUpdate()
            {

                if (baseHelicopter.GetDriver() != null)
                {

                    var driver = baseHelicopter.GetDriver();
                    showHUDMethod(driver);

                    switch (baseHeliType)
                    {
                        case HelicopterType.Mini:
                            KeepTurretsFacingFront(turret1);
                            KeepTurretsFacingFront(turret2);
                            KeepTurretsFacingFront(turret3);
                            KeepTurretsFacingFront(turret4);
                            turret1.SetTarget(null);
                            turret2.SetTarget(null);
                            turret3.SetTarget(null);
                            turret4.SetTarget(null);
                            break;

                        case HelicopterType.Transport:
                            KeepTurretsFacingFront(turret1);
                            KeepTurretsFacingFront(turret2);
                            turret1.SetTarget(null);
                            turret2.SetTarget(null);

                            ResetAmmo();
                            break;
                    }
                    SetCanShootGuns();
                    SetCanShootRockets();
                }
            }

            private void showHUDMethod(BasePlayer driver)
            {
                if (baseHeliType == HelicopterType.Transport)
                {
                    ShowUICrosshair(driver, 72);
                    ShowUIRocketCount(driver);
                }
                if (baseHeliType == HelicopterType.Mini)
                {
                    ShowUICrosshair(driver, GetCrosshairSize());
                    ShowUIAmmoCount(driver, $"Ammo Count: {GetTurretAmmoCount()}");
                    ShowUICurrentWeapon(driver, $"Current Weapon: {GetCurrentWeaponName()}");
                }

                ShowRollLinesUI(driver, NormalizeZ(), NormalizeX());
                ShowSpeedUI(driver);
                ShowHeightUI(driver);
                instance.verticalLineShow = false;
            }

            private void KeepTurretsFacingFront(AutoTurret turret)
            {
                if (baseHelicopter.GetDriver() != null)
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

            private object GetTurretAmmoCount()
            {
                string result;
                string ammoType0 = turret1.GetAttachedWeapon().primaryMagazine.ammoType.shortname;
                int ammoAmount0 = turret1.GetAttachedWeapon().primaryMagazine.contents;

                if (IsReloading())
                {
                    result = $"Reloading ({(nextShootTime - Time.time).ToString("0.0")} seconds).";
                }
                else
                {
                    result = $"{ammoAmount0} {ammoType0}";
                }

                return result;

            }
            private bool IsReloading()
            {
                if (nextShootTime - Time.time > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private float NormalizeX()
            {
                float x = baseHelicopter.transform.rotation.eulerAngles.x;

                if (x < 90)
                {
                    x = -x;
                }

                if (x > 270)
                {
                    x = 360 - x;
                }

                return x;
            }
            private float NormalizeZ()
            {
                float z = baseHelicopter.transform.rotation.eulerAngles.z;

                if (z < 90)
                {
                    z = -z;
                }

                if (z > 270)
                {
                    z = 360 - z;
                }

                return z;
            }
            private void SetCanShootRockets()
            {
                if (numberOfRockets < 1)
                {
                    canFireRockets = false;
                }

                if (numberOfRockets > 0)
                {
                    canFireRockets = true;
                }
            }

            private void SetCanShootGuns()
            {
                if (!canGunShoot)
                {
                    timeReloadGuns = nextShootTimeGuns - Time.time;
                }

                if (!canGunShoot && Time.time >= nextShootTimeGuns)
                {
                    canGunShoot = true;
                }
            }

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
                foreach (KeyValuePair<uint, BaseEntity> entity in spawnedBaseEntityList)
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
            private AutoTurret SpawnTurret(Vector3 position, Vector3 rotationEuler, BaseEntity parent)
            {
                BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", this.position, rotation, true);
                if (parent != null)
                {
                    entity.SetParent(parent, 0);
                    entity.transform.localEulerAngles = rotationEuler;
                    entity.transform.localPosition = position;
                }
                entity?.Spawn();

                var turret = entity.GetComponent<AutoTurret>();
                turret.SetPeacekeepermode(true);
                turret.InitializeControl(null);
                turret.UpdateFromInput(100, 0);

                AddEntityToData(entity, entity.transform.position);

                return turret;
            }

            private BaseEntity SpawnBaseEntity(Vector3 position, Vector3 rotationEuler, BaseEntity parent, string prefab)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefab, baseHelicopter.transform.position, baseHelicopter.transform.rotation, true);

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

            public void HelicopterInput(InputState inputState, BasePlayer player)
            {
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.WasJustPressed(BUTTON.USE))
                {
                    previousCrosshairSize = 0;
                    instance.PrintToChat(showHUD.ToString());
                    if (showHUD == 0)
                    { showHUD = 1; return; }

                    if (showHUD == 1)
                    { showHUD = 2; return; }

                    if (showHUD == 2)
                    { showHUD = 0; return; }
                }
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_PRIMARY) && baseHeliType == HelicopterType.Mini)
                {
                    FireTurretsGuns(player, turret1);
                    FireTurretsGuns(player, turret2);
                    FireTurretsGuns(player, turret3);
                    FireTurretsGuns(player, turret4);
                }
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_PRIMARY) && baseHeliType == HelicopterType.Transport)
                {
                    FireTurretsGuns(player, turret1);
                    FireTurretsGuns(player, turret2);
                }
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.IsDown(BUTTON.FIRE_SECONDARY) && baseHeliType == HelicopterType.Transport)
                {
                    FireTurretsRockets(player);
                }
                if (baseHelicopter.GetPlayerSeat(player) == 0 && inputState.WasJustPressed(BUTTON.RELOAD) && baseHeliType == HelicopterType.Mini)
                {
                    if (index == gunList.Count - 1)
                    {
                        index = 0;
                    }
                    else
                    {
                        index++;
                    }

                    ChangeGun();
                }
            }

            private void ResetAmmo()
            {
                if (Time.time > lastShot + 30 && numberOfRockets < 1)
                {
                    numberOfRockets = 12;
                }
            }

            #endregion Common

            #region Mini

            private void KeepFacingFrontMini(AutoTurret turret, Vector3 target)
            {
                if (turret != null && turret?.IsOnline() == true)
                {
                    turret.aimDir = target;
                    turret?.SendAimDir();
                    turret?.UpdateAiming();
                }
            }

            public void SpawnWings()
            {
                SpawnBaseEntity(new Vector3(-0.3f, 0.2f, 0f), new Vector3(90, 0, 90), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
                SpawnBaseEntity(new Vector3(0.3f, 0.2f, 0f), new Vector3(90, 0, 270), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");

                SpawnBaseEntity(new Vector3(-0.3f, 0.75f, 0f), new Vector3(90, 0, 90), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
                SpawnBaseEntity(new Vector3(0.3f, 0.75f, 0f), new Vector3(90, 0, 270), baseHelicopter, "assets/prefabs/deployable/signs/sign.post.single.prefab");
            }
            private void SpawnGuns()
            {
                turret1 = SpawnTurret(new Vector3(1f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter);
                turret2 = SpawnTurret(new Vector3(2f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter);
                turret3 = SpawnTurret(new Vector3(-1f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter);
                turret4 = SpawnTurret(new Vector3(-2f, 0.5f, 0f), new Vector3(90, 0, 0), baseHelicopter);
                turretsCount = 4;
            }

            private int GetCrosshairSize()
            {
                return gunList[index].CrossHairSize;
            }
            private string GetCurrentWeaponName()
            {
                return gunList[index].GunName;
            }
            private void ChangeGun()
            {
                switch (baseHeliType)
                {
                    case HelicopterType.Mini:
                        AttachWeapon(turret1);
                        AttachWeapon(turret2);
                        AttachWeapon(turret3);
                        AttachWeapon(turret4);
                        break;

                    case HelicopterType.Transport:
                        AttachWeapon(turret1);
                        AttachWeapon(turret2);
                        break;
                }
            }

            private void AttachWeapon(AutoTurret turret)
            {
                if (turret != null)
                {
                    instance.Puts(gunList[index].GunItemName);

                    ItemManager.CreateByName(gunList[index].GunItemName, 1).MoveToContainer(turret.inventory, 0);
                    ItemManager.CreateByName(gunList[index].AmmoType, 1000).MoveToContainer(turret.inventory, 1);

                    turret.UpdateAttachedWeapon();
                    turret.Reload();
                    turret.isLootable = false;
                    turret.dropChance = 0;
                }
            }
            public void FireTurretsGuns(BasePlayer player, AutoTurret turret)
            {
                if (turret.IsOnline() == true && canGunShoot)
                {
                    if (turret.GetAttachedWeapon().AmmoFraction() <= 0)
                    {
                        nextShootTime = Time.time + turret.GetAttachedWeapon().GetReloadDuration();
                        TopUpAmmo();
                        canGunShoot = false;
                    }
                    turret.FireAttachedGun(target, ConVar.PatrolHelicopter.bulletAccuracy);
                }
            }

            private void TopUpAmmo()
            {
                turret1.GetAttachedWeapon().TopUpAmmo();
                turret2.GetAttachedWeapon().TopUpAmmo();
                turret3.GetAttachedWeapon().TopUpAmmo();
                turret4.GetAttachedWeapon().TopUpAmmo();
            }

            #endregion Mini

            #region Transport

            private void MakeDoorsInactive(BaseEntity entity)
            {
                Door door = entity.GetComponent<Door>();
                if (door != null)
                {
                    door.canHandOpen = false;
                    door.canNpcOpen = false;
                    door.canTakeCloser = false;
                    door.canTakeKnocker = false;
                    door.canTakeLock = false;
                }
            }
            private BaseEntity SpawnArmament(Vector3 position, Vector3 rotation, BaseEntity parent)
            {
                BaseEntity entityParent = GameManager.server.CreateEntity("assets/prefabs/tools/pager/pager.entity.prefab", this.position, this.rotation, true);
                entityParent.Spawn();
                entityParent.SetParent(parent);
                entityParent.transform.localPosition = position;
                entityParent.transform.localEulerAngles = rotation;
                AddEntityToData(entityParent, entityParent.transform.position);

                BaseEntity entityTube = GameManager.server.CreateEntity("assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", this.position, this.rotation, true);
                entityTube.SetParent(entityParent);
                entityTube?.Spawn();

                AddEntityToData(entityTube, entityTube.transform.position);
                TubesEntities.Add(entityTube);
                TubesParents.Add(entityParent);
                return entityTube;
            }
            internal void SpawnRockets()
            {
                if (baseHelicopter == null)
                {
                    instance.Puts("Base heli is null");
                }

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

                turret1 = SpawnTurret(new Vector3(2f, 1.4f, 0.5f), new Vector3(180, 0, 0), baseHelicopter);
                turret2 = SpawnTurret(new Vector3(-2f, 1.4f, 0.5f), new Vector3(180, 0, 0), baseHelicopter);

                turretsCount = 2;
            }
            #endregion Transport

            private Vector3 GetDirection(float accuracy)
            {
                return Quaternion.Euler(UnityEngine.Random.Range(-accuracy * 0.5f, accuracy * 0.5f), UnityEngine.Random.Range(-accuracy * 0.5f, accuracy * 0.5f), UnityEngine.Random.Range(-accuracy * 0.5f, accuracy * 0.5f)) * baseHelicopter.transform.forward;
            }

            internal void FireTurretsRockets(BasePlayer player)
            {
                if (Time.time > lastShot + 0.25 && numberOfRockets > 0)
                {
                    string projectile = "assets/prefabs/ammo/rocket/rocket_fire.prefab";

                    numberOfRockets -= 1;

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

                    Vector3 direction = GetDirection(4f);

                    BaseEntity rocketsR = GameManager.server.CreateEntity(projectile, originR);

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

                        rocketsR.SendMessage("InitializeVelocity", direction * 75f);
                        rocketsR.Spawn();

                        lastShot = Time.time;
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

            private void ShowUICrosshair(BasePlayer player, int crosshairSize)
            {


                DestroyUICrosshair(player);
                LabelOnScreen(player, new Vector2(0f, 0f), "◎", instance.crosshair, "crosshair", crosshairSize);
                CuiHelper.AddUi(player, instance.crosshair);

            }

            private void ShowUICurrentWeapon(BasePlayer player, string currentWeapon)
            {
                DestroyUICurrentWeapon(player);
                if (showHUD == 0)
                {
                    if (previousWeapon != currentWeapon)
                    {

                        LabelOnScreen(player, new Vector2(0f, -0.2f), currentWeapon, instance.currentWeapon, "currentWeapon");
                        CuiHelper.AddUi(player, instance.currentWeapon);
                        previousWeapon = currentWeapon;
                    }
                }
            }

            private void ShowUIAmmoCount(BasePlayer player, string ammoCount)
            {
                DestroyUIAmmoCount(player);
                if (showHUD == 0)
                {
                    if (previousAmmoCount != ammoCount)
                    {

                        LabelOnScreen(player, new Vector2(0f, -0.25f), ammoCount, instance.ammoCount, "ammoCount");
                        CuiHelper.AddUi(player, instance.ammoCount);
                        previousAmmoCount = ammoCount;
                    }
                }
            }

            private void ShowUIRocketCount(BasePlayer player)
            {
                DestroyUIRocketCount(player);
                if (showHUD == 0)
                {
                    var reloadTime = lastShot + 30 - Time.time;

                    if (baseHeliType == HelicopterType.Transport)
                    {
                        if (canFireRockets)
                        {
                            LabelOnScreen(player, new Vector2(0f, -0.30f), $"Rockets: {numberOfRockets} ", instance.rocketCount, "rocketCount");
                        }
                        if (!canFireRockets && reloadTime > 0)
                        {
                            LabelOnScreen(player, new Vector2(0f, -0.30f), $"Rockets: reloading ({reloadTime} seconds). ", instance.rocketCount, "rocketCount");
                        }
                    }
                    CuiHelper.AddUi(player, instance.rocketCount);
                }
            }

            private void ShowSpeedUI(BasePlayer player)
            {
                DestroyUiSpeed(player);
                if (showHUD == 0)
                {
                    Vector3 currentPosition = baseHelicopter.transform.position;
                    float speed = (Vector3.Distance(lastPosition, currentPosition) / Time.deltaTime) * 1.8f;

                    if (previousSpeedText != speed)
                    {
                        lastPosition = currentPosition;

                        Vector2 speedArrowPoint = new Vector2(-0.198f, (speed * 0.005f) - 0.24f);
                        string pointerTextSpeed = $"{speed:0.0} km/h ►";


                        ButtonOnScreen(player, new Vector2(-0.168f, 0), 0.48f, 0.002f, instance.speedIndicators, "speedBar");
                        LabelOnScreen(player, speedArrowPoint, pointerTextSpeed, instance.speedIndicators, "speedPointer");

                        CuiHelper.AddUi(player, instance.speedIndicators);
                        previousSpeedText = speed;
                    }
                }
            }

            private void ShowHeightUI(BasePlayer player)
            {
                DestroyUiHeight(player);
                if (showHUD == 0)
                {
                    float hValue = GetHeightValue();

                    if (previousHValue != hValue)
                    {
                        Vector2 pointerPoint = new Vector2(0.191f, (hValue * 0.0025f) - 0.24f);
                        string pointerTextHeight = $"◄ {hValue:0.0} m";


                        ButtonOnScreen(player, new Vector2(0.168f, 0), 0.48f, 0.0025f, instance.heightIndicators, "heightBar");
                        LabelOnScreen(player, pointerPoint, pointerTextHeight, instance.heightIndicators, "heightPointer");

                        CuiHelper.AddUi(player, instance.heightIndicators);
                        previousHValue = hValue;
                    }
                }
            }

            private void LabelOnScreen(BasePlayer player, Vector2 pointCoords, string text, CuiElementContainer container, string elementName = "", int fontSize = 16)
            {
                Vector2 point = new Vector2(0.5f, 0.5f) + pointCoords;
                string colorGreen = "0 255 0 1";
                TextAnchor align = TextAnchor.MiddleCenter;
                float h = 0.2f;

                float xmin = point.x - h;
                float xmax = point.x + h;
                float ymin = point.y - h;
                float ymax = point.y + h;

                if (elementName == "")
                {
                    elementName = $"{text}_{pointCoords}_{player.net.ID}";
                }

                container.Add(new CuiLabel
                {
                    Text = { Color = colorGreen, FontSize = fontSize, Align = align, Text = text },
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmax} {ymax}" }
                }, "Hud", elementName);
            }

            private void ButtonOnScreen(BasePlayer player, Vector2 pointCoords, float height, float width, CuiElementContainer container, string elementName = "")
            {
                Vector2 point = new Vector2(0.5f, 0.5f) + pointCoords;
                string colorGreen = "0 255 0 1";
                TextAnchor align = TextAnchor.MiddleCenter;

                float xmin = point.x - width / 2;
                float xmax = point.x + width / 2;
                float ymin = point.y - height / 2;
                float ymax = point.y + height / 2;

                if (elementName == "")
                {
                    elementName = $"button_{pointCoords}_{player.net.ID}";
                }

                container.Add(new CuiPanel
                {
                    Image = { Color = colorGreen },
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmax} {ymax}" }
                }, "Hud", elementName);
            }

            private float GetHeightValue()
            {
                RaycastHit hitInfo;

                if (!UnityEngine.Physics.Raycast(baseHelicopter.transform.position, -baseHelicopter.transform.up, out hitInfo, Mathf.Infinity, -1063040255))
                {
                }

                return hitInfo.distance;
            }
            private void ShowRollLinesUI(BasePlayer player, float angleValue, float pitchValue)
            {
                DestroyHLines(player);
                if (showHUD == 0 || showHUD == 1)                    
                {
                    string colorGreen = "0 255 0 1";
                    float angle = angleValue / 45;
                    int fontSize = 24;
                    string text = "┈";
                    Vector2 screenMiddle = new Vector2(0.5f, 0.5f);

                    float h = 0.05f;

                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 newCenter = screenMiddle + new Vector2(0, (-pitchValue / 180));

                        instance.hLines = hLinesHeli;
                        for (int i = 0; i < 5; i++)
                        {
                            float newY = (float)((0.04f + (0.02f * i)) * Math.Sin(angle));
                            float newX = (float)((0.04f + (0.02f * i)) * Math.Cos(angle));
                            Vector2 point = newCenter + new Vector2(newX, newY);

                            float xmin = point.x - h;
                            float xmax = point.x + h;
                            float ymin = point.y - h;
                            float ymax = point.y + h;

                            hLinesHeli.Add(new CuiLabel
                            {
                                Text = { Color = colorGreen, FontSize = fontSize, Align = TextAnchor.MiddleCenter, Text = text },
                                RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmax} {ymax}" }
                            }, "Hud", $"pointR{k}{i}");

                            point = newCenter - new Vector2(newX, newY);

                            xmin = point.x - h;
                            xmax = point.x + h;
                            ymin = point.y - h;
                            ymax = point.y + h;

                            hLinesHeli.Add(new CuiLabel
                            {
                                Text = { Color = colorGreen, FontSize = fontSize, Align = TextAnchor.MiddleCenter, Text = text },
                                RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmax} {ymax}" }
                            }, "Hud", $"pointL{k}{i}");
                        }

                    }

                    float newY2 = (float)((0.04f + (0.02f * 5f)) * Math.Sin(angle));
                    float newX2 = (float)((0.04f + (0.02f * 5f)) * Math.Cos(angle));

                    var pointValue = screenMiddle + new Vector2(0, (-pitchValue / 180)) + new Vector2(newX2, newY2);

                    float xmin2 = pointValue.x - h;
                    float xmax2 = pointValue.x + h;
                    float ymin2 = pointValue.y - h;
                    float ymax2 = pointValue.y + h;

                    hLinesHeli.Add(new CuiLabel
                    {
                        Text = { Color = colorGreen, FontSize = 12, Align = TextAnchor.MiddleCenter, Text = pitchValue.ToString("0.0") },
                        RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmax2} {ymax2}" }
                    }, "Hud", $"pointRValue");

                    pointValue = screenMiddle + new Vector2(0, (-pitchValue / 180)) - new Vector2(newX2, newY2);

                    xmin2 = pointValue.x - h;
                    xmax2 = pointValue.x + h;
                    ymin2 = pointValue.y - h;
                    ymax2 = pointValue.y + h;

                    hLinesHeli.Add(new CuiLabel
                    {
                        Text = { Color = colorGreen, FontSize = 12, Align = TextAnchor.MiddleCenter, Text = pitchValue.ToString("0.0") },
                        RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmax2} {ymax2}" }
                    }, "Hud", $"pointLValue");


                    CuiHelper.AddUi(player, hLinesHeli);
                }
            }
            public void DestroyUICrosshair(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "crosshair");
                instance.crosshair.Clear();
            }
            public void DestroyUICurrentWeapon(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "currentWeapon");
                instance.currentWeapon.Clear();
            }
            public void DestroyUIAmmoCount(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "ammoCount");
                instance.ammoCount.Clear();
            }
            public void DestroyUIRocketCount(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "rocketCount");
                instance.rocketCount.Clear();
            }
            public void DestroyUiMain(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "crosshair");
                CuiHelper.DestroyUi(player, "currentWeapon");
                CuiHelper.DestroyUi(player, "ammoCount");
                CuiHelper.DestroyUi(player, "rocketCount");
                instance.mainIndicators.Clear();
            }

            public void DestroyUiSpeed(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "speedPointer");
                CuiHelper.DestroyUi(player, "speedBar");
                instance.speedIndicators.Clear();
            }

            public void DestroyUiHeight(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "heightPointer");
                CuiHelper.DestroyUi(player, "heightBar");
                instance.heightIndicators.Clear();
            }

            private void DestroyHLines(BasePlayer player)
            {
                foreach (CuiElement el in hLinesHeli)
                {
                    CuiHelper.DestroyUi(player, el.Name);
                }

                hLinesHeli.Clear();
            }

            private void DestroyStaticLines(BasePlayer player)
            {
                for (int i = 0; i < 13; i++)
                {
                    CuiHelper.DestroyUi(player, $"lines{i * 0.002f}{-0.16f}");
                    CuiHelper.DestroyUi(player, $"lines{i * -0.002f}{-0.16f}");
                    CuiHelper.DestroyUi(player, $"lines{i * 0.002f}{0.16f}");
                    CuiHelper.DestroyUi(player, $"lines{i * -0.002f}{0.16f}");
                }
                instance.verticalLineShow = true;
                instance.lines.Clear();
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            instance.verticalLineShow = true;

            for (int i = 0; i < 13; i++)
            {
                CuiHelper.DestroyUi(player, $"lines{i * 0.002f}{-0.16f}");
                CuiHelper.DestroyUi(player, $"lines{i * -0.002f}{-0.16f}");
                CuiHelper.DestroyUi(player, $"lines{i * 0.002f}{0.16f}");
                CuiHelper.DestroyUi(player, $"lines{i * -0.002f}{0.16f}");
            }


            CuiHelper.DestroyUi(player, "crosshair");
            CuiHelper.DestroyUi(player, "currentWeapon");
            CuiHelper.DestroyUi(player, "ammoCount");
            CuiHelper.DestroyUi(player, "rocketCount");

            CuiHelper.DestroyUi(player, "speedPointer");
            CuiHelper.DestroyUi(player, "speedBar");

            CuiHelper.DestroyUi(player, "heightPointer");
            CuiHelper.DestroyUi(player, "heightBar");

            CuiHelper.DestroyUi(player, "pitch0");
            CuiHelper.DestroyUi(player, "pitch1");
            CuiHelper.DestroyUi(player, "pitch2");
            CuiHelper.DestroyUi(player, "pitch3");
            CuiHelper.DestroyUi(player, "pitch4");
            CuiHelper.DestroyUi(player, "pitch5");
            CuiHelper.DestroyUi(player, "pitchMain");

            CuiHelper.DestroyUi(player, "pitch-0");
            CuiHelper.DestroyUi(player, "pitch-1");
            CuiHelper.DestroyUi(player, "pitch-2");
            CuiHelper.DestroyUi(player, "pitch-3");
            CuiHelper.DestroyUi(player, "pitch-4");
            CuiHelper.DestroyUi(player, "pitch-5");

            foreach (CuiElement el in hLines)
            {
                CuiHelper.DestroyUi(player, el.Name);
            }

            instance.mainIndicators.Clear();
            instance.speedIndicators.Clear();
            instance.heightIndicators.Clear();

            instance.crosshair.Clear();
            instance.currentWeapon.Clear();
            instance.ammoCount.Clear();
            instance.rocketCount.Clear();

            instance.lines.Clear();
            instance.hLines.Clear();
        }
        public class Gun : MonoBehaviour
        {
            public Gun(string gunName, string gunItemName,
                string ammoType, int crossHairSize)
            {
                GunName = gunName;
                GunItemName = gunItemName;
                AmmoType = ammoType;
                CrossHairSize = crossHairSize;
            }

            public string GunName { get; set; }
            public string GunItemName { get; set; }
            public string AmmoType { get; set; }
            public int CrossHairSize { get; set; }
        }
    }
}