using UnityEngine;
using MoonSharp.Interpreter;

    
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
        public float buff_crit_chanse_seconds;

        public float buff_speed;
        public float buff_speed_seconds;

        public int secondary_skill;
        public float secondary_skillchance;
}

[DisallowMultipleComponent]
public abstract class Equipment : ItemContainer, IHealthBonus, IManaBonus, ICombatBonus
{


    // Run down the user's equipment and call the damage scripts for each item.
    public void RunOnDamageScripts(int depth, CombatState1 state1, Entity caster, Entity victim) {
        foreach (ItemSlot slot in slots) {
            if (slot.item.data is EquipmentItem) {
                Item item = slot.item;

                //string scriptText = item.luaScriptOnDamageDealtTo;
                string scriptText = @"
                function OnDoDamage(quality)  -- Quality comes from the item itself: 0 or junk, 1, 2, or 3, 4 = corrupt

                    caster_skillid = input.caster_skillid
                    caster_damageout = input.caster_damage

                    caster_level = inout.caster_level
                    caster_health = input.caster_health
                    caster_maxhealth = input.caster_maxhealth
                    caster_mana = input.caster_mana
                    caster_maxmana = input.caster_maxmana
                    caster_runspeed = input.caster_runspeed
                    caster_lastcombat = input.caster_lastcombat

                    caster_defense = input.caster_defense
                    caster_block = input.caster_block
                    caster_crit = input.caster_crit

                    victim_level = input.victim_level
                    victim_health = input.victim.health
                    victim_maxhealth = input.victim.health
                    victim_mana = input.victim_mana
                    victim_maxmana = input.victim_maxmana
                    victim_runspeed = input.victim_runspeed
                    victim_lastcombat = input.victim_lastcombat
        
                    victim_defense = input.victim_defense
                    victim_block = input.victim_block
                    victim_crit = input.victim_crit

                    -- Convert the inputs into outputs using AI

                    out.added_damageout = 0
                    out.added_selfheal = 0

                    out.buff_maxmana = 0
                    out.buff_maxmana_seconds = 0

                    out.buff_maxhealth = 0
                    out.buff_maxhealth_seconds = 0

                    out.area_damageout = 0
                    out.area_effectyards = 0

                    out.buff_defense = 0
                    out.buff_defense_seconds = 0

                    out.buff_block = 0;
                    out.buff_block_seconds = 0;

                    out.buff_crit = 0
                    out.buff_crit_seconds = 0

                    out.buff_runspeed = 0
                    out.buff_runspeed_seconds = 0

                    out.secondary_skill = 0;
                    out.secondary_skillchance = 0;

                end
                ";

                Script script = new Script();
                script.DoString(scriptText);

                script.Globals["state1"] = state1;
                script.Globals["caster"] = caster;
                script.Globals["victim"] = victim;

                script.Globals["out"] = new CombatState2{};

                script.Call(script.Globals["OnDoDamage"], 4);   // Always corrupt items for now...

                Debug.Log(script.Globals["out"]);

                // Now to implement each of these effects.
            }
        }
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
