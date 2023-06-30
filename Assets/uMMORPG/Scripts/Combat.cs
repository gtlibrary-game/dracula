using System;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.Events;

public enum DamageType { Normal, Block, Crit }

    public struct CombatState1 {
        public int caster_skillid;
        public int caster_damageout;

        public int caster_level;
        public int caster_health;
        public int caster_maxhealth;
        public int caster_mana;
        public int caster_maxmana;
        public float caster_runspeed;
        public double caster_lastcombat;

        public int caster_defense;
        public float caster_block;
        public float caster_crit;


        public int victim_level;
        public int victim_health;
        public int victim_maxhealth;
        public int victim_mana;
        public int victim_maxmana;
        public float victim_runspeed;
        public double victim_lastcombat;
        
        public int victim_defense;
        public float victim_block;
        public float victim_crit;
    }


// inventory, attributes etc. can influence max health
public interface ICombatBonus
{
    int GetDamageBonus();
    int GetDefenseBonus();
    float GetCriticalChanceBonus();
    float GetBlockChanceBonus();
}

[Serializable] public class UnityEventIntDamageType : UnityEvent<int, DamageType> {}

[DisallowMultipleComponent]
public class Combat : NetworkBehaviour
{
    [Header("Components")]
    public Level level;
    public Entity entity;
#pragma warning disable CS0109 // member does not hide accessible member
    public new Collider collider;
#pragma warning restore CS0109 // member does not hide accessible member

    [Header("Stats")]
    [SyncVar] public bool invincible = false; // GMs, Npcs, ...
    public LinearInt baseDamage = new LinearInt{baseValue=1};
    public LinearInt baseDefense = new LinearInt{baseValue=1};
    public LinearFloat baseBlockChance;
    public LinearFloat baseCriticalChance;

    [Header("Damage Popup")]
    public GameObject damagePopupPrefab;

    // events
    [Header("Events")]
    public UnityEventEntity onDamageDealtTo;
    public UnityEventEntity onKilledEnemy;
    public UnityEventEntityInt onServerReceivedDamage;
    public UnityEventIntDamageType onClientReceivedDamage;

    // cache components that give a bonus (attributes, inventory, etc.)
    ICombatBonus[] _bonusComponents;
    ICombatBonus[] bonusComponents =>
        _bonusComponents ?? (_bonusComponents = GetComponents<ICombatBonus>());

