using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Zutatensuppe.D2Reader;
using Zutatensuppe.D2Reader.Models;
using Zutatensuppe.D2Reader.Struct.Item;
using Zutatensuppe.DiabloInterface.Business.Data;
using Zutatensuppe.DiabloInterface.Business.IO;
using Zutatensuppe.DiabloInterface.Core.Logging;
using Newtonsoft.Json;

namespace Zutatensuppe.DiabloInterface.Business.Services
{
    public class InventoryState
    {
        public HashSet<string> equipmentStateHash;
        public Dictionary<int, StructuredItemData> equipmentState;

        public HashSet<int> charmStateHash;
        public Dictionary<int, StructuredItemData> charmState;

        public InventoryState()
        {
            equipmentStateHash = new HashSet<string>();
            equipmentState = new Dictionary<int, StructuredItemData>();

            charmStateHash = new HashSet<int>();
            charmState = new Dictionary<int, StructuredItemData>();
        }
    }

    public class SkillState
    {
        public OrderedDictionary state;
        public HashSet<string> skillNames;
        public HashSet<int> skillLevels;

        public SkillState()
        {
            state = new OrderedDictionary();
            skillNames = new HashSet<string>();
            skillLevels = new HashSet<int>();
        }
    }

    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        private static readonly HttpClient client = new HttpClient();
        string baseURI;

        InventoryState currentInventoryState = new InventoryState();
        SkillState currentSkillState = new SkillState();

        public D2IDBackendService(ISettingsService settingsService, IGameService gameService)
        {
            Logger.Info("Creating D2ID backend service.");
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            RegisterServiceEventHandlers();

#if DEBUG
            baseURI = "localhost";
#else
            baseURI = "d2id.multilurk.tv";
#endif
        }

        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += D2IDOnDataRead;
        }

        async void D2IDOnDataRead(object sender, DataReadEventArgs e)
        {
            bool equippedItemsChanged = false;
            bool skillLevelsChanged = false;

            // START INVENTORY CHECK
            Dictionary<int, StructuredItemData> equippedItems = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.location != "None" && item.page == "Equipped";
            });

            HashSet<string> newEquippedHash = new HashSet<string>();
            foreach (var item in equippedItems.Values)
            {
                string str = item.location + item.asString();
                newEquippedHash.Add(str);
            }

            currentInventoryState.equipmentStateHash.SymmetricExceptWith(newEquippedHash);
            if (currentInventoryState.equipmentStateHash.Count > 0)
            {
                Logger.Info("Equipped items changed");
                equippedItemsChanged = true;
            }
            currentInventoryState.equipmentStateHash = newEquippedHash;
            currentInventoryState.equipmentState = equippedItems;

            HashSet<string> charmBases = new HashSet<string> { "Small Charm", "Large Charm", "Grand Charm" };
            Dictionary<int, StructuredItemData> equippedCharms = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.page == "Inventory" && charmBases.Contains(item.baseName) && item.properties.Count > 0;
            });

            HashSet<int> newCharmsHash = new HashSet<int>(equippedCharms.Keys);
            currentInventoryState.charmStateHash.SymmetricExceptWith(newCharmsHash);
            if (currentInventoryState.charmStateHash.Count > 0)
            {
                Logger.Info("Equipped charms changed");
                equippedItemsChanged = true;
            }
            currentInventoryState.charmStateHash = newCharmsHash;
            currentInventoryState.charmState = equippedCharms;
            // END INVENTORY CHECK

            // START SKILL CHECK
            HashSet<string> newSkillNames = new HashSet<string>();
            foreach (string key in e.skillLevels.Keys)
            {
                newSkillNames.Add(key);
            }
            HashSet<int> newSkillLevels = new HashSet<int>();
            foreach (int val in e.skillLevels.Values)
            {
                newSkillLevels.Add(val);
            }

            currentSkillState.skillNames.SymmetricExceptWith(newSkillNames);
            currentSkillState.skillLevels.SymmetricExceptWith(newSkillLevels);
            if (currentSkillState.skillNames.Count > 0 || currentSkillState.skillLevels.Count > 0)
            {
                Logger.Info("Skill state changed");
                skillLevelsChanged = true;
            }
            currentSkillState.skillNames = newSkillNames;
            currentSkillState.skillLevels = newSkillLevels;
            // END SKILL CHECK

            // TODO: JSON stringify all objects synchronously before awaiting any Task
            // and pass JSON strings into async calls because underlying state
            // can be modified while a Task is awaited.
            // This serialization makes input object deterministic regardless
            // of network latency or the state of the event loop.
            // (Not necessarily input ORDER, but that's okay. DB will index by timestamp.)
            if (equippedItemsChanged)
            {
                await ProcessInventoryChange(currentInventoryState);
            }
            if (skillLevelsChanged)
            {
                await ProcessSkillChange();
            }
        }

        // Data that should be sent when the inventory state changes
        // TODO: Rewrite to accept stringified JSON as input
        async Task ProcessInventoryChange(InventoryState inventoryState)
        {
            // Receive JSON object and send to backend asynchronously
            string json = JsonConvert.SerializeObject(inventoryState);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://" + baseURI + ":8080/snapshots/equipped/test/test", content);
            var responseStr = await response.Content.ReadAsStringAsync();
            Logger.Info("Response: " + responseStr);
        }

        // Data that should be sent when the character's stats change
        async Task ProcessCharacterStatChange()
        {

        }

        // Data that should be sent when the character levels up
        async Task ProcessCharacterLevelUp()
        {

        }

        async Task ProcessSkillChange()
        {

        }
    }
}
