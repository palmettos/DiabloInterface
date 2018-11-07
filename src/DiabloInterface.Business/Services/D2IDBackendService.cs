using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Zutatensuppe.DiabloInterface.Business.Services
{
    using System;
    using System.Reflection;
    using Zutatensuppe.D2Reader;
    using Zutatensuppe.D2Reader.Models;
    using Zutatensuppe.D2Reader.Struct.Item;
    using Zutatensuppe.DiabloInterface.Business.Data;
    using Zutatensuppe.DiabloInterface.Business.IO;
    using Zutatensuppe.DiabloInterface.Core.Logging;
    public class D2IDBackendService
    {
        static readonly ILogger Logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        Dictionary<BodyLocation, string> inventoryState;
        public D2IDBackendService(ISettingsService settingsService, IGameService gameService)
        {
            Logger.Info("Creating D2ID backend service.");
            if (settingsService == null) throw new ArgumentNullException(nameof(settingsService));
            if (gameService == null) throw new ArgumentNullException(nameof(gameService));
            this.settingsService = settingsService;
            this.gameService = gameService;
            RegisterServiceEventHandlers();
            inventoryState = new Dictionary<BodyLocation, string>();
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
            Dictionary<string, List<BodyLocation>> locsToChange = new Dictionary<string, List<BodyLocation>>()
            {
                {"add", new List<BodyLocation>()},
                {"remove", new List<BodyLocation>()}
            };
            bool didItemStateChange = false;
            Logger.Info("New iteration");
            foreach (BodyLocation loc in Enum.GetValues(typeof(BodyLocation)))
            {
                if (inventoryState.ContainsKey(loc) && !e.ItemStrings.ContainsKey(loc))
                {
                    locsToChange["remove"].Add(loc);
                    didItemStateChange = true;
                }
                else if (!inventoryState.ContainsKey(loc) && e.ItemStrings.ContainsKey(loc))
                {
                    locsToChange["add"].Add(loc);
                    didItemStateChange = true;
                }
                else if (inventoryState.ContainsKey(loc) && e.ItemStrings.ContainsKey(loc))
                {
                    if (inventoryState[loc] != e.ItemStrings[loc])
                    {
                        locsToChange["add"].Add(loc);
                        didItemStateChange = true;
                    }
                }
            }
            foreach (var key in locsToChange.Keys)
            {
                foreach (var loc in locsToChange[key])
                {
                    switch (key)
                    {
                        case "add":
                            Logger.Info(loc.ToString());
                            Logger.Info("Added to equipped items");
                            inventoryState[loc] = e.ItemStrings[loc];
                            break;
                        case "remove":
                            Logger.Info(loc.ToString());
                            Logger.Info("Removed from equipped items");
                            inventoryState.Remove(loc);
                            break;
                    }
                }
            }
        }
    }
}
