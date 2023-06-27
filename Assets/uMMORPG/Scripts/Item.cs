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
    public string luaScriptOnDamageDealtTo;

    public bool superDiced;

    // constructors
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
        luaScriptOnDamageDealtTo = null;

        if(data is EquipmentItem && !superDiced) {
            SuperDice((EquipmentItem) data);
        }

    }
    
    private async void getAIForOnDamageDealtTo(EquipmentItem data) {
        luaScriptOnDamageDealtTo = "FIXME: need to call into the AI got get the unqiue script for this item."; //-jrr
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
        getAIForOnDamageDealtTo(data);

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
}
