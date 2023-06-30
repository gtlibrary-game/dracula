// The Item struct only contains the dynamic item properties, so that the static
// properties can be read from the scriptable object.
//
// Items have to be structs in order to work with SyncLists.
//
// Use .Equals to compare two items. Comparing the name is NOT enough for cases
// where dynamic stats differ. E.g. two pets with different levels shouldn't be
// merged.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror;

using MoonSharp.Interpreter;
using OpenAI;
using System.Net.Http;
using System.Linq;
using System.Text.RegularExpressions;

[Serializable]
public partial struct Item
{
    // hashcode used to reference the real ScriptableItem (can't link to data
    // directly because synclist only supports simple types). and syncing a
    // string's hashcode instead of the string takes WAY less bandwidth.
    public int hash;

    // current durability
    public int durability;

    // dynamic stats (cooldowns etc. later)
    public NetworkIdentity summoned; // summonable that's currently summoned
    public int summonedHealth; // stored in item while summonable unsummoned
    public int summonedLevel; // stored in item while summonable unsummoned
    public long summonedExperience; // stored in item while summonable unsummoned

    // The server side code for the items (may also run on client)
    public string luaScriptFlavor;
    public string luaScriptOnUse;
    public string luaScriptOnDoDamage;

    public bool superDiced;
    public int quality;         // 0 - 4 with 4 being "corrupted."

    public string onDoDamageBegin;
    public string onDoDamageFinish;
    public string onDoDamageSystemMessage; 
    // FIXME: This code is horribly coded and slow. --jrr
    public string[] prefixes;
    private List<string> prefixList;

    public string prefix1;
    public string prefix2;
    public string prefix3;
    public string prefix4;

    public string affix1;

    public string[] skills;
    public List<string> skillsList;
    
    // constructors

    public string getPrefix() {
        // Create a new instance of the Random class
        System.Random random = new System.Random();

        // Generate a random index within the range of the array
        int randomIndex = random.Next(0, prefixes.Length);

        // Access the element at the random index
        string randomPrefix = prefixes[randomIndex];
        Console.WriteLine(randomPrefix);

        return randomPrefix;
    }

    // All this bagging code is very slow. Luckily we are chaching the result when rewriting the script.
    public int FuzzySpellLookUp(string inSpell) {
        return GetClosestMatchIndex(inSpell);
    }

    public int GetClosestMatchIndex(string searchTerm) {
        int closestMatchIndex = -1;
        int highestScore = 0;

        foreach (string skill in skillsList)
        {
            int score = CalculateFuzzyMatchScore(skill, searchTerm);

            if (score > highestScore)
            {
                highestScore = score;
                closestMatchIndex = skillsList.IndexOf(skill);
            }
        }

        if(-1 == closestMatchIndex) {
            return 0;
        }
        return closestMatchIndex;
    }

    public int CalculateFuzzyMatchScore(string skill, string searchTerm)
    {
        int score = 0;

        foreach (char letter in skill)
        {
            if (searchTerm.Contains(letter))
            {
                score++;
            }
        }

        return score;
    }

    public string RewriteScript(string inScript) {
        string outScript = inScript;
        
        string pattern = @"(toPrimaryId|toSecondaryId)\('([^']*)'\)";
        MatchCollection matches = Regex.Matches(outScript, pattern);
        foreach (Match match in matches)
        {
            string originalCall = match.Groups[0].Value;
            string spellName = match.Groups[2].Value;

            string replacement = FuzzySpellLookUp(spellName).ToString(); // FIXME: Replace with fuzzy lookup against skills

            outScript = outScript.Replace(originalCall, replacement);
        }

        Console.WriteLine(outScript);

        return outScript;
    }

    private async void getAIForOnDamage(EquipmentItem data) {

        string onDoDamageUserInput = "Please give me a new item based on the following power words: " + prefix1 + " " + prefix2 + " " + prefix3 + " " + prefix4 + "  and especially " + affix1;

        string url = "https://author.greatlibrary.io/art/chat/";
        using (HttpClient client = new HttpClient())
        {
            var content = new MultipartFormDataContent
            {
                { new StringContent("True"), "return_json" },
                { new StringContent(onDoDamageSystemMessage), "context" },
                { new StringContent(onDoDamageUserInput), "user_input" },
                //{ new StringContent(message1), "message1" }
            };

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            string result = await response.Content.ReadAsStringAsync();
            Debug.Log("result from DKC " + result);
            result = JsonUtility.FromJson<FromChatWrapper>(result).content.content;
            Debug.Log(result);

            luaScriptOnDoDamage = RewriteScript(onDoDamageBegin + result + onDoDamageFinish);

        }
    }

