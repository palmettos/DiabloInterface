using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Zutatensuppe.D2Reader;
using Zutatensuppe.DiabloInterface.Core;
using Zutatensuppe.D2Reader.Models;
using Zutatensuppe.D2Reader.Struct.Item;
using Zutatensuppe.DiabloInterface.Business.Data;
using Zutatensuppe.DiabloInterface.Business.IO;
using Zutatensuppe.DiabloInterface.Core.Logging;
using Newtonsoft.Json;

namespace Zutatensuppe.DiabloInterface.Business.Services
{
    public class NSTEndpoint
    {
        private string scheme = "http";
        private string authority = "//localhost:8080";
        private string path;

        public void setPath(string path)
        {
            this.path = path;
        }

        public void appendPath(string path)
        {
            this.path += path;
        }

        public string getURI()
        {
            return scheme + ":" + authority + path;
        }
    }

    public interface INSTEndpointHandler
    {
        void updateURI(string channelName, string characterName);
        void processGameState(DataReadEventArgs state);
        bool isSendRequired();
        string getSerializedPayload();
        Task sendSnapshot(HttpClient client, string packet);
    }

    public class EquippedEndpointHandler: INSTEndpointHandler
    {
        private NSTEndpoint uri;
        private ILogger logger;
        private HashSet<string> hashedEquipmentState;
        private Dictionary<int, StructuredItemData> equipmentState;
        private HashSet<int> hashedCharmState;
        private Dictionary<int, StructuredItemData> charmState;
        private bool sendRequired;

        public EquippedEndpointHandler(ILogger logger)
        {
            uri = new NSTEndpoint();
            this.logger = logger;
            hashedEquipmentState = new HashSet<string>();
            equipmentState = new Dictionary<int, StructuredItemData>();
            hashedCharmState = new HashSet<int>();
            charmState = new Dictionary<int, StructuredItemData>();
            sendRequired = false;
        }

        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/snapshots/equipped/" + channelName + "/" + characterName);
        }

