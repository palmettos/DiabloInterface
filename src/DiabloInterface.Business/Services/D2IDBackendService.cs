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

    public class InventoryState
    {
        public Dictionary<BodyLocation, StructuredItemData> state;

        public InventoryState()
        {
            state = new Dictionary<BodyLocation, StructuredItemData>();

            foreach (BodyLocation location in Enum.GetValues(typeof(BodyLocation)))
            {
                if (location != BodyLocation.None)
                {
                    state.Add(location, new StructuredItemData());
                }
            }
        }
    }

    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        InventoryState inventoryState;
        public D2IDBackendService(ISettingsService settingsService, IGameService gameService)
        {
            Logger.Info("Creating D2ID backend service.");
            if (settingsService == null) throw new ArgumentNullException(nameof(settingsService));
            if (gameService == null) throw new ArgumentNullException(nameof(gameService));
            this.settingsService = settingsService;
            this.gameService = gameService;
            RegisterServiceEventHandlers();
            inventoryState = new InventoryState();
            string invJson = JsonConvert.SerializeObject(inventoryState.state);
            Logger.Info(invJson);
        }
        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += D2IDOnDataRead;
            gameService.CharacterCreated += D2IDOnCharacterCreated;
        }
        void D2IDOnCharacterCreated(object sender, CharacterCreatedEventArgs e)
        {
            Logger.Info("New character created.");
        }

        void D2IDOnDataRead(object sender, DataReadEventArgs e)
        {
            bool stateDidChange = false;

            foreach (BodyLocation location in Enum.GetValues(typeof(BodyLocation)))
            {
                if (location != BodyLocation.None)
                {
                    if (e.structuredItems.ContainsKey(location))
                    {
                        if (inventoryState.state[location].guid != e.structuredItems[location].guid)
                        {
                            inventoryState.state[location] = e.structuredItems[location];
                            Logger.Info("Overwrote " + location.ToString());
                            stateDidChange = true;
                        }
                    }
                    else
                    {
                        if (inventoryState.state[location].guid != 0)
                        {
                            inventoryState.state[location] = new StructuredItemData();
                            Logger.Info("Removed " + location.ToString());
                            stateDidChange = true;
                        }
                    }
                }
            }
        }
    }
}
