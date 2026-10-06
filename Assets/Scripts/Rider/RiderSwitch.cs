using MotoSquid.Audio;
using MotoSquid.Characters;
using MotoSquid.Combat;
using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MotoSquid.Rider
{
    [DefaultExecutionOrder(-50)]
    public class RiderSwitch : MonoBehaviour
    {
        public GameObject[] riders = new GameObject[0];
        public bool hideOtherTargetSets = true;

        // The roster is the one source of truth for who a rider is: the rider GameObject takes its
        // name from the model file, which no tool guarantees matches a character name
        public CharacterLibrary characterLibrary;

        public const string SetPrefix = "Biker Animation Targets - ";

        int index;
        Transform[] sets;
        Transform donor;

        void Start()
        {
            EnsureResolved();
            for (int i = 0; i < riders.Length; i++)
                if (riders[i] != null && riders[i].activeSelf) { index = i; break; }
            Apply();
        }

        public bool SetRider(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return false;
            EnsureResolved();

            int found = IndexOf(characterName, true);
            if (found < 0) found = IndexOf(characterName, false);
            if (found < 0) found = IndexOfByDefinition(characterName);
            if (found < 0) return false;

            index = found;
            Apply();
            return true;
        }

        int IndexOf(string characterName, bool exact)
        {
            for (int i = 0; i < riders.Length; i++)
            {
                if (riders[i] == null) continue;
                bool hit = exact
                    ? riders[i].name.Equals(characterName, StringComparison.OrdinalIgnoreCase)
                    : riders[i].name.StartsWith(characterName, StringComparison.OrdinalIgnoreCase);
                if (hit) return i;
            }
            return -1;
        }

        // "Mace" never matched "Fixing_Mace_R" by name, so resolve through the roster instead
        int IndexOfByDefinition(string characterName)
        {
            for (int i = 0; i < riders.Length; i++)
            {
                var def = DefinitionFor(riders[i]);
                if (def != null && string.Equals(def.characterName, characterName,
                                                 StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public CharacterDefinition DefinitionFor(GameObject rider)
        {
            if (characterLibrary == null || rider == null) return null;

            CharacterDefinition match = null;
            for (int i = 0; i < characterLibrary.Count; i++)
            {
                var def = characterLibrary.Get(i);
                if (def == null || string.IsNullOrEmpty(def.characterName)) continue;
                if (rider.name.IndexOf(def.characterName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (match != null) return null;   // Ambiguous, better none than the wrong one
                match = def;
            }
            return match;
        }

        public GameObject ActiveRider
        {
            get
            {
                foreach (var r in riders) if (r != null && r.activeSelf) return r;
                return index < riders.Length ? riders[index] : null;
            }
        }

        public IEnumerable<string> RiderNames
        {
            get { foreach (var r in riders) if (r != null) yield return r.name; }
        }

        void EnsureResolved()
        {
            if (sets != null && sets.Length == riders.Length) return;

            var live = new List<GameObject>();
            foreach (var r in riders) if (r != null) live.Add(r);
            if (live.Count != riders.Length) riders = live.ToArray();

            Resolve();
        }

        void Resolve()
        {
            sets = new Transform[riders.Length];
            for (int i = 0; i < riders.Length; i++)
            {
                if (riders[i] == null) continue;
                var parent = riders[i].transform.parent;
                if (parent == null) continue;
                sets[i] = parent.Find(SetPrefix + riders[i].name);
                if (donor == null) donor = parent.Find("Biker Animation Targets");
            }
        }

        void Update()
        {
            if (riders.Length < 2) return;
            if (Pressed(true)) Step(1);
            else if (Pressed(false)) Step(-1);
        }

        void Step(int dir)
        {
            index = (index + dir + riders.Length) % riders.Length;
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < riders.Length; i++)
            {
                if (riders[i] != null) riders[i].SetActive(i == index);
                if (sets != null && i < sets.Length && sets[i] != null)
                    sets[i].gameObject.SetActive(!hideOtherTargetSets || i == index);
            }

            if (donor != null && hideOtherTargetSets) donor.gameObject.SetActive(false);

            Rebind();
        }

        public void Rebind()
        {
            var rider = index < riders.Length ? riders[index] : null;
            if (rider == null) return;

            var anim = rider.GetComponent<Animator>() ?? rider.GetComponentInChildren<Animator>(true);
            if (anim == null) return;

            var driver = rider.GetComponentInChildren<CombatRigDriver>(true);

            foreach (var combat in GetComponentsInChildren<CombatSystem>(true))
                combat.BindRider(anim, driver);

            // One holder serves every rider, and BindRider carries the previous weapon across, so
            // without this all three swing whoever's weapon the holder happened to start with
            var def = DefinitionFor(rider);
            foreach (var holder in GetComponentsInChildren<MeleeWeaponHolder>(true))
            {
                holder.BindRider(anim);
                // Only when it actually changes, Equip destroys and respawns the prop, and a
                // pointless respawn on every rebind is what left a dead renderer behind
                if (def != null && def.defaultWeapon != null && holder.Weapon != def.defaultWeapon)
                    holder.Equip(def.defaultWeapon);
            }

            if (def != null && def.voice != null)
                foreach (var barks in GetComponentsInChildren<VoiceBarkController>(true))
                    barks.SetVoice(def.voice);

            // The ragdoll is a per-character prefab, so without this every rider crashes as
            // whichever character the bike was authored with
            foreach (var rag in GetComponentsInChildren<RagdollActivator>(true))
            {
                rag.characterAnimator = anim;
                if (def != null && def.ragdollPrefab != null)
                    rag.characterRagdollPrefab = def.ragdollPrefab;
            }
        }

        // The rider object is named after its model file, which is not what a player should read
        public string CharacterName
        {
            get
            {
                EnsureResolved();
                var rider = ActiveRider;
                if (rider == null) return "";
                var def = DefinitionFor(rider);
                return def != null && !string.IsNullOrEmpty(def.characterName) ? def.characterName : rider.name;
            }
        }

        public string Current
        {
            get { return index < riders.Length && riders[index] != null ? riders[index].name : "-"; }
        }

        static bool Pressed(bool next)
        {
            // Editor only: in a build a keyboard P2 would swap riders mid race
            if (!UnityEngine.Application.isEditor) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
                return next ? kb.periodKey.wasPressedThisFrame : kb.commaKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(next ? KeyCode.Period : KeyCode.Comma);
#else
            return false;
#endif
        }
        
        public bool showReadout;

        void OnGUI()
        {
            if (showReadout) Draw();
        }

        void Draw()
        {
            if (riders.Length < 2) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            GUI.Label(new Rect(14, 34, 600, 20),
                      ", / .  character  " + (index + 1) + "/" + riders.Length + "   " + Current, style);
        }
    }
}