        public void processGameState(DataReadEventArgs state)
        {
            bool equippedItemsChanged = false;

            Dictionary<int, StructuredItemData> newEquippedItems = state.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.location != "None" && item.page == "Equipped";
            });

            HashSet<string> newHashedEquipped = new HashSet<string>();
            foreach (var item in newEquippedItems.Values)
            {
                string str = item.location + item.asString();
                newHashedEquipped.Add(str);
            }

            hashedEquipmentState.SymmetricExceptWith(newHashedEquipped);
            if (hashedEquipmentState.Count > 0)
            {
                equippedItemsChanged = true;
                logger.Info("Item state changed...");
            }
            hashedEquipmentState = newHashedEquipped;
            equipmentState = newEquippedItems;

            HashSet<string> charmBases = new HashSet<string> { "Small Charm", "Large Charm", "Grand Charm" };
            Dictionary<int, StructuredItemData> newEquippedCharms = state.structuredInventory.filter((StructuredItemData item) =>
            {
                return item.page == "Inventory" && charmBases.Contains(item.baseName) && item.properties.Count > 0;
            });

            HashSet<int> newCharmsHash = new HashSet<int>(newEquippedCharms.Keys);
            hashedCharmState.SymmetricExceptWith(newCharmsHash);
            if (hashedCharmState.Count > 0)
            {
                equippedItemsChanged = true;
                logger.Info("Item state changed...");
            }
            hashedCharmState = newCharmsHash;
            charmState = newEquippedCharms;

            if (equippedItemsChanged)
            {
                sendRequired = true;
                logger.Info("Equipped state send required.");
            }
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public string getSerializedPayload()
        {
            return JsonConvert.SerializeObject(equipmentState.Values.Concat(charmState.Values));
        }

        public async Task sendSnapshot(HttpClient client, string packet)
        {
            logger.Info("Sending packet to: " + uri.getURI());
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri.getURI(), content);
            var responseStr = await response.Content.ReadAsStringAsync();
            logger.Info(responseStr);
            sendRequired = false;
        }
    }

    public class SkillsEndpointHandler : INSTEndpointHandler
    {
        private NSTEndpoint uri;
        private ILogger logger;
        private HashSet<string> hashedSkillNames;
        private HashSet<int> hashedSkillLevels;
        private OrderedDictionary skillState;
        private bool sendRequired;

        public SkillsEndpointHandler(ILogger logger)
        {
            uri = new NSTEndpoint();
            this.logger = logger;
            hashedSkillNames = new HashSet<string>();
            hashedSkillLevels = new HashSet<int>();
            sendRequired = false;
        }

        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/snapshots/skills/" + channelName + "/" + characterName);
        }

        public void processGameState(DataReadEventArgs state)
        {
            bool skillLevelsChanged = false;
            HashSet<string> newSkillNames = new HashSet<string>();
            foreach (string key in state.skillLevels.Keys)
            {
                newSkillNames.Add(key);
            }
            HashSet<int> newSkillLevels = new HashSet<int>();
            foreach (int val in state.skillLevels.Values)
            {
                newSkillLevels.Add(val);
            }

            hashedSkillNames.SymmetricExceptWith(newSkillNames);
            hashedSkillLevels.SymmetricExceptWith(newSkillLevels);
            if (hashedSkillNames.Count > 0 || hashedSkillLevels.Count > 0)
            {
                logger.Info("Skill state changed");
                skillLevelsChanged = true;
            }
            hashedSkillNames = newSkillNames;
            hashedSkillLevels = newSkillLevels;

            if (skillLevelsChanged)
            {
                sendRequired = true;
                logger.Info("Skills state send required.");
            }
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public string getSerializedPayload()
        {
            return JsonConvert.SerializeObject(skillState);
        }

        public async Task sendSnapshot(HttpClient client, string packet)
        {
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri.getURI(), content);
            var responseStr = await response.Content.ReadAsStringAsync();
            logger.Info(responseStr);
            sendRequired = false;
        }
    }

    public class NSTPacket
    {
        public Dictionary<string, object> data;

        public NSTPacket(string channel, Character character, string payload)
        {
            data = new Dictionary<string, object>();
            data["timestamp"] = Utility.GetUnixTimestamp();
            data["channel"] = channel;
            data["characterName"] = character.Name;
            data["characterClass"] = character.CharClass.ToString();
            data["characterLevel"] = character.Level;
            data["payload"] = payload;
        }

        public string asJson()
        {
            return JsonConvert.SerializeObject(data);
        }
    }

    public class NSTBackendService
    {
        static readonly ILogger logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        private static readonly HttpClient client = new HttpClient();
        private List<INSTEndpointHandler> handlers;

        private int healingPotionCount;
        private int manaPotionCount;

        public NSTBackendService(ISettingsService settingsService, IGameService gameService)
        {
            logger.Info("Creating NST backend service.");
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            RegisterServiceEventHandlers();

            handlers = new List<INSTEndpointHandler>
            {
                new EquippedEndpointHandler(logger),
                new SkillsEndpointHandler(logger)
            };

            int healingPotionCount = 0;
            int manaPotionCount = 0;
        }

        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += NSTOnDataRead;
        }

        async void NSTOnDataRead(object sender, DataReadEventArgs e)
        {
            Dictionary<INSTEndpointHandler, string> sendingHandlers = new Dictionary<INSTEndpointHandler, string>();
            foreach (var handler in handlers)
            {
                handler.updateURI("test_channel", e.Character.Name);
                handler.processGameState(e);
                if (handler.isSendRequired())
                {
                    string payload = handler.getSerializedPayload();
                    NSTPacket packet = new NSTPacket("test_channel", e.Character, payload);
                    sendingHandlers[handler] = packet.asJson();
                }
            }

            foreach (var handler in sendingHandlers.Keys)
            {
                await handler.sendSnapshot(client, sendingHandlers[handler]);
            }

            //// START POTION COUNTING
            //Dictionary<int, StructuredItemData> healingPotions = e.structuredInventory.filter((StructuredItemData item) =>
            //{
            //    return item.baseName.Contains("Healing Potion");
            //});

            //Dictionary<int, StructuredItemData> manaPotions = e.structuredInventory.filter((StructuredItemData item) =>
            //{
            //    return item.baseName.Contains("Mana Potion");
            //});
            //// END POTION COUNTING
        }
    }
}