    private async void getAIForItemFlavor(EquipmentItem data) {

        OpenAIApi openai = new OpenAIApi();
        List<OpenAI.ChatMessage> messages = new List<OpenAI.ChatMessage>();

        var systemMessage = new OpenAI.ChatMessage() {
            Role = "system",
            Content = @"I write lua code for the game Dracula
```lua
function code() {
  -- Like this...
end
```
I try to keep comments and code vague enough for them to be true."

        };
        messages.Add(systemMessage);

        var newMessage = new OpenAI.ChatMessage() {
            Role = "user",
            Content = "Write me just the lua code for a function called flavor() that returns very flowery item flavor text based on \n" 
                            + data.name + ", damage bonus: " + data.damageBonus + ". health bonus: " + data.healthBonus + ", mana bonus: " + data.manaBonus + "\n" +
                            "Remain vague about what the item actually does."
        };

        messages.Add(newMessage);

        // Complete the instruction  // See Johnrraymond for { api_key: "sk-...." }  ->  %USERPROFILE%\.openai\auth.json 
        // See https://github.com/srcnalt/OpenAI-Unity
        var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest() {
                Model = "gpt-3.5-turbo-0301",
                Messages = messages
        });

        if (completionResponse.Choices != null && completionResponse.Choices.Count > 0) {
            var message = completionResponse.Choices[0].Message;
            message.Content = message.Content.Trim();

            Debug.Log("AI message is: " + message.Content);

            string input = message.Content;
            string output;

        int start = input.IndexOf("```lua");
        if (start != -1) {
            start += 6;
            int end = input.IndexOf("```", start);

            output = input.Substring(start, end - start);

            Console.WriteLine(output);
        } else {
            Console.WriteLine("Start quote not found.");
            // no change to output.
            output = message.Content;
        }

        try {
            DynValue res = Script.RunString(output + "return flavor()");
	        Debug.LogWarning("result: " + res.String);

            luaScriptFlavor = res.String;
        } catch(Exception e) {
            
            luaScriptFlavor = message.Content +  " and " + e;
        }
        }

    }

    [Server]
    public async void SuperDice(EquipmentItem data) {
        getAIForItemFlavor(data);
        getAIForOnDamage(data);

        //Need to tell the player the special effect so it can show up
        //in their UI. FIXME. --jrr
    }

    // wrappers for easier access
    public ScriptableItem data
    {
        get
        {
            // show a useful error message if the key can't be found
            // note: ScriptableItem.OnValidate 'is in resource folder' check
            //       causes Unity SendMessage warnings and false positives.
            //       this solution is a lot better.
            if (!ScriptableItem.All.ContainsKey(hash))
                throw new KeyNotFoundException("There is no ScriptableItem with hash=" + hash + ". Make sure that all ScriptableItems are in the Resources folder so they are loaded properly.");
            return ScriptableItem.All[hash];
        }
    }
    public string name => data.name;
    public int maxStack => data.maxStack;
    public int maxDurability => data.maxDurability;
    public float DurabilityPercent()
    {
        return (durability != 0 && maxDurability != 0) ? (float)durability / (float)maxDurability : 0;
    }
    public long buyPrice => data.buyPrice;
    public long sellPrice => data.sellPrice;
    public long itemMallPrice => data.itemMallPrice;
    public bool sellable => data.sellable;
    public bool tradable => data.tradable;
    public bool destroyable => data.destroyable;
    public Sprite image => data.image;

    // helper function to check for valid durability if a durability item
    public bool CheckDurability() =>
        maxDurability == 0 || durability > 0;

    // tooltip
    public string ToolTip()
    {
        // note: caching StringBuilder is worse for GC because .Clear frees the internal array and reallocates.
        StringBuilder tip = new StringBuilder(data.ToolTip());

        // show durability only if item has durability
        if (maxDurability > 0)
            tip.Replace("{DURABILITY}", (DurabilityPercent() * 100).ToString("F0"));

        tip.Replace("{SUMMONEDHEALTH}", summonedHealth.ToString());
        tip.Replace("{SUMMONEDLEVEL}", summonedLevel.ToString());
        tip.Replace("{SUMMONEDEXPERIENCE}", summonedExperience.ToString());

        // addon system hooks
        Utils.InvokeMany(typeof(Item), this, "ToolTip_", tip);

        return tip.ToString();
    }

    public Item(ScriptableItem data)
    {
        hash = data.name.GetStableHashCode();
        durability = data.maxDurability;
        summoned = null;
        summonedHealth = data is SummonableItem summonable ? summonable.summonPrefab.health.max : 0;
        summonedLevel = data is SummonableItem ? 1 : 0;
        summonedExperience = 0;
        superDiced = false;

        luaScriptFlavor = null;
        luaScriptOnUse = null;
        luaScriptOnDoDamage= null;

        onDoDamageBegin = @"
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
        ";
        onDoDamageFinish = @"
        doit(slotid, item, input, out, caster, victim);
        end
        ";
        onDoDamageSystemMessage = @"My job is to create the snippet of code to inject into the following template:
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
For example, here is a result of what I build:

-- Chronomancer's Hourglass: If casting a 'Time Warp' spell, it generates a unique 'Temporal Echo' effect that changes depending on caster's health. Below 90% health, it decreases damage output but provides immediate self-healing. If the caster's health exceeds 90%, the 'Temporal Echo' evolves, reversing its effects: it increases damage output but causes immediate damage to the caster.

if skillid == toPrimaryId('Time Warp') then
	if caster_health <= 0.9 * caster_maxhealth then
    	out.added_damageout = -0.3 * damage -- decrease damage by 30%
    	out.added_selfheal = 0.2 * caster_maxhealth -- immediately heal 20% of caster's max health
	else
    	out.added_damageout = 0.3 * damage -- increase damage by 30%
    	out.added_selfheal = -0.2 * caster_maxhealth -- immediately take 20% of caster's max health as damage
	end
end
out.secondary_skill = toSecondaryId('Temporal Distortion') -- secondary spell is 'Temporal Distortion'
out.secondary_skillchance = 0.5 -- 50% chance to trigger the secondary skill";

        quality = 4;        // 4 is "best."
        prefixList = new List<string>();

    // FIXME: Randomly choose a set of prefixes. (Also choose a affix and base from these words.) 
    // "Fire" "Spectral" "Fabled" then from that set the item's random properties.
    // For now we choose 4 or more words to randomize the prompt because we are using low temperature ai calls.
    prefixList.Add("Fire");
    prefixList.Add("Ice");
    prefixList.Add("Lightning");
    prefixList.Add("Stone");
    prefixList.Add("Water");
    prefixList.Add("Air");
    prefixList.Add("Holy");
    prefixList.Add("Unholy");
    prefixList.Add("Arcane");
    prefixList.Add("Shadow");
    prefixList.Add("Force");
    prefixList.Add("Spirit");
    prefixList.Add("Nature");
    prefixList.Add("Ethereal");
    prefixList.Add("Mythic");
    prefixList.Add("Thunder");
    prefixList.Add("Frozen");
    prefixList.Add("Blazing");
    prefixList.Add("Radiant");
    prefixList.Add("Dark");
    prefixList.Add("Void");
    prefixList.Add("Earth");
    prefixList.Add("Wind");
    prefixList.Add("Toxic");
    prefixList.Add("Powerful");
    prefixList.Add("Infernal");
    prefixList.Add("Celestial");
    prefixList.Add("Temporal");
    prefixList.Add("Ancient");
    prefixList.Add("Primal");
    prefixList.Add("Vital");
    prefixList.Add("Astral");

    prefixList.Add("Spectral");
    prefixList.Add("Ghostly");
    prefixList.Add("Divine");
    prefixList.Add("Demonic");
    prefixList.Add("Fiery");
    prefixList.Add("Venomous");
    prefixList.Add("Terrifying");
    prefixList.Add("Mystic");
    prefixList.Add("Enchanted");
    prefixList.Add("Glowing");
    prefixList.Add("Cursed");
    prefixList.Add("Blessed");
    prefixList.Add("Runic");
    prefixList.Add("Sacred");
    prefixList.Add("Invisible");
    prefixList.Add("Adamant");
    prefixList.Add("Mighty");
    prefixList.Add("Brutal");
    prefixList.Add("Vicious");
    prefixList.Add("Ruthless");
    prefixList.Add("Furious");
    prefixList.Add("Invincible");
    prefixList.Add("Swift");
    prefixList.Add("Deadly");
    prefixList.Add("Armored");
    prefixList.Add("Unyielding");
    prefixList.Add("Energized");
    prefixList.Add("Sturdy");
    prefixList.Add("Elder");
    prefixList.Add("Harmonic");
    prefixList.Add("Chaotic");
    prefixList.Add("Titanic");
    prefixList.Add("Masterwork");
    prefixList.Add("Storm");
    prefixList.Add("Wicked");
    prefixList.Add("Peaceful");
    prefixList.Add("Piercing");
    prefixList.Add("Lunar");
    prefixList.Add("Solar");
    prefixList.Add("Stellar");
    prefixList.Add("Galactic");
    prefixList.Add("Timeless");
    prefixList.Add("Fearless");
    prefixList.Add("Silent");
    prefixList.Add("Volatile");
    prefixList.Add("Unstoppable");
    prefixList.Add("Inferno");
    prefixList.Add("Tsunami");
    prefixList.Add("Cyclonic");
    prefixList.Add("Seismic");
    prefixList.Add("Thunderous");
    prefixList.Add("Grim");
    prefixList.Add("Sinister");
    prefixList.Add("Hallowed");
    prefixList.Add("Wraith");
    prefixList.Add("Phoenix");
    prefixList.Add("Dragon");
    prefixList.Add("Wolf");
    prefixList.Add("Bear");
    prefixList.Add("Eagle");
    prefixList.Add("Lion");
    prefixList.Add("Tiger");
    prefixList.Add("Serpent");
    prefixList.Add("Pegasus");
    prefixList.Add("Unicorn");
    prefixList.Add("Chimera");
    prefixList.Add("Kraken");
    prefixList.Add("Leviathan");
    prefixList.Add("Griffin");
    prefixList.Add("Centaur");
    prefixList.Add("Gargoyle");
    prefixList.Add("Minotaur");
    prefixList.Add("Sphinx");
    prefixList.Add("Basilisk");

    prefixList.Add("Fabled");
    prefixList.Add("Legendary");
    prefixList.Add("Quantum");
    prefixList.Add("Abyssal");
    prefixList.Add("Harpy");
    prefixList.Add("Gorgon");
    prefixList.Add("Medusa");
    prefixList.Add("Cyclops");
    prefixList.Add("Troll");
    prefixList.Add("Giant");
    prefixList.Add("Elf");
    prefixList.Add("Dwarf");
    prefixList.Add("Orc");
    prefixList.Add("Goblin");
    prefixList.Add("Nymph");
    prefixList.Add("Siren");
    prefixList.Add("Satyr");
    prefixList.Add("Faun");
    prefixList.Add("Centurion");
    prefixList.Add("Viking");
    prefixList.Add("Spartan");
    prefixList.Add("Samurai");
    prefixList.Add("Ninja");
    prefixList.Add("Knight");
    prefixList.Add("Pirate");
    prefixList.Add("Pharaoh");
    prefixList.Add("Vampire");
    prefixList.Add("Werewolf");
    prefixList.Add("Zombie");
    prefixList.Add("Mummy");
    prefixList.Add("Ghost");
    prefixList.Add("Witch");
    prefixList.Add("Warlock");
    prefixList.Add("Necromancer");
    prefixList.Add("Sorcerer");
    prefixList.Add("Druid");
    prefixList.Add("Bard");
    prefixList.Add("Priest");
    prefixList.Add("Monk");
    prefixList.Add("Paladin");
    prefixList.Add("Hunter");
    prefixList.Add("Warrior");
    prefixList.Add("Rogue");
    prefixList.Add("Mage");
    prefixList.Add("Shaman");
    prefixList.Add("Cleric");
    prefixList.Add("Alchemist");
    prefixList.Add("Summoner");
    prefixList.Add("Psionic");
    prefixList.Add("Ranger");
    prefixList.Add("Thief");
    prefixList.Add("Assassin");
    prefixList.Add("Guardian");
    prefixList.Add("Duelist");
    prefixList.Add("Templar");
    prefixList.Add("Mariner");
    prefixList.Add("Patriarch");
    prefixList.Add("Matriarch");
    prefixList.Add("Warden");
    prefixList.Add("Slayer");
    prefixList.Add("Conqueror");
    prefixList.Add("Champion");
    prefixList.Add("Warlord");
    prefixList.Add("Overlord");
    prefixList.Add("Emperor");
    prefixList.Add("Empress");
    prefixList.Add("King");
    prefixList.Add("Queen");
    prefixList.Add("Prince");
    prefixList.Add("Princess");
    prefixList.Add("Duke");
    prefixList.Add("Duchess");
    prefixList.Add("Count");
    prefixList.Add("Countess");
    prefixList.Add("Baron");
    prefixList.Add("Baroness");
    prefixList.Add("Sir");
    prefixList.Add("Lady");
    prefixList.Add("Lord");
    prefixList.Add("Maiden");
    prefixList.Add("Hero");
    prefixList.Add("Heroine");

    prefixList.Add("Bloodthirsty");
    prefixList.Add("Nightwalker");
    prefixList.Add("Moonstruck");
    prefixList.Add("Crimson");
    prefixList.Add("Ghastly");
    prefixList.Add("Undying");
    prefixList.Add("Eternal");
    prefixList.Add("Nosferatu");
    prefixList.Add("Sanguine");
    prefixList.Add("Coffinborn");
    prefixList.Add("Crypt");
    prefixList.Add("Gravebound");
    prefixList.Add("Transylvanian");
    prefixList.Add("Batswarm");
    prefixList.Add("Vampiric");
    prefixList.Add("Shadowsoul");
    prefixList.Add("Twilight");
    prefixList.Add("Dusk");
    prefixList.Add("Darkvein");
    prefixList.Add("Fang");
    prefixList.Add("Nightstalker");
    prefixList.Add("Gloom");
    prefixList.Add("Veil");
    prefixList.Add("Abyss");
    prefixList.Add("Mist");
    prefixList.Add("Ebon");
    prefixList.Add("Midnight");
    prefixList.Add("Shroud");
    prefixList.Add("Raven");
    prefixList.Add("Obsidian");
    prefixList.Add("Dread");
    prefixList.Add("Wraith");
    prefixList.Add("Specter");
    prefixList.Add("Ghostly");
    prefixList.Add("Shadow");
    prefixList.Add("Haunting");
    prefixList.Add("Tomb");
    prefixList.Add("Sepulchral");
    prefixList.Add("Morbid");
    prefixList.Add("Revenant");
    prefixList.Add("Unhallowed");
    prefixList.Add("Pallid");
    prefixList.Add("Skeletal");
    prefixList.Add("Phantasmal");
    prefixList.Add("Ghoulish");
    prefixList.Add("Dreadful");
    prefixList.Add("Ghoul");
    prefixList.Add("Deathly");
    prefixList.Add("Creeping");
    prefixList.Add("Crawling");
    prefixList.Add("Lurking");
    prefixList.Add("Banshee");
    prefixList.Add("Nether");
    prefixList.Add("Necrotic");
    prefixList.Add("Blighted");
    prefixList.Add("Death");

    prefixList.Add("Lamenting");
    prefixList.Add("Silent");
    prefixList.Add("Whispering");
    prefixList.Add("Tormented");
    prefixList.Add("Forsaken");
    prefixList.Add("Desolate");
    prefixList.Add("Bleak");
    prefixList.Add("Forlorn");
    prefixList.Add("Haunted");
    prefixList.Add("Shadowy");
    prefixList.Add("Spectral");
    prefixList.Add("Ethereal");
    prefixList.Add("Ominous");
    prefixList.Add("Grim");
    prefixList.Add("Mournful");
    prefixList.Add("Darkened");
    prefixList.Add("Dreadful");
    prefixList.Add("Woe");
    prefixList.Add("Sorrow");
    prefixList.Add("Misery");
    prefixList.Add("Anguish");
    prefixList.Add("Grieving");
    prefixList.Add("Despair");
    prefixList.Add("Macabre");
    prefixList.Add("Sinister");
    prefixList.Add("Ghastly");
    prefixList.Add("Morose");
    prefixList.Add("Gloomy");
    prefixList.Add("Melancholy");
    prefixList.Add("Dismal");
    prefixList.Add("Somber");
    prefixList.Add("Dire");
    prefixList.Add("Doomed");
    prefixList.Add("Eerie");
    prefixList.Add("Ghostly");
    prefixList.Add("Phantom");
    prefixList.Add("Apparition");
    prefixList.Add("Specter");
    prefixList.Add("Cursed");
    prefixList.Add("Chilling");
    prefixList.Add("Terrifying");
    prefixList.Add("Horror");
    prefixList.Add("Fear");
    prefixList.Add("Terror");
    prefixList.Add("Spooky");
    prefixList.Add("Dread");
    prefixList.Add("Petrifying");
    prefixList.Add("Nightmare");
    prefixList.Add("Abandoned");
    prefixList.Add("Barren");
    prefixList.Add("Cryptic");
    prefixList.Add("Monstrous");
    prefixList.Add("Gargoyle");
    prefixList.Add("Demon");
    prefixList.Add("Fiend");
    prefixList.Add("Devil");
    prefixList.Add("Hellish");
    prefixList.Add("Infernal");
    prefixList.Add("Purgatory");
    prefixList.Add("Lycanthropic");
    prefixList.Add("Bat");
    prefixList.Add("Spider");
    prefixList.Add("Rat");
    prefixList.Add("Worm");
    prefixList.Add("Serpent");
    prefixList.Add("Raven");
    prefixList.Add("Owl");

    prefixList.Add("Eldritch");
    prefixList.Add("Ancient");
    prefixList.Add("Primordial");
    prefixList.Add("Abyssal");
    prefixList.Add("Chaotic");
    prefixList.Add("Cyclopean");
    prefixList.Add("Cthonic");
    prefixList.Add("Arcane");
    prefixList.Add("Mythos");
    prefixList.Add("Occult");
    prefixList.Add("Unspeakable");
    prefixList.Add("Non-Euclidean");
    prefixList.Add("Cryptic");
    prefixList.Add("Forbidden");
    prefixList.Add("Lost");
    prefixList.Add("Timeless");
    prefixList.Add("Eternal");
    prefixList.Add("Profane");
    prefixList.Add("Esoteric");
    prefixList.Add("Unknowable");
    prefixList.Add("Ineffable");
    prefixList.Add("Unseen");
    prefixList.Add("Obscure");
    prefixList.Add("Shadowy");
    prefixList.Add("Mysterious");
    prefixList.Add("Immemorial");
    prefixList.Add("Unfathomable");
    prefixList.Add("Nameless");
    prefixList.Add("Dread");
    prefixList.Add("Malevolent");
    prefixList.Add("Inhuman");
    prefixList.Add("Lurking");
    prefixList.Add("Monolithic");
    prefixList.Add("Silent");
    prefixList.Add("Void");
    prefixList.Add("Outer");
    prefixList.Add("Elder");
    prefixList.Add("Starless");
    prefixList.Add("Dream");
    prefixList.Add("R'lyehian");
    prefixList.Add("Madness");
    prefixList.Add("Insidious");
    prefixList.Add("Formless");
    prefixList.Add("Aeon");
    prefixList.Add("Unearthly");
    prefixList.Add("Twisted");
    prefixList.Add("Bizarre");
    prefixList.Add("Alien");
    prefixList.Add("Spectral");
    prefixList.Add("Ethereal");
    prefixList.Add("Otherworldly");
    prefixList.Add("Shoggoth");
    prefixList.Add("Nyarlathotep");
    prefixList.Add("Azathoth");
    prefixList.Add("Cthulhu");
    prefixList.Add("Yog-Sothoth");

    prefixList.Add("Nightmare");
    prefixList.Add("Boogeyman");
    prefixList.Add("Shadow");
    prefixList.Add("Closet");
    prefixList.Add("Dark");
    prefixList.Add("Monster");
    prefixList.Add("Goblin");
    prefixList.Add("Troll");
    prefixList.Add("Witch");
    prefixList.Add("Ghost");
    prefixList.Add("Haunted");
    prefixList.Add("Spectral");
    prefixList.Add("Spider");
    prefixList.Add("Creepy");
    prefixList.Add("Crawly");
    prefixList.Add("Scary");
    prefixList.Add("Dreadful");
    prefixList.Add("Fearful");
    prefixList.Add("Horrifying");
    prefixList.Add("Ghastly");
    prefixList.Add("Gruesome");
    prefixList.Add("Chilling");
    prefixList.Add("Eerie");
    prefixList.Add("Phantom");
    prefixList.Add("Underbed");
    prefixList.Add("Cryptic");
    prefixList.Add("Shriek");
    prefixList.Add("Bloodcurdling");
    prefixList.Add("Frightful");
    prefixList.Add("Graveyard");
    prefixList.Add("Moonlit");
    prefixList.Add("Midnight");
    prefixList.Add("Howling");
    prefixList.Add("Grim");
    prefixList.Add("Growling");
    prefixList.Add("Sinister");
    prefixList.Add("Rattling");
    prefixList.Add("Creaking");
    prefixList.Add("Squeaking");
    prefixList.Add("Thumping");
    prefixList.Add("Whispering");
    prefixList.Add("Banshee");
    prefixList.Add("Screaming");
    prefixList.Add("Mummy");
    prefixList.Add("Skeleton");
    prefixList.Add("Zombie");
    prefixList.Add("Biting");
    prefixList.Add("Stalking");
    prefixList.Add("Lurking");
    prefixList.Add("Slithering");
    prefixList.Add("Creeping");
    prefixList.Add("Foggy");
    prefixList.Add("Windy");
    prefixList.Add("Stormy");
    prefixList.Add("Gusty");
    prefixList.Add("Ominous");

    prefixList.Add("Apocalyptic");
    prefixList.Add("Cataclysmic");
    prefixList.Add("Maddening");
    prefixList.Add("Unholy");
    prefixList.Add("Infernal");
    prefixList.Add("Malevolent");
    prefixList.Add("Diabolical");
    prefixList.Add("Ravaging");
    prefixList.Add("Terror");
    prefixList.Add("Pandemonium");
    prefixList.Add("Ruinous");
    prefixList.Add("Catastrophic");
    prefixList.Add("Anarchic");
    prefixList.Add("Chaotic");
    prefixList.Add("Disastrous");
    prefixList.Add("Destructive");
    prefixList.Add("Devastating");
    prefixList.Add("Blighted");
    prefixList.Add("Frenzied");
    prefixList.Add("Wrathful");
    prefixList.Add("Ruthless");
    prefixList.Add("Savage");
    prefixList.Add("Unrelenting");
    prefixList.Add("Vengeful");
    prefixList.Add("Insidious");
    prefixList.Add("Ravenous");
    prefixList.Add("Tormented");
    prefixList.Add("Feral");
    prefixList.Add("Malefic");
    prefixList.Add("Nefarious");
    prefixList.Add("Atrocious");
    prefixList.Add("Invidious");
    prefixList.Add("Malignant");
    prefixList.Add("Venomous");
    prefixList.Add("Baneful");
    prefixList.Add("Maledict");
    prefixList.Add("Pernicious");
    prefixList.Add("Depraved");
    prefixList.Add("Calamitous");
    prefixList.Add("Cruel");
    prefixList.Add("Merciless");
    prefixList.Add("Iniquitous");
    prefixList.Add("Vile");
    prefixList.Add("Doom");
    prefixList.Add("Inferno");
    prefixList.Add("Hellfire");
    prefixList.Add("Oblivion");
    prefixList.Add("Armageddon");


        prefixes = prefixList.ToArray();

        prefix1 = "";
        prefix2 = "";
        prefix3 = "";
        prefix4 = "";
        affix1 = "";


        
        skillsList = new List<string>();

        skillsList.Add("Projectile");
        skillsList.Add("Melee");
        skillsList.Add("Time Warp");
        skillsList.Add("Healing Wave");
        skillsList.Add("Reality Distortion");
        skillsList.Add("Cosmic Overload");
        skillsList.Add("Chaotic Blast");
        skillsList.Add("Temporal Distortion");

skillsList.Add("Fireball");
skillsList.Add("Arcane Shield");
skillsList.Add("Wind Gust");
skillsList.Add("Earthquake");
skillsList.Add("Water Surge");
skillsList.Add("Lightning Bolt");
skillsList.Add("Void Tear");
skillsList.Add("Gravity Crush");
skillsList.Add("Astral Projection");
skillsList.Add("Mental Break");
skillsList.Add("Cryo Freeze");
skillsList.Add("Venom Strike");
skillsList.Add("Lunar Blessing");
skillsList.Add("Solar Flare");
skillsList.Add("Meteor Shower");
skillsList.Add("Blink");
skillsList.Add("Mirror Image");
skillsList.Add("Divine Wrath");
skillsList.Add("Hellfire Rain");
skillsList.Add("Spiritual Healing");
skillsList.Add("Revive");
skillsList.Add("Shadow Strike");
skillsList.Add("Ghoul Touch");
skillsList.Add("Dark Nova");
skillsList.Add("Plague Spread");
skillsList.Add("Life Drain");
skillsList.Add("Spectral Chains");
skillsList.Add("Death's Grasp");
skillsList.Add("Ethereal Form");
skillsList.Add("Mana Burst");
skillsList.Add("Soul Swap");
skillsList.Add("Time Echo");
skillsList.Add("Quantum Leap");
skillsList.Add("Parallel Paradox");
skillsList.Add("Dimensional Rift");
skillsList.Add("Psychic Scream");
skillsList.Add("Mind Control");
skillsList.Add("Phantom Blade");
skillsList.Add("Frost Wave");
skillsList.Add("Earth Spike");
skillsList.Add("Tidal Wave");
skillsList.Add("Wind Tunnel");
skillsList.Add("Lava Eruption");
skillsList.Add("Lightning Chain");
skillsList.Add("Nature's Wrath");
skillsList.Add("Sonic Boom");
skillsList.Add("Invisibility");
skillsList.Add("Stone Skin");
skillsList.Add("Flame Shield");
skillsList.Add("Ice Armor");
skillsList.Add("Astral Armor");
skillsList.Add("Spiritual Barrier");
skillsList.Add("Repulsion Field");
skillsList.Add("Soul Shield");
skillsList.Add("Divine Intervention");
skillsList.Add("Banish");
skillsList.Add("Resurrection");
skillsList.Add("Spectral Form");
skillsList.Add("Demonic Possession");
skillsList.Add("Arcane Barrage");
skillsList.Add("Siphon Life");
skillsList.Add("Enthrall");
skillsList.Add("Mirror Realm");
skillsList.Add("Necrotic Touch");
skillsList.Add("Petrify");
skillsList.Add("Polymorph");
skillsList.Add("Shadow Dance");
skillsList.Add("Soul Reaper");
skillsList.Add("Elemental Overload");
skillsList.Add("Summon Golem");
skillsList.Add("Raise Dead");
skillsList.Add("Mind Shatter");
skillsList.Add("Nether Swap");
skillsList.Add("Dimensional Shift");
skillsList.Add("Time Dilation");
skillsList.Add("Entropic Decay");
skillsList.Add("Stellar Detonation");
skillsList.Add("Void Collapse");
skillsList.Add("Cosmic Rewind");
skillsList.Add("Aether Pulse");
skillsList.Add("Galactic Beam");
skillsList.Add("Reality Rupture");
skillsList.Add("Singularity Crush");
skillsList.Add("Temporal Echo");
skillsList.Add("Gravitational Vortex");
skillsList.Add("Stasis Field");
skillsList.Add("Chrono Burst");
skillsList.Add("Astral Communion");
skillsList.Add("Mind Meld");
skillsList.Add("Telekinetic Grasp");
skillsList.Add("Psychic Onslaught");
skillsList.Add("Kinetic Barrage");
skillsList.Add("Telepathic Assault");
skillsList.Add("Psychokinetic Blast");

skillsList.Add("Telesthetic Strike");
skillsList.Add("Mental Fury");
skillsList.Add("Precognitive Strike");
skillsList.Add("Psychic Disruption");
skillsList.Add("Mind Over Matter");
skillsList.Add("Psychic Crush");
skillsList.Add("Psychoportation");
skillsList.Add("Brain Drain");
skillsList.Add("Mental Mirage");
skillsList.Add("Thought Theft");
skillsList.Add("Cerebral Shockwave");
skillsList.Add("Astral Infusion");
skillsList.Add("Pyroclasmic Burst");
skillsList.Add("Aqueous Assault");
skillsList.Add("Stoneform Stomp");
skillsList.Add("Galeforce Gust");
skillsList.Add("Thunderclap");
skillsList.Add("Aetheric Arrow");
skillsList.Add("Glacial Grasp");
skillsList.Add("Meteor Maul");
skillsList.Add("Burning Brand");
skillsList.Add("Aquatic Aura");
skillsList.Add("Granite Grip");
skillsList.Add("Tempest Torrent");
skillsList.Add("Voltage Volley");
skillsList.Add("Frostbite Fury");
skillsList.Add("Lunar Lunge");
skillsList.Add("Solar Slam");
skillsList.Add("Polar Push");
skillsList.Add("Solar Smash");
skillsList.Add("Galactic Grind");
skillsList.Add("Supernova Smash");
skillsList.Add("Eclipse Edge");
skillsList.Add("Quasar Quake");
skillsList.Add("Black Hole Blast");
skillsList.Add("Radiant Rain");
skillsList.Add("Event Horizon Edge");
skillsList.Add("Stellar Storm");
skillsList.Add("Celestial Strike");
skillsList.Add("Astral Assault");
skillsList.Add("Comet Crash");
skillsList.Add("Starfall Slam");
skillsList.Add("Planetary Punch");
skillsList.Add("Cosmic Crush");
skillsList.Add("Wormhole Warp");
skillsList.Add("Temporal Tornado");
skillsList.Add("Reality Ripple");
skillsList.Add("Dimensional Drive");
skillsList.Add("Quantum Quake");
skillsList.Add("Continuum Crack");
skillsList.Add("Parallel Puncture");
skillsList.Add("Time Travel Twist");
skillsList.Add("Universe Unravel");
skillsList.Add("Interdimensional Impact");
skillsList.Add("Singularity Slice");
skillsList.Add("Vortex Volley");
skillsList.Add("Infinity Inferno");
skillsList.Add("Multiverse Maelstrom");
skillsList.Add("Void Vengeance");
skillsList.Add("Galaxy Gash");
skillsList.Add("Spacetime Shred");
skillsList.Add("Orbit Obliterate");
skillsList.Add("Stardust Slash");
skillsList.Add("Nebula Nuke");
skillsList.Add("Meteoroid Maul");
skillsList.Add("Photon Fist");
skillsList.Add("Pulsar Punch");
skillsList.Add("Supernova Surge");
skillsList.Add("Quasar Quell");
skillsList.Add("Comet Clobber");
skillsList.Add("Starburst Smash");
skillsList.Add("Galactic Grasp");
skillsList.Add("Universe Uppercut");
skillsList.Add("Cosmic Cleave");
skillsList.Add("Nebula Nova");
skillsList.Add("Orbital Onslaught");
skillsList.Add("Interstellar Impact");
skillsList.Add("Space Slam");
skillsList.Add("Time Tear");
skillsList.Add("Reality Rend");
skillsList.Add("Dimensional Discharge");
skillsList.Add("Astral Annihilation");
skillsList.Add("Celestial Clobber");
skillsList.Add("Stellar Smite");
skillsList.Add("Quantum Quell");
skillsList.Add("Cosmic Cataclysm");
skillsList.Add("Event Horizon Haze");
skillsList.Add("Black Hole Burst");

        skills = skillsList.ToArray();
        
        prefix1 = getPrefix();
        prefix2 = getPrefix();
        prefix3 = getPrefix();
        prefix4 = getPrefix();
        affix1 = getPrefix();

        if(data is EquipmentItem && !superDiced) {
            SuperDice((EquipmentItem) data);
        }
    }

}
