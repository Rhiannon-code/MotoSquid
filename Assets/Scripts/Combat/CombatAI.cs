using MotoSquid.AI;
using MotoSquid.Bike;
using UnityEngine;

namespace MotoSquid.Combat
{
    public class CombatAI : MonoBehaviour
    {
        public CombatSystem combatSystem;

        [Header("AI Combat Settings")]
        public float attackCheckInterval = 0.45f;
        public float attackProbability   = 0.38f;
        // Hard floor between this AI's own swings, so hunting the player harder doesn't turn into a flurry
        public float swingCooldown       = 2.2f;

        [Header("Retaliation")]
        public float retaliationWindow = 4f;

        [Header("Race State")]
        // Fighting off the line stops the pack launching, the swing pulls the rider off the bars and
        // the lunge impulse that comes with it shoves a near stationary bike sideways
        public float minEngageSpeedKmh = 90f;

        [Header("Proximity Engagement")]
        public float proximityEngageRange = 8f;
        public float proximityEngageTime  = 5f;
        public float proximityLoseTime    = 2f;

        [Header("Player Hunting")]
        // A fight with another AI is scenery, a fight with the player is the game, so the player is
        // picked over a closer AI, chased from much further out, and committed to almost at once
        public bool  preferPlayer      = true;
        // Chasing another AI drags them both off the racing line for the length of the fight, which reads
        // from the front of the pack as the whole field falling behind.
        public bool  onlyFightPlayer   = true;
        public float combatStartDelaySeconds = 30f;
        public float playerEngageRange = 24f;
        public float playerEngageTime  = 0.4f;
        public float playerLoseTime    = 4f;
        // Everyone is inside playerEngageRange on the grid, so without this every AI turns to fight at GO
        // instead of launching. No engaging until this AI is genuinely racing
        public float combatMinSpeedKmh = 150f;

        // A swing was always aimed at whoever was nearest, so an AI hunting the player still elbowed
        // every AI it passed on the way. These keep the grid scrappy without it becoming the show:
        // the engage target is hit in preference to a closer bystander, and swings at another AI are
        // thinned out so the player is the one getting hit.
        [Range(0f, 1f)] public float aiVsAiAttackChance = 0.3f;

        private float _checkTimer;
        private float _swingTimer;
        private float _retaliationTimer;
        private float _proximityTimer;
        private float _proximityLoseTimer;
        private bool  _engageState;
        private BikeAILogic _aiLogic;
        private BikeAIController _aiController;

        // BikeAILogic reads this to steer toward the combat target when engaged
        public CombatSystem EngageTarget { get; private set; }

        // BikeAILogic reads this to hold station on the player instead of driving away from the fight
        public bool HuntingPlayer => EngageTarget != null && EngageTarget.isPlayer;

        void Start()
        {
            _checkTimer = Random.Range(0f, attackCheckInterval);
            _aiLogic      = GetComponentInParent<BikeAILogic>();
            _aiController = GetComponentInParent<BikeAIController>();

            if (combatSystem != null)
                combatSystem.onHitReceived.AddListener(OnHitReceived);
        }

        void OnDestroy()
        {
            if (combatSystem != null)
                combatSystem.onHitReceived.RemoveListener(OnHitReceived);
        }

        // Player hitting this AI opens an immediate retaliation window
        private void OnHitReceived() => _retaliationTimer = retaliationWindow;

        void FixedUpdate()
        {
            if (combatSystem == null || combatSystem.IsKnockedOff) return;

            _retaliationTimer -= Time.fixedDeltaTime;
            _swingTimer       -= Time.fixedDeltaTime;

            // Engagement is dropped rather than paused. A start grid is every racer packed well inside
            // playerEngageRange, so letting the timer run there latches the whole field into combat
            // before the lights even go out
            if (!ReadyToFight())
            {
                _proximityTimer     = 0f;
                _proximityLoseTimer = 0f;
                _engageState        = false;
                EngageTarget        = null;
                return;
            }

            UpdateProximityState();

            bool shouldAttack = _retaliationTimer > 0f || _engageState;
            if (!shouldAttack || _swingTimer > 0f) return;

            _checkTimer -= Time.fixedDeltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = attackCheckInterval;

            if (TryAttackNearby()) _swingTimer = swingCooldown;
        }

