using System;
using System.Collections.Generic;
using System.Linq;
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
    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        Dictionary<string, StructuredItemData> lastEquippedItemState;
        Dictionary<int, StructuredItemData> lastEquippedCharmState;

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
        }

        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += D2IDOnDataRead;
        }

        void D2IDOnDataRead(object sender, DataReadEventArgs e)
        {
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
            bool stateDidChange = false;
            foreach (string location in lastEquippedItemState.Keys.ToArray())
            {
                if (equippedItems.ContainsKey(location))
                {
                    if (lastEquippedItemState[location] == null || lastEquippedItemState[location].guid != equippedItems[location].guid)
                    {
                        lastEquippedItemState[location] = equippedItems[location];
                        Logger.Info("Overwrote " + location);
                        stateDidChange = true;
                    }
                }
                else
                {
                    if (lastEquippedItemState[location] != null)
                    {
                        lastEquippedItemState[location] = null;
                        Logger.Info("Removed " + location);
                        stateDidChange = true;
                    }
                }
            }

            HashSet<string> charmBaseNames = new HashSet<string> { "Small Charm", "Large Charm", "Grand Charm" };
            Dictionary<int, StructuredItemData> charms = e.structuredInventory.filter((StructuredItemData item) =>
            {
                return charmBaseNames.Contains(item.baseName) && item.page != "Stash";
            });


        }

        // Data that should be sent when the inventory state changes
        void ProcessInventoryChange()
        {

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
