using System;
using UnityEngine;
using MoonSharp.Interpreter;
using Mirror;

    
public class CombatState2 {
        public float added_damageout;
        public float added_selfheal;

        public float buff_mana;
        public float buff_mana_seconds;

        public float buff_health;
        public float buff_health_seconds;

        public float area_damage;
        public float area_effectyards;

        public float buff_defense;
        public float buff_defense_seconds;

        public float buff_block;
        public float buff_block_seconds;

        public float buff_crit_chance;
        public float buff_crit_chance_seconds;

        public float buff_speed;
        public float buff_speed_seconds;

        public int secondary_skill;
        public float secondary_skillchance;
}

public struct DamageEffects {
    public float added_damageout;
    public float added_selfheal;

    public float buff_mana;
    public float buff_mana_seconds;
 
    public float buff_crit_chance;
    public float buff_crit_chance_seconds;

 
    public int[] second_skills;
    public float[] second_skill_chances;

    public DamageEffects (int items) {
        added_damageout = 0;
        added_selfheal = 0;

        buff_mana = 0;
        buff_mana_seconds = 0;

        // FIXME: Add the rest of the options here...

        buff_crit_chance = 0;
        buff_crit_chance_seconds = 0;

        second_skills = new int[items];
        second_skill_chances = new float[items];
    }
}

[DisallowMultipleComponent]
public abstract class Equipment : ItemContainer, IHealthBonus, IManaBonus, ICombatBonus
{

    public int AccumulateDamageEffects(int slotid, DamageEffects effects, Item item, CombatState1 state1, CombatState2 state2, Entity caster, Entity victim) {
        Debug.Log("In AccumulateDamageEffects()");

        effects.added_damageout += state2.added_damageout;
        effects.added_selfheal += state2.added_selfheal;

        // FIXME: Need to handle all the different output states in state2 and do them

        effects.buff_crit_chance += state2.buff_crit_chance;
        effects.buff_crit_chance_seconds += state2.buff_crit_chance_seconds;

        effects.second_skills[slotid] = state2.secondary_skill;
        effects.second_skill_chances[slotid] = state2.secondary_skillchance;

        return 0;
    }
    
    
    public void RunDamageEffects(int depth, CombatState1 state1, DamageEffects damageEffects, Entity caster, Entity victim) {
        Combat victimCombat = victim.combat;
        DamageType damageType = DamageType.Normal;
        float damageDealt = 0; 

        // float depthDamageMult[] = {0.90F, 0.40F, 0.10F};    // Effects always get less powerful in general.

        // Get info about skill

        float actualAmount = damageEffects.added_damageout;
        float criticalChance = damageEffects.buff_crit_chance;

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
                victim.health.current -= (int)damageDealt;

                // call OnServerReceivedDamage event on the target
                // -> can be used for monsters to pull aggro
                // -> can be used by equipment to decrease durability etc.
                victimCombat.onServerReceivedDamage.Invoke(caster.combat.entity, (int)damageDealt);

                // stun?
                /*
                if (UnityEngine.Random.value < stunChance)
                {
                    // dont allow a short stun to overwrite a long stun
                    // => if a player is hit with a 10s stun, immediately
                    //    followed by a 1s stun, we don't want it to end in 1s!
                    double newStunEndTime = NetworkTime.time + stunTime;
                    victim.stunTimeEnd = Math.Max(newStunEndTime, entity.stunTimeEnd);
                }
                */
            }

            // call OnDamageDealtTo / OnKilledEnemy events
            caster.combat.onDamageDealtTo.Invoke(victim);
            if (victim.health.current == 0)
                caster.combat.onKilledEnemy.Invoke(victim);
        }

        // let's make sure to pull aggro in any case so that archers
        // are still attacked if they are outside of the aggro range
        victim.OnAggro(caster.combat.entity);

        // show effects on clients
        victimCombat.RpcOnReceivedDamaged((int)damageDealt, damageType);

        // reset last combat time for both
        caster.combat.entity.lastCombatTime = NetworkTime.time;
        victim.lastCombatTime = NetworkTime.time;

        // FIXME: Need to add in other effects... --JRR

        foreach(int skillid in damageEffects.second_skills) {
            caster.combat.DealDamageAt(depth, skillid, caster, victim, 0, 0, 0);
        }
    }

    // Run down the user's equipment and call the damage scripts for each item.
    public void RunOnDoDamageScripts(int depth, CombatState1 state1, Entity caster, Entity victim) {

        DamageEffects damageEffects = new DamageEffects(slots.Count);

        foreach (ItemSlot slot in slots) {
            if (slot.item.data is EquipmentItem) {
                Item item = slot.item;

                string scriptText = item.luaScriptOnDoDamage;

                Script script = new Script();
                script.DoString(scriptText);

                script.Globals["input"] = state1;
                script.Globals["caster"] = caster;
                script.Globals["victim"] = victim;
                script.Globals["item"] = item;

                script.Globals["out"] = new CombatState2{};
                script.Globals["effects"] = damageEffects;      // This accumulated the effects from all items
                script.Globals["slotid"] = damageEffects;      // This accumulated the effects from all items

                script.Globals["doit"] = (Func<int, DamageEffects, Item, CombatState1, CombatState2, Entity, Entity, int>)AccumulateDamageEffects;

            }
        }

        RunDamageEffects(depth, state1, damageEffects, caster, victim);
    }

    // boni ////////////////////////////////////////////////////////////////////
    public int GetHealthBonus(int baseHealth)
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).healthBonus;
        return bonus;
    }

    public int GetHealthRecoveryBonus() => 0;

    public int GetManaBonus(int baseMana)
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).manaBonus;
        return bonus;
    }

    public int GetManaRecoveryBonus() => 0;

    public int GetDamageBonus()
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).damageBonus;
        return bonus;
    }

    public int GetDefenseBonus()
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).defenseBonus;
        return bonus;
    }

    public float GetCriticalChanceBonus()
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).criticalChanceBonus;
        return bonus;
    }

    public float GetBlockChanceBonus()
    {
        // calculate equipment bonus
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float bonus = 0;
        foreach (ItemSlot slot in slots)
            if (slot.amount > 0 && slot.item.CheckDurability())
                bonus += ((EquipmentItem)slot.item.data).blockChanceBonus;
        return bonus;
    }

    ////////////////////////////////////////////////////////////////////////////
    // helper function to find the equipped weapon index
    // -> works for all entity types. returns -1 if no weapon equipped.
    public int GetEquippedWeaponIndex()
    {
        // (avoid FindIndex to minimize allocations)
        for (int i = 0; i < slots.Count; ++i)
        {
            ItemSlot slot = slots[i];
            if (slot.amount > 0 && slot.item.data is WeaponItem)
                return i;
        }
        return -1;
    }

    // get currently equipped weapon category to check if skills can be casted
    // with this weapon. returns "" if none.
    public string GetEquippedWeaponCategory()
    {
        // find the weapon slot
        int index = GetEquippedWeaponIndex();
        return index != -1 ? ((WeaponItem)slots[index].item.data).category : "";
    }
}