    // calculate damage
    public int damage
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDamageBonus();
            return baseDamage.Get(level.current) + bonus;
        }
    }

    // calculate defense
    public int defense
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDefenseBonus();
            return baseDefense.Get(level.current) + bonus;
        }
    }

    // calculate block
    public float blockChance
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            float bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetBlockChanceBonus();
            return baseBlockChance.Get(level.current) + bonus;
        }
    }

    // calculate critical
    public float criticalChance
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            float bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetCriticalChanceBonus();
            return baseCriticalChance.Get(level.current) + bonus;
        }
    }

    public int SkillDatabase_getDamage(int skillid, Entity caster, int clientAmount) {
        int digitalAmount = clientAmount;

        if(0 == skillid) {
            // Effectively automatic shot
            // Fixme:
            // Damage needs to be a function of 
            digitalAmount += 4;                     // Create imbalance towards melee
        }

        if(1 == skillid) {
            // White damage/ auto attack 
            digitalAmount += 5;                     // With higher damage than ranged attack
        }

        return digitalAmount;
    }


    public async void DoUniqueDamage(int depth, CombatState1 state1, Entity caster, Entity victim) {
        // Pass state1 into the script for each unique item.
        caster.equipment.RunOnDamageScripts(depth, state1, caster, victim);
    }

    // combat //////////////////////////////////////////////////////////////////
    // deal damage at another entity
    // (can be overwritten for players etc. that need custom functionality)
    [Server]
    public virtual void DealDamageAt(int depth, int skillid, Entity caster, Entity victim, int amount, float stunChance=0, float stunTime=0)
    {
        if(depth >=2) {
            return;
        }
        Combat victimCombat = victim.combat;
        int damageDealt = 0; // FIXME: This is function of caster and skill id and not a function of amount.
        DamageType damageType = DamageType.Normal; // This is also a function of skill and caster.

        int actualAmount = SkillDatabase_getDamage(skillid, caster, amount);

        CombatState1 state1 = new CombatState1 {
            caster_skillid = skillid,
            caster_damageout = actualAmount, 
            caster_level = caster.level.current,
            caster_health = caster.health.baseHealth.Get(caster.level.current),
            caster_mana = caster.mana.baseMana.Get(caster.level.current),
            caster_runspeed = caster.speed,
            caster_lastcombat = NetworkTime.time - caster.lastCombatTime,

            caster_defense =  defense,
            caster_block =  blockChance,
            caster_crit =  criticalChance,

            victim_level = victim.level.current,
            victim_health = victim.health.baseHealth.Get(victim.level.current),
            victim_mana = victim.mana.baseMana.Get(victim.level.current),
            victim_runspeed = victim.speed,
            victim_lastcombat = NetworkTime.time - victim.lastCombatTime,

            victim_defense = victimCombat.defense,
            victim_block = victimCombat.blockChance,
            victim_crit = victimCombat.criticalChance,
        };

        Debug.Log("state1: " + state1);

        DoUniqueDamage(depth, state1, caster, victim);

        // don't deal any damage if entity is invincible
        if (!victimCombat.invincible)
        {
            // block? (we use < not <= so that block rate 0 never blocks)
            if (UnityEngine.Random.value < victimCombat.blockChance)
            {
                damageType = DamageType.Block;
            }
            // deal damage
            else
            {
                // subtract defense (but leave at least 1 damage, otherwise
                // it may be frustrating for weaker players)
                damageDealt = Mathf.Max(actualAmount - victimCombat.defense, 1);

                // critical hit?`
                if (UnityEngine.Random.value < criticalChance)
                {
                    damageDealt *= 2;
                    damageType = DamageType.Crit;
                }

                // deal the damage
                victim.health.current -= damageDealt;

                // call OnServerReceivedDamage event on the target
                // -> can be used for monsters to pull aggro
                // -> can be used by equipment to decrease durability etc.
                victimCombat.onServerReceivedDamage.Invoke(entity, damageDealt);

                // stun?
                if (UnityEngine.Random.value < stunChance)
                {
                    // dont allow a short stun to overwrite a long stun
                    // => if a player is hit with a 10s stun, immediately
                    //    followed by a 1s stun, we don't want it to end in 1s!
                    double newStunEndTime = NetworkTime.time + stunTime;
                    victim.stunTimeEnd = Math.Max(newStunEndTime, entity.stunTimeEnd);
                }
            }

            // call OnDamageDealtTo / OnKilledEnemy events
            onDamageDealtTo.Invoke(victim);
            if (victim.health.current == 0)
                onKilledEnemy.Invoke(victim);
        }

        // let's make sure to pull aggro in any case so that archers
        // are still attacked if they are outside of the aggro range
        victim.OnAggro(entity);

        // show effects on clients
        victimCombat.RpcOnReceivedDamaged(damageDealt, damageType);

        // reset last combat time for both
        entity.lastCombatTime = NetworkTime.time;
        victim.lastCombatTime = NetworkTime.time;
    }

    // no need to instantiate damage popups on the server
    // -> calculating the position on the client saves server computations and
    //    takes less bandwidth (4 instead of 12 byte)
    [Client]
    void ShowDamagePopup(int amount, DamageType damageType)
    {
        // spawn the damage popup (if any) and set the text
        if (damagePopupPrefab != null)
        {
            // showing it above their head looks best, and we don't have to use
            // a custom shader to draw world space UI in front of the entity
            Bounds bounds = collider.bounds;
            Vector3 position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            GameObject popup = Instantiate(damagePopupPrefab, position, Quaternion.identity);
            if (damageType == DamageType.Normal)
                popup.GetComponentInChildren<TextMeshPro>().text = amount.ToString();
            else if (damageType == DamageType.Block)
                popup.GetComponentInChildren<TextMeshPro>().text = "<i>Block!</i>";
            else if (damageType == DamageType.Crit)
                popup.GetComponentInChildren<TextMeshPro>().text = amount + " Crit!";
        }
    }

    [ClientRpc]
    public void RpcOnReceivedDamaged(int amount, DamageType damageType)
    {
        // show popup above receiver's head in all observers via ClientRpc
        ShowDamagePopup(amount, damageType);

        // call OnClientReceivedDamage event
        onClientReceivedDamage.Invoke(amount, damageType);
    }
}