        // SpeedMs already reports 0 while the countdown holds the bike, so this one check covers the
        // grid, the launch phase and any mid race crawl without a second race state hook
        private bool ReadyToFight()
        {
            if (_aiLogic != null && _aiLogic.IsLaunching) return false;

            // Fighting before the field has settled into a pace is what turns the first corner into a
            // brawl, and an AI that joins it never gets the time back. They race first.
            if (combatStartDelaySeconds > 0f && _aiLogic != null && _aiLogic.raceManager != null &&
                _aiLogic.raceManager.RaceElapsedTime < combatStartDelaySeconds)
                return false;

            Rigidbody rb = combatSystem.bikeRigidbody;
            float speedMs = _aiController != null ? _aiController.SpeedMs
                          : rb != null ? rb.linearVelocity.magnitude
                          : float.MaxValue;
            return speedMs * 3.6f >= minEngageSpeedKmh;
        }

        private bool UpToSpeed()
        {
            Rigidbody rb = combatSystem.bikeRigidbody;
            return rb == null || rb.linearVelocity.magnitude * 3.6f >= combatMinSpeedKmh;
        }

        private void UpdateProximityState()
        {
            CombatSystem player  = preferPlayer ? FindPlayer(playerEngageRange) : null;
            CombatSystem closest = player != null ? player
                                          : onlyFightPlayer ? null
                                          : FindClosestCombatant(proximityEngageRange);

            if (closest != null)
            {
                _proximityLoseTimer = player != null ? playerLoseTime : proximityLoseTime;
                _proximityTimer    += Time.fixedDeltaTime;

                if (_proximityTimer >= (player != null ? playerEngageTime : proximityEngageTime))
                    _engageState = true;

                if (_engageState)
                    EngageTarget = closest;
            }
            else
            {
                _proximityLoseTimer -= Time.fixedDeltaTime;
                if (_proximityLoseTimer <= 0f)
                {
                    _proximityTimer = 0f;
                    _engageState    = false;
                    EngageTarget    = null;
                }
            }
        }

        private bool TryAttackNearby()
        {
            if (Random.value > attackProbability) return false;

            float reach = combatSystem.PunchRange + combatSystem.lungeReachBonus;

            CombatSystem target = WithinReach(EngageTarget, reach)
                ? EngageTarget
                : combatSystem.FindTarget(0, reach);
            if (target == null) return false;

            if (!target.isPlayer && (onlyFightPlayer || Random.value > aiVsAiAttackChance)) return false;

            // Traffic still comes first, but "is anything ahead of me" was the wrong question - it blocked
            // swings while the AI was comfortably tracking a wide gap. SafeToEngage asks whether there is
            // actually room to fight where the target is, so they engage more often in clear air and not at
            // all when boxed in or braking. Checked here rather than above because it needs the target
            if (_aiLogic != null && !_aiLogic.SafeToEngage(target.transform.position)) return false;

            Vector3 toTarget = target.transform.position - transform.position;
            float side = Vector3.Dot(combatSystem.Facing.right, toTarget);
            int dir = side >= 0f ? 1 : -1;

            return combatSystem.TryPunch(dir);
        }

        private bool WithinReach(CombatSystem c, float reach)
        {
            if (c == null || c.IsKnockedOff) return false;
            return (c.transform.position - transform.position).sqrMagnitude <= reach * reach;
        }

        // Nearest human racer within range, the one this AI would rather be fighting
        private CombatSystem FindPlayer(float range)
        {
            CombatSystem nearest   = null;
            float                 nearestSq = range * range;
            var allCombatants = CombatSystem.AllInstances;

            for (int i = 0; i < allCombatants.Count; i++)
            {
                CombatSystem c = allCombatants[i];
                if (c == null || c == combatSystem || !c.isPlayer || c.IsKnockedOff) continue;

                float sqDist = (transform.position - c.transform.position).sqrMagnitude;
                if (sqDist < nearestSq)
                {
                    nearestSq = sqDist;
                    nearest   = c;
                }
            }

            return nearest;
        }

        // Finds the nearest combatant (player or AI) within range, excluding self
        private CombatSystem FindClosestCombatant(float range)
        {
            CombatSystem nearest    = null;
            float                 nearestSq  = range * range;
            var allCombatants = CombatSystem.AllInstances;

            for (int i = 0; i < allCombatants.Count; i++)
            {
                CombatSystem c = allCombatants[i];
                if (c == null || c == combatSystem || c.IsKnockedOff) continue;

                float sqDist = (transform.position - c.transform.position).sqrMagnitude;
                if (sqDist < nearestSq)
                {
                    nearestSq = sqDist;
                    nearest   = c;
                }
            }

            return nearest;
        }
    }
}
