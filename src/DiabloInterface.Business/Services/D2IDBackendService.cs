using System;
using System.Collections.Generic;
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

    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        private static readonly HttpClient client = new HttpClient();
        string baseURI;

        InventoryState currentState = new InventoryState();

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

            currentState.equipmentStateHash.SymmetricExceptWith(newEquippedHash);
            if (currentState.equipmentStateHash.Count > 0)
            {
                Logger.Info("Equipped items changed");
                equippedItemsChanged = true;
            }
            currentState.equipmentStateHash = newEquippedHash;
            currentState.equipmentState = equippedItems;

            HashSet<string> charmBases = new HashSet<string> { "Small Charm", "Large Charm", "Grand Charm" };
            Dictionary<int, StructuredItemData> equippedCharms = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.page == "Inventory" && charmBases.Contains(item.baseName);
            });

            HashSet<int> newCharmsHash = new HashSet<int>(equippedCharms.Keys);
            currentState.charmStateHash.SymmetricExceptWith(newCharmsHash);
            if (currentState.charmStateHash.Count > 0)
            {
                Logger.Info("Equipped charms changed");
                equippedItemsChanged = true;
            }
            currentState.charmStateHash = newCharmsHash;
            currentState.charmState = equippedCharms;

            if (equippedItemsChanged)
            {
                await ProcessInventoryChange(currentState);
            }
        }

        // Data that should be sent when the inventory state changes
        async Task ProcessInventoryChange(InventoryState inventoryState)
        {
            // Construct inventory JSON object and send to backend asynchronously
            string json = JsonConvert.SerializeObject(inventoryState);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://" + baseURI + ":8080/snapshots/equipped/test/test", content);
            var responseStr = await response.Content.ReadAsStringAsync();
            Logger.Info("Response: " + responseStr);
        }

        // Data that should be sent when the character's stats change
        void ProcessCharacterStatChange()
        {

        }

        // Data that should be sent when the character levels up
        void ProcessCharacterLevelUp()
        {

        }
    }
}
