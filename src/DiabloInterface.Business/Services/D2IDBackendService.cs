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
    public class InventorySnapshot
    {
        public Dictionary<string, StructuredItemData> equipmentState;
        public Dictionary<int, StructuredItemData> charmState;
    }

    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        private static readonly HttpClient client = new HttpClient();
        Dictionary<string, StructuredItemData> lastEquippedItemState;
        Dictionary<int, StructuredItemData> lastEquippedCharmState;
        string baseURI;

        public D2IDBackendService(ISettingsService settingsService, IGameService gameService)
        {
            Logger.Info("Creating D2ID backend service.");
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            RegisterServiceEventHandlers();

            lastEquippedItemState = new Dictionary<string, StructuredItemData>();
            foreach (BodyLocation location in Enum.GetValues(typeof(BodyLocation)))
            {
                if (location != BodyLocation.None)
                {
                    lastEquippedItemState[location.ToString()] = null;
                }
            }

            lastEquippedCharmState = new Dictionary<int, StructuredItemData>();

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
            //*** START INVENTORY CHECK
            Dictionary<int, StructuredItemData> equippedItemsTemp = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.location != "None" && item.page == "Equipped";
            });

            // Invert the item state into BodyLoc -> StructuredItemData pairs
            Dictionary<string, StructuredItemData> equippedItems = new Dictionary<string, StructuredItemData>();
            foreach (StructuredItemData item in equippedItemsTemp.Values)
            {
                equippedItems[item.location] = item;
            }

            // Update the equipped item state
            bool inventoryStateDidChange = false;
            foreach (string location in lastEquippedItemState.Keys.ToArray())
            {
                if (equippedItems.ContainsKey(location))
                {
                    if (lastEquippedItemState[location] == null || lastEquippedItemState[location].guid != equippedItems[location].guid)
                    {
                        lastEquippedItemState[location] = equippedItems[location];
                        Logger.Info("Overwrote " + location);
                        inventoryStateDidChange = true;
                    }
                }
                else
                {
                    if (lastEquippedItemState[location] != null)
                    {
                        lastEquippedItemState[location] = null;
                        Logger.Info("Removed " + location);
                        inventoryStateDidChange = true;
                    }
                }
            }

            // Get the inventory charm state
            HashSet<string> charmBaseNames = new HashSet<string> { "Small Charm", "Large Charm", "Grand Charm" };
            Dictionary<int, StructuredItemData> equippedCharms = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return charmBaseNames.Contains(item.baseName) && item.page != "Stash" && item.properties.Count > 0;
            });

            // Update the inventory charm state
            HashSet<int> updateGuids = new HashSet<int>(lastEquippedCharmState.Keys);
            HashSet<int> currGuids = new HashSet<int>(equippedCharms.Keys);
            updateGuids.SymmetricExceptWith(currGuids);
            foreach (int guid in updateGuids)
            {
                if (lastEquippedCharmState.Keys.Contains(guid))
                {
                    lastEquippedCharmState.Remove(guid);
                    Logger.Info("Removed charm: " + guid.ToString());
                    inventoryStateDidChange = true;
                }
                if (equippedCharms.Keys.Contains(guid))
                {
                    lastEquippedCharmState[guid] = equippedCharms[guid];
                    Logger.Info("Added charm: " + guid.ToString());
                    inventoryStateDidChange = true;
                }
            }

            // Process inventory state change event
            if (inventoryStateDidChange)
            {
                InventorySnapshot inventoryState = new InventorySnapshot
                {
                    equipmentState = lastEquippedItemState,
                    charmState = lastEquippedCharmState
                };
                await ProcessInventoryChange(inventoryState);
            }
            //*** END INVENTORY CHECK


        }

        // Data that should be sent when the inventory state changes
        async Task ProcessInventoryChange(InventorySnapshot inventoryState)
        {
            // Construct inventory JSON object and send to backend asynchronously
            string json = JsonConvert.SerializeObject(inventoryState);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://" + baseURI + ":8080/snapshots/test/test/equipped", content);
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
