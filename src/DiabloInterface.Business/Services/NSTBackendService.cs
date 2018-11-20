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
        string getURI();
        void updateURI(string channelName, string characterName);
        void processGameState(DataReadEventArgs state);
        bool isSendRequired();
        NSTPacket getPacket(string channel, Character character);
        Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet);
    }

    public class GoldEndpointHandler: INSTEndpointHandler
    {
        private NSTEndpoint uri;
        private ILogger logger;
        private Dictionary<string, int> values;
        private bool sendRequired;
        private bool initialized;
        private string characterName;

        public GoldEndpointHandler(ILogger logger)
        {
            this.logger = logger;
            uri = new NSTEndpoint();
            values = new Dictionary<string, int>();
            values["currentGold"] = 0;
            values["delta"] = 0;
            sendRequired = false;
            initialized = false;
            characterName = null;
        }

        public string getURI()
        {
            return uri.getURI();
        }

        // TODO: Remove this method from the interface as it's no longer needed
        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/snapshots/gold");
        }

        public void processInitialState(DataReadEventArgs state)
        {
            characterName = state.Character.Name;
            values["currentGold"] = state.Character.Gold + state.Character.GoldStash;
            values["delta"] = 0;
            initialized = true;
        }

        public void processGameState(DataReadEventArgs state)
        {
            sendRequired = false;
            if (!initialized || !characterName.Equals(state.Character.Name))
            {
                processInitialState(state);
                return;
            }

            int newGold = state.Character.Gold + state.Character.GoldStash;
            if (values["currentGold"] != newGold)
            {
                values["delta"] = newGold - values["currentGold"];
                values["currentGold"] = newGold;
                sendRequired = true;
            }
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public NSTPacket getPacket(string channel, Character character)
        {
            return new NSTPacket(channel, character, values);

        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending packet to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
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

        public string getURI()
        {
            return uri.getURI();
        }

        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/snapshots/items");
        }

        public void processGameState(DataReadEventArgs state)
        {
            sendRequired = false;

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
                sendRequired = true;
                logger.Info("Item state changed, send required...");
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
                sendRequired = true;
                logger.Info("Charm state changed, send required...");
            }
            hashedCharmState = newCharmsHash;
            charmState = newEquippedCharms;
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public NSTPacket getPacket(string channel, Character character)
        {
            return new NSTPacket(channel, character, equipmentState.Values.Concat(charmState.Values));

        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending packet to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
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

        public string getURI()
        {
            return uri.getURI();
        }


        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/snapshots/skills");
        }

        public void processGameState(DataReadEventArgs state)
        {
            sendRequired = false;
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
                logger.Info("Skill state changed, send required...");
                sendRequired = true;
            }
            hashedSkillNames = newSkillNames;
            hashedSkillLevels = newSkillLevels;

            skillState = state.skillLevels;
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public NSTPacket getPacket(string channel, Character character)
        {
            return new NSTPacket(channel, character, skillState);
        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending skills snapshot to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
    }

    public class NSTPacket
    {
        public Dictionary<string, object> data;

        public NSTPacket(string channel, Character character, object payload)
        {
            data = new Dictionary<string, object>();
            // I'll come back to this in the future if not having a timestamp
            // actually becomes an issue.
            // data["timestamp"] = Utility.GetUnixTimestamp();
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

        public NSTBackendService(ISettingsService settingsService, IGameService gameService)
        {
            logger.Info("Creating NST backend service.");
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            RegisterServiceEventHandlers();

            handlers = new List<INSTEndpointHandler>
            {
                new EquippedEndpointHandler(logger),
                new SkillsEndpointHandler(logger),
                new GoldEndpointHandler(logger)
            };
        }

        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += NSTOnDataRead;
        }

        async void NSTOnDataRead(object sender, DataReadEventArgs e)
        {
            Queue<Dictionary<string, object>> sendingHandlers = new Queue<Dictionary<string, object>>();
            // Perform all steps that are required to be synchronous
            foreach (var handler in handlers)
            {
                handler.updateURI("test_channel", e.Character.Name);
                handler.processGameState(e);
                if (handler.isSendRequired())
                {
                    string uri = handler.getURI();
                    NSTPacket packet = handler.getPacket("test_channel", e.Character);
                    Dictionary<string, object> handlerState = new Dictionary<string, object>();
                    handlerState["handler"] = handler;
                    handlerState["uri"] = uri;
                    handlerState["packet"] = packet.asJson();
                    logger.Info(handlerState["packet"]);
                    sendingHandlers.Enqueue(handlerState);
                }
            }

            while (sendingHandlers.Count > 0)
            {
                Dictionary<string, object> currentHandler = sendingHandlers.Dequeue();
                string uri = (string)currentHandler["uri"];
                string packet = (string)currentHandler["packet"];
                var response = await ((INSTEndpointHandler)currentHandler["handler"]).sendSnapshot(client, uri, packet);
                logger.Info(response.StatusCode.ToString());
            }
        }
    }
}
