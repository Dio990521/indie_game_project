using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Combat.States;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗管理器（常驻单例，由 GameBootstrapper 创建，挂在 [GameSystem] 根节点下）：
    /// 与 BoardGameManager 同构——管理器本身跨场景常驻，但依赖的场景内容（CombatSceneRefs）
    /// 随 Combat 场景 Additive 加载/卸载而生灭，通过 GameModeChangedEvent 动态解析/清理，
    /// 不在 Combat 模式时整体休眠（Sleep），避免持有已销毁场景对象的悬空引用。
    ///
    /// 职责：
    /// - 驱动 主+Overlay 双状态机（参照 BoardGameManager 的模式）；
    /// - 统一消费战斗输入事件（技能/上下场/名册切换），按规则转发或拒绝；
    /// - 生成/回收战斗单位与弹道（按预制体分池复用，池随本管理器常驻）；
    /// - 中心化订阅 DeathEvent 做幂等死亡处理与胜负判定。
    /// </summary>
    public class CombatManager : MonoSingleton<CombatManager>
    {
        [Header("外部依赖")]
        [Tooltip("输入读取器资产（与 GameBootstrapper 使用的同一份）")]
        [SerializeField] private GameInputReader inputReader;

        [Tooltip("战斗全局参数配置")]
        [SerializeField] private CombatConfigSO config;

        // 场景引用聚合（出生点/放置指示器/战斗相机）：
        // 不再走 Inspector 序列化——本管理器常驻跨场景，无法在 Inspector 里预先绑定
        // 某个具体战斗场景的对象，改为进入 Combat 模式时用 FindAnyObjectByType 动态解析
        // （与 BoardGameManager 解析 movementController 的方式一致）。
        private CombatSceneRefs sceneRefs;

        // 是否处于 Combat 模式（由 GameModeChangedEvent 驱动，Update 据此决定是否驱动状态机）
        private bool _isCombatModeActive;

        // 重写单例属性：与 BoardGameManager/UIManager/GameManager 保持一致——
        // 实际跨场景保留由 GameBootstrapper 的 [GameSystem] 根节点（DontDestroyRoot）提供，
        // 本管理器始终作为其子物体实例化，这里的 KeepAcrossScenes 仅用于语义一致性。
        protected override bool KeepAcrossScenes => true;

        // --- 状态机（主 + Overlay，参照 BoardGameManager）---
        private readonly StateMachine<CombatManager> _stateMachine = new StateMachine<CombatManager>();
        private readonly StateMachine<CombatManager> _overlayStateMachine = new StateMachine<CombatManager>();

        /// <summary> 主状态机当前状态 </summary>
        public BaseState<CombatManager> CurrentState => _stateMachine.CurrentState;

        /// <summary> Overlay 状态机当前状态（放置态等） </summary>
        public BaseState<CombatManager> OverlayState => _overlayStateMachine.CurrentState;

        // --- 核心数据 ---
        /// <summary> 单位注册表（索敌/技能目标筛选） </summary>
        public CombatUnitRegistry Registry { get; } = new CombatUnitRegistry();

        /// <summary> 战斗名册（选择指针/上下场规则） </summary>
        public CombatRoster Roster { get; } = new CombatRoster();

        /// <summary> 战斗是否进行中（结算后为 false，所有单位组件据此停摆） </summary>
        public bool BattleRunning { get; private set; }

        /// <summary> 最近一场战斗是否胜利（CombatExitState 读取） </summary>
        public bool LastBattleVictory { get; private set; }

        /// <summary> 输入读取器（放置态读取指针/摇杆缓存用） </summary>
        public GameInputReader InputReader => inputReader;

        /// <summary> 战斗全局参数 </summary>
        public CombatConfigSO Config => config;

        /// <summary> 场景引用聚合 </summary>
        public CombatSceneRefs SceneRefs => sceneRefs;

        // 战斗是否已启动（StartBattle 后为 true，Update 才驱动状态机）
        private bool _battleStarted;
        // 本场遭遇配置
        private EncounterSO _encounter;
        // 我方队伍等级（取自常驻探索玩家的等级，全员共用）
        private int _partyLevel = 1;

        // --- 波次刷怪 ---
        private int _nextWaveIndex;
        private float _nextWaveTime;

        /// <summary> 是否已生成全部波次（胜利判定条件之一） </summary>
        public bool AllWavesSpawned =>
            _encounter == null || _encounter.Waves == null || _nextWaveIndex >= _encounter.Waves.Count;

        // --- 对象池（战斗体按角色/敌人预制体分池；弹道按弹道预制体分池）---
        private readonly Dictionary<GameObject, GameObjectPool> _poolByPrefab = new Dictionary<GameObject, GameObjectPool>(8);
        private readonly Dictionary<GameObject, GameObjectPool> _poolByInstance = new Dictionary<GameObject, GameObjectPool>(32);
        // 当前活动（已生成未回池）的战斗单位，重开战斗时统一回收
        private readonly List<CombatUnit> _activeUnits = new List<CombatUnit>(12);
        // 池根节点（单位与弹道分开挂，便于调试查看）
        private Transform _unitPoolRoot;
        private Transform _projectilePoolRoot;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            // 创建池根节点（本管理器的子物体，随管理器常驻跨场景保留）
            _unitPoolRoot = new GameObject("[UnitPool]").transform;
            _unitPoolRoot.SetParent(transform, false);
            _projectilePoolRoot = new GameObject("[ProjectilePool]").transform;
            _projectilePoolRoot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            // 场景模式切换：驱动场景引用的动态解析/清理（见 HandleGameModeChanged）
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
            // 战斗输入（GameInputReader 的战斗扩展 action 广播）
            EventBus.Subscribe<InputSkillEvent>(HandleSkillInput);
            EventBus.Subscribe<InputDeployEvent>(HandleDeployInput);
            EventBus.Subscribe<InputSelectEvent>(HandleSelectInput);
            // 死亡事件中心化订阅（幂等处理 + 胜负判定）
            EventBus.Subscribe<DeathEvent>(HandleDeath);
            // ESC/手柄B 取消（放置态用）
            if (inputReader != null) inputReader.UICancelEvent += HandleUICancel;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
            EventBus.Unsubscribe<InputSkillEvent>(HandleSkillInput);
            EventBus.Unsubscribe<InputDeployEvent>(HandleDeployInput);
            EventBus.Unsubscribe<InputSelectEvent>(HandleSelectInput);
            EventBus.Unsubscribe<DeathEvent>(HandleDeath);
            if (inputReader != null) inputReader.UICancelEvent -= HandleUICancel;
        }

        /// <summary>
        /// 场景模式变化响应：
        /// 进入 Combat 场景 → 动态解析本场景的 CombatSceneRefs（场景对象已在 Awake 阶段全部就绪，
        /// 此时 FindAnyObjectByType 必定可靠命中）；离开 Combat 模式 → 整体休眠清理，
        /// 避免常驻的本管理器持有已随场景销毁的悬空引用。
        /// </summary>
        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            _isCombatModeActive = evt.Mode == GameMode.Combat;
            if (_isCombatModeActive)
            {
                sceneRefs = FindAnyObjectByType<CombatSceneRefs>();
                if (sceneRefs == null)
                {
                    DebugTools.LogWarning("[CombatManager] 进入 Combat 场景但未找到 CombatSceneRefs，战斗将无法初始化。");
                }
                return;
            }
            Sleep();
        }

        /// <summary>
        /// 休眠：离开 Combat 模式时调用（正常返回棋盘、或独立测试场景卸载）。
        /// 清空状态机、回收全部在场单位、重置战斗状态，并释放场景引用。
        /// </summary>
        private void Sleep()
        {
            PopOverlayState();
            _stateMachine.Clear(this);
            while (_activeUnits.Count > 0)
            {
                ReleaseUnit(_activeUnits[_activeUnits.Count - 1]);
            }
            Registry.Clear();
            BattleRunning = false;
            _battleStarted = false;
            _encounter = null;
            sceneRefs = null;
        }

        private void Update()
        {
            if (!_isCombatModeActive || !_battleStarted) return;
            // Overlay 优先（放置态的指向解析先于主状态机逻辑）
            _overlayStateMachine.Update(this);
            _stateMachine.Update(this);
        }

        // ===================== 状态机接口 =====================

        /// <summary>
        /// 切换主状态。
        /// </summary>
        public void ChangeState(BaseState<CombatManager> newState)
        {
            if (newState == null) return;
            _stateMachine.ChangeState(newState, this);
        }

        /// <summary>
        /// 压入 Overlay 状态（战斗同时只允许一个 Overlay）。
        /// </summary>
        public void PushOverlayState(BaseState<CombatManager> newState)
        {
            if (newState == null) return;
            PopOverlayState();
            _overlayStateMachine.ChangeState(newState, this);
        }

        /// <summary>
        /// 弹出当前 Overlay 状态。
        /// </summary>
        public void PopOverlayState()
        {
            if (OverlayState == null) return;
            _overlayStateMachine.Clear(this);
        }

        // ===================== 战斗生命周期 =====================

        /// <summary>
        /// 启动战斗（由 CombatTestBootstrapper 在载荷就绪后调用）。
        /// </summary>
        public void StartBattle()
        {
            if (_battleStarted)
            {
                DebugTools.LogWarning("[CombatManager] 战斗已启动，忽略重复的 StartBattle。");
                return;
            }
            _battleStarted = true;
            ChangeState(new CombatInitState());
        }

        /// <summary>
        /// 战斗初始化（由 CombatInitState 调用）：
        /// 消费载荷 → 构建名册 → 生成主角与首波敌人 → 绑定战斗相机 → 广播开战事件。
        /// </summary>
        /// <returns>true = 初始化成功</returns>
        public bool SetupBattle()
        {
            // 1) 解析遭遇配置（载荷优先）
            _encounter = CombatLaunchContext.Encounter;
            CombatLaunchContext.Consume();
            if (_encounter == null || sceneRefs == null)
            {
                return false;
            }

            // 2) 构建名册与队伍等级
            Registry.Clear();
            Roster.Build(_encounter.PlayerRoster);
            _partyLevel = ResolveProtagonistLevel();

            // 3) 生成主角（战斗开始仅主角在场）
            RosterMember protagonist = Roster.Protagonist;
            if (protagonist == null || protagonist.Definition == null) return false;

            Vector3 spawnPos = sceneRefs.PlayerSpawnPoint != null
                ? sceneRefs.PlayerSpawnPoint.position
                : Vector3.zero;
            CombatUnit protagonistUnit = SpawnPlayerUnit(protagonist.Definition, spawnPos);
            if (protagonistUnit == null) return false;
            Roster.MarkDeployed(protagonist, protagonistUnit);
            CastEntrySkillIfAny(protagonist.Definition, protagonistUnit);

            // 4) 战斗相机跟随主角战斗体（场景卸载后 Cinemachine 自动切回原相机）
            if (sceneRefs.BattleCamera != null)
            {
                sceneRefs.BattleCamera.Follow = protagonistUnit.transform;
            }

            // 5) 波次计时复位（首波按其延迟生成，通常为 0 即立刻）
            _nextWaveIndex = 0;
            _nextWaveTime = _encounter.Waves != null && _encounter.Waves.Count > 0
                ? Time.time + _encounter.Waves[0].DelayAfterPrevious
                : Time.time;

            // 6) 开战
            BattleRunning = true;
            EventBus.Raise(new CombatStartedEvent { Encounter = _encounter, Roster = Roster });
            Roster.Select(0);
            DebugTools.Log("<color=orange>[Combat] 战斗开始！</color>");
            return true;
        }

        /// <summary>
        /// 结束战斗（由胜利/失败状态调用）：单位组件统一停摆，广播结束事件。
        /// </summary>
        public void EndBattle(bool victory)
        {
            if (!BattleRunning) return;
            BattleRunning = false;
            LastBattleVictory = victory;
            PopOverlayState();
            EventBus.Raise(new CombatEndedEvent { Victory = victory });
            DebugTools.Log(victory
                ? "<color=lime>[Combat] 战斗胜利！</color>"
                : "<color=red>[Combat] 战斗失败…</color>");
        }

        /// <summary>
        /// 结算画面停留时长。
        /// </summary>
        public float GetResultScreenDuration()
        {
            return config != null ? config.ResultScreenDuration : 2f;
        }

        /// <summary>
        /// 胜负兜底判定（死亡事件为主，此处防事件顺序边界漏判）：
        /// 全部波次已生成且敌方无存活 → 胜利。
        /// </summary>
        public void CheckBattleOutcome()
        {
            if (!BattleRunning) return;
            if (AllWavesSpawned && Registry.CountAlive(CombatTeam.Enemy) == 0)
            {
                ChangeState(new CombatVictoryState());
            }
        }

        // ===================== 波次刷怪 =====================

        /// <summary>
        /// 波次刷怪计时（CombatActiveState 每帧驱动）。
        /// </summary>
        public void TickWaveSpawning()
        {
            if (AllWavesSpawned || Time.time < _nextWaveTime) return;

            EncounterSO.Wave wave = _encounter.Waves[_nextWaveIndex];
            SpawnWave(wave);
            _nextWaveIndex++;

            if (!AllWavesSpawned)
            {
                _nextWaveTime = Time.time + _encounter.Waves[_nextWaveIndex].DelayAfterPrevious;
            }
        }

        /// <summary>
        /// 生成一波敌人（同点位多只时加随机散布，避免重叠）。
        /// </summary>
        private void SpawnWave(EncounterSO.Wave wave)
        {
            if (wave == null || wave.Spawns == null) return;
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                EncounterSO.SpawnEntry entry = wave.Spawns[i];
                if (entry == null || entry.Enemy == null) continue;
                Vector3 basePos = sceneRefs.GetEnemySpawnPosition(entry.SpawnPointIndex);
                for (int n = 0; n < entry.Count; n++)
                {
                    Vector2 scatter = Random.insideUnitCircle * 1.5f;
                    SpawnEnemyUnit(entry.Enemy, basePos + new Vector3(scatter.x, 0f, scatter.y));
                }
            }
        }

        // ===================== 单位生成/回收 =====================

        /// <summary>
        /// 生成我方战斗体：
        /// 主角优先使用已装备武器的战斗数据（并同步武器属性加成），否则用定义的默认武器。
        /// </summary>
        private CombatUnit SpawnPlayerUnit(CharacterDefinitionSO def, Vector3 position)
        {
            if (def == null || def.CombatUnitPrefab == null)
            {
                DebugTools.LogError("[CombatManager] 角色定义缺少战斗体预制体。");
                return null;
            }

            CombatUnit unit = SpawnUnitInternal(def.CombatUnitPrefab, position);
            if (unit == null) return null;

            // 解析武器：主角读装备，同伴用默认
            WeaponCombatDataSO weapon = def.DefaultWeaponData;
            List<StatModifierData> weaponModifiers = null;
            if (def.IsProtagonist)
            {
                WeaponSO equipped = ResolveEquippedWeapon();
                if (equipped != null && equipped.CombatData != null)
                {
                    weapon = equipped.CombatData;
                    weaponModifiers = equipped.Modifiers;
                }
            }

            unit.Initialize(CombatTeam.Player, weapon, def.StatConfig, _partyLevel, def.IsProtagonist, config);
            if (weaponModifiers != null)
            {
                unit.ApplyStatModifiers(weaponModifiers);
            }

            Registry.Register(unit);
            EventBus.Raise(new CombatUnitSpawnedEvent { Unit = unit });
            return unit;
        }

        /// <summary>
        /// 生成敌方战斗体。
        /// </summary>
        private CombatUnit SpawnEnemyUnit(EnemyDefinitionSO def, Vector3 position)
        {
            if (def == null || def.CombatUnitPrefab == null)
            {
                DebugTools.LogError("[CombatManager] 敌人定义缺少战斗体预制体。");
                return null;
            }

            CombatUnit unit = SpawnUnitInternal(def.CombatUnitPrefab, position);
            if (unit == null) return null;

            unit.Initialize(CombatTeam.Enemy, def.WeaponData, def.StatConfig, def.Level, false, config);
            Registry.Register(unit);
            EventBus.Raise(new CombatUnitSpawnedEvent { Unit = unit });
            return unit;
        }

        /// <summary>
        /// 从池中取战斗体并放置到指定位置（优先 NavMeshAgent.Warp 保证落在 NavMesh 上）。
        /// </summary>
        private CombatUnit SpawnUnitInternal(GameObject prefab, Vector3 position)
        {
            GameObjectPool pool = GetPool(prefab, _unitPoolRoot);
            GameObject instance = pool.Get();
            _poolByInstance[instance] = pool;

            CombatUnit unit = instance.GetComponent<CombatUnit>();
            if (unit == null)
            {
                DebugTools.LogError($"[CombatManager] 预制体 {prefab.name} 缺少 CombatUnit 组件。");
                pool.Release(instance);
                return null;
            }

            if (unit.Mover == null || !unit.Mover.WarpTo(position))
            {
                instance.transform.position = position;
            }

            _activeUnits.Add(unit);
            return unit;
        }

        /// <summary>
        /// 回收战斗体到池（死亡/下场/重开战斗共用）。
        /// </summary>
        private void ReleaseUnit(CombatUnit unit)
        {
            if (unit == null) return;
            _activeUnits.Remove(unit);
            if (_poolByInstance.TryGetValue(unit.gameObject, out GameObjectPool pool))
            {
                pool.Release(unit.gameObject);
            }
            else
            {
                unit.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 按预制体取（或创建）对象池。
        /// 战斗体/弹道都需要保留世界坐标语义，统一关闭回池时的 Transform 重置。
        /// </summary>
        private GameObjectPool GetPool(GameObject prefab, Transform root)
        {
            if (!_poolByPrefab.TryGetValue(prefab, out GameObjectPool pool))
            {
                pool = new GameObjectPool(prefab, root, 2, autoResetTransformOnRelease: false);
                _poolByPrefab[prefab] = pool;
            }
            return pool;
        }

        // ===================== 弹道 =====================

        /// <summary>
        /// 发射弹道（AutoAttackController 调用）。
        /// </summary>
        public void SpawnProjectile(
            GameObject prefab, Vector3 origin, CombatUnit owner, CombatUnit target,
            int damageBeforeDefense, float speed)
        {
            if (prefab == null || target == null) return;

            GameObjectPool pool = GetPool(prefab, _projectilePoolRoot);
            GameObject instance = pool.Get();
            _poolByInstance[instance] = pool;
            instance.transform.position = origin;

            Projectile projectile = instance.GetComponent<Projectile>();
            if (projectile == null)
            {
                DebugTools.LogError($"[CombatManager] 弹道预制体 {prefab.name} 缺少 Projectile 组件。");
                pool.Release(instance);
                return;
            }
            projectile.Launch(owner, target, damageBeforeDefense, speed);
        }

        /// <summary>
        /// 弹道回池（Projectile 命中/作废时回调）。
        /// </summary>
        public void ReleaseProjectile(Projectile projectile)
        {
            if (projectile == null) return;
            if (_poolByInstance.TryGetValue(projectile.gameObject, out GameObjectPool pool))
            {
                pool.Release(projectile.gameObject);
            }
            else
            {
                Destroy(projectile.gameObject);
            }
        }

        // ===================== 上下场 =====================

        /// <summary>
        /// 执行上场（放置态确认后调用）：生成战斗体、恢复下场时血量、触发入场技。
        /// </summary>
        public void DeployMember(RosterMember member, Vector3 position)
        {
            if (member == null || member.Definition == null) return;

            CombatUnit unit = SpawnPlayerUnit(member.Definition, position);
            if (unit == null) return;

            Roster.MarkDeployed(member, unit);

            // 恢复下场时记录的血量（防止回收→再上场变成免费回血）
            if (member.SavedFieldHP > 0 && unit.Stats != null)
            {
                unit.Stats.ApplySavedRuntimeState(member.SavedFieldHP, unit.Stats.CurrentLevel, 0);
            }

            CastEntrySkillIfAny(member.Definition, unit);
            EventBus.Raise(new UnitDeployedEvent { Member = member, Unit = unit, Position = position });
            DebugTools.Log($"<color=orange>[Combat] {member.Definition.name} 上场。</color>");
        }

        /// <summary>
        /// 执行下场回收（即时生效）：记录血量、撤销加成、回池并启动冷却。
        /// </summary>
        public void RecallMember(RosterMember member)
        {
            if (member == null || member.FieldUnit == null) return;

            CombatUnit unit = member.FieldUnit;
            int savedHP = unit.Stats != null ? unit.Stats.CurrentHP : -1;

            unit.PrepareForRecall();
            Registry.Unregister(unit);
            Roster.MarkRecalled(member, savedHP);
            ReleaseUnit(unit);

            EventBus.Raise(new UnitRecalledEvent { Member = member });
            DebugTools.Log($"<color=orange>[Combat] {member.Definition.name} 下场，进入重上场冷却。</color>");
        }

        /// <summary>
        /// 触发入场技（配置了才释放）。
        /// </summary>
        private void CastEntrySkillIfAny(CharacterDefinitionSO def, CombatUnit unit)
        {
            if (def.EntrySkill != null && unit.Caster != null)
            {
                unit.Caster.CastEntrySkill(def.EntrySkill);
            }
        }

        // ===================== 输入处理 =====================

        /// <summary>
        /// 技能键：对选中角色释放技能（规则拒绝时广播提示事件）。
        /// </summary>
        private void HandleSkillInput(InputSkillEvent evt)
        {
            if (!BattleRunning) return;
            // 放置态中技能键不响应，避免误操作
            if (OverlayState != null) return;

            RosterMember member = Roster.SelectedMember;
            if (member == null) return;

            if (!Roster.CanCastSkill(member, out SkillCastRejectReason reason))
            {
                EventBus.Raise(new SkillCastRejectedEvent { Member = member, Reason = reason });
                return;
            }
            member.FieldUnit.Caster.TryCast();
        }

        /// <summary>
        /// 上场/下场键（语义随上下文）：
        /// 放置态中 = 确认落点；选中后台成员 = 进入放置态；选中在场成员 = 回收下场。
        /// </summary>
        private void HandleDeployInput(InputDeployEvent evt)
        {
            if (!BattleRunning) return;

            if (OverlayState is DeployPlacementState placement)
            {
                placement.ConfirmPlacement(this);
                return;
            }

            RosterMember member = Roster.SelectedMember;
            if (member == null) return;

            if (member.State == RosterMemberState.Field)
            {
                if (!Roster.CanRecall(member, out DeployRejectReason recallReason))
                {
                    EventBus.Raise(new DeployRejectedEvent { Member = member, Reason = recallReason });
                    return;
                }
                RecallMember(member);
                return;
            }

            if (!Roster.CanDeploy(member, out DeployRejectReason deployReason))
            {
                EventBus.Raise(new DeployRejectedEvent { Member = member, Reason = deployReason });
                return;
            }
            PushOverlayState(new DeployPlacementState(member));
        }

        /// <summary>
        /// 名册切换键：移动选择指针（放置态中锁定选择）。
        /// </summary>
        private void HandleSelectInput(InputSelectEvent evt)
        {
            if (!BattleRunning || OverlayState != null) return;
            Roster.MoveSelection(evt.Direction);
        }

        /// <summary>
        /// ESC/手柄B：取消放置态。
        /// </summary>
        private void HandleUICancel()
        {
            if (OverlayState is DeployPlacementState placement)
            {
                placement.CancelPlacement(this);
            }
        }

        // ===================== 死亡处理 =====================

        /// <summary>
        /// 死亡事件中心化处理：
        /// CharacterStats 对 HP=0 的再次 TakeDamage 会重复广播 DeathEvent，
        /// 通过 CombatUnit.MarkDead 的幂等返回值保证只处理一次。
        /// </summary>
        private void HandleDeath(DeathEvent evt)
        {
            if (!_battleStarted) return;

            CombatUnit unit = Registry.FindByGameObject(evt.Owner);
            if (unit == null) return;
            if (!unit.MarkDead()) return;

            Registry.Unregister(unit);

            RosterMember member = Roster.FindByUnit(unit);
            if (member != null)
            {
                Roster.MarkDead(member);
            }

            EventBus.Raise(new CombatUnitDiedEvent { Unit = unit });
            // Phase 1 无死亡动画：尸体直接回池
            ReleaseUnit(unit);

            if (unit.IsProtagonist)
            {
                // 主角死亡 = 战斗失败
                ChangeState(new CombatDefeatState());
                return;
            }
            CheckBattleOutcome();
        }

        // ===================== 调试命令 =====================

#if UNITY_EDITOR
        [ContextMenu("Debug/强制胜利")]
        private void DebugForceVictory()
        {
            if (BattleRunning) ChangeState(new CombatVictoryState());
        }

        [ContextMenu("Debug/强制失败")]
        private void DebugForceDefeat()
        {
            if (BattleRunning) ChangeState(new CombatDefeatState());
        }

        [ContextMenu("Debug/选中角色充能拉满")]
        private void DebugFillSelectedCharge()
        {
            RosterMember member = Roster.SelectedMember;
            if (member != null && member.FieldUnit != null && member.FieldUnit.Charge != null)
            {
                member.FieldUnit.Charge.AddCharge(float.MaxValue);
            }
        }

        [ContextMenu("Debug/杀死全部敌人")]
        private void DebugKillAllEnemies()
        {
            var buffer = new List<CombatUnit>(8);
            Registry.GetAliveUnitsNonAlloc(CombatTeam.Enemy, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].Stats != null) buffer[i].Stats.TakeDamage(int.MaxValue / 2);
            }
        }

        [ContextMenu("Debug/杀死选中角色")]
        private void DebugKillSelected()
        {
            RosterMember member = Roster.SelectedMember;
            if (member != null && member.FieldUnit != null && member.FieldUnit.Stats != null)
            {
                member.FieldUnit.Stats.TakeDamage(int.MaxValue / 2);
            }
        }

        [ContextMenu("Debug/重新开始战斗（独立测试）")]
        private void DebugRestartBattle()
        {
            EncounterSO lastEncounter = _encounter;
            if (lastEncounter == null)
            {
                DebugTools.LogWarning("[CombatManager] 无遭遇配置，无法重开。");
                return;
            }
            // Sleep() 会清空 _encounter/sceneRefs（与离开 Combat 模式共用同一套清理），
            // 重开前先缓存遭遇配置，Sleep 后重新解析场景引用（仍在同一 Combat 场景内，未真正离开）
            Sleep();
            _isCombatModeActive = true;
            sceneRefs = FindAnyObjectByType<CombatSceneRefs>();
            CombatLaunchContext.SetStandaloneTest(lastEncounter);
            StartBattle();
        }
#endif

        // ===================== 内部工具 =====================

        /// <summary>
        /// 读取常驻探索玩家的等级（我方全员共用；玩家不存在时回退 1 级）。
        /// </summary>
        private int ResolveProtagonistLevel()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentPlayer == null) return 1;
            CharacterStats stats = gm.CurrentPlayer.GetComponent<CharacterStats>();
            return stats != null ? stats.CurrentLevel : 1;
        }

        /// <summary>
        /// 读取常驻探索玩家当前装备的武器（未装备返回 null）。
        /// </summary>
        private WeaponSO ResolveEquippedWeapon()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentPlayer == null) return null;
            WeaponEquipController equip = gm.CurrentPlayer.GetComponent<WeaponEquipController>();
            return equip != null ? equip.CurrentWeapon : null;
        }
    }
}
