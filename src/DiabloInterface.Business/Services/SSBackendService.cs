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
    public class SSEndpoint
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

    public interface ISSEndpointHandler
    {
        string getURI();
        void updateURI(string channelName, string characterName);
        void processGameState(DataReadEventArgs state);
        bool isSendRequired();
        SSPacket getPacket(string channel, Character character);
        Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet);
    }

    public class AttributesEndpointHandler : ISSEndpointHandler
    {
        private SSEndpoint uri;
        private ILogger logger;
        private Dictionary<string, int> values;
        private bool sendRequired;

        public AttributesEndpointHandler(ILogger logger)
        {
            this.logger = logger;
            uri = new SSEndpoint();

            values = new Dictionary<string, int>();
            values["strength"] = 0;
            values["dexterity"] = 0;
            values["vitality"] = 0;
            values["energy"] = 0;
            values["fireResist"] = 0;
            values["coldResist"] = 0;
            values["lightningResist"] = 0;
            values["poisonResist"] = 0;
            values["fasterHitRecovery"] = 0;
            values["fasterRunWalk"] = 0;
            values["fasterCastRate"] = 0;
            values["increasedAttackSpeed"] = 0;
        }

        public string getURI()
        {
            return uri.getURI();
        }

        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/api/v1/snapshots/attributes");
        }

        public void processGameState(DataReadEventArgs state)
        {
            sendRequired = false;
            HashSet<string> newValues = new HashSet<string>
            {
                string.Concat(state.Character.Strength, "STR"),
                string.Concat(state.Character.Dexterity, "DEX"),
                string.Concat(state.Character.Vitality, "VIT"),
                string.Concat(state.Character.Energy, "ENE"),

                string.Concat(state.Character.FireResist, "FR"),
                string.Concat(state.Character.ColdResist, "CR"),
                string.Concat(state.Character.LightningResist, "LR"),
                string.Concat(state.Character.PoisonResist, "PR"),

                string.Concat(state.Character.FasterHitRecovery, "FHR"),
                string.Concat(state.Character.FasterRunWalk, "FRW"),
                string.Concat(state.Character.FasterCastRate, "FCR"),
                string.Concat(state.Character.IncreasedAttackSpeed, "IAS")
            };

            HashSet<string> oldValues = new HashSet<string>
            {
                string.Concat(values["strength"], "STR"),
                string.Concat(values["dexterity"], "DEX"),
                string.Concat(values["vitality"], "VIT"),
                string.Concat(values["energy"], "ENE"),

                string.Concat(values["fireResist"], "FR"),
                string.Concat(values["coldResist"], "CR"),
                string.Concat(values["lightningResist"], "LR"),
                string.Concat(values["poisonResist"], "PR"),

                string.Concat(values["fasterHitRecovery"], "FHR"),
                string.Concat(values["fasterRunWalk"], "FRW"),
                string.Concat(values["fasterCastRate"], "FCR"),
                string.Concat(values["increasedAttackSpeed"], "IAS")
            };
            oldValues.SymmetricExceptWith(newValues);
            if (oldValues.Count > 0)
            {
                values = new Dictionary<string, int>();
                values["strength"] = state.Character.Strength;
                values["dexterity"] = state.Character.Dexterity;
                values["vitality"] = state.Character.Vitality;
                values["energy"] = state.Character.Energy;
                values["fireResist"] = state.Character.FireResist;
                values["coldResist"] = state.Character.ColdResist;
                values["lightningResist"] = state.Character.LightningResist;
                values["poisonResist"] = state.Character.PoisonResist;
                values["fasterHitRecovery"] = state.Character.FasterHitRecovery;
                values["fasterRunWalk"] = state.Character.FasterRunWalk;
                values["fasterCastRate"] = state.Character.FasterCastRate;
                values["increasedAttackSpeed"] = state.Character.IncreasedAttackSpeed;
                sendRequired = true;
            }
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public SSPacket getPacket(string channel, Character character)
        {
            return new SSPacket(channel, character, values);
        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending packet to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
    }

    public class GoldEndpointHandler: ISSEndpointHandler
    {
        private SSEndpoint uri;
        private ILogger logger;
        private Dictionary<string, int> values;
        private bool sendRequired;
        private bool initialized;
        private string characterName;

        public GoldEndpointHandler(ILogger logger)
        {
            this.logger = logger;
            uri = new SSEndpoint();
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
            uri.setPath("/api/v1/snapshots/gold");
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

        public SSPacket getPacket(string channel, Character character)
        {
            return new SSPacket(channel, character, values);

        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending packet to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
    }

    public class EquippedEndpointHandler: ISSEndpointHandler
    {
        private SSEndpoint uri;
        private ILogger logger;
        private HashSet<string> hashedEquipmentState;
        private Dictionary<int, StructuredItemData> equipmentState;
        private HashSet<int> hashedCharmState;
        private Dictionary<int, StructuredItemData> charmState;
        private bool sendRequired;

        public EquippedEndpointHandler(ILogger logger)
        {
            uri = new SSEndpoint();
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
            uri.setPath("/api/v1/snapshots/items");
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

        public SSPacket getPacket(string channel, Character character)
        {
            return new SSPacket(channel, character, equipmentState.Values.Concat(charmState.Values));

        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending packet to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
    }

    public class SkillsEndpointHandler : ISSEndpointHandler
    {
        private SSEndpoint uri;
        private ILogger logger;
        private HashSet<string> hashedSkills;
        private OrderedDictionary skillState;
        private bool sendRequired;

        public SkillsEndpointHandler(ILogger logger)
        {
            uri = new SSEndpoint();
            this.logger = logger;
            hashedSkills = new HashSet<string>();
            sendRequired = false;
        }

        public string getURI()
        {
            return uri.getURI();
        }


        public void updateURI(string channelName, string characterName)
        {
            uri.setPath("/api/v1/snapshots/skills");
        }

        public void processGameState(DataReadEventArgs state)
        {
            sendRequired = false;

            HashSet<string> newSkills = new HashSet<string>();
            foreach (string key in state.skillLevels.Keys)
            {
                newSkills.Add(string.Concat(key, state.skillLevels[key]));
            }

            hashedSkills.SymmetricExceptWith(newSkills);
            if (hashedSkills.Count > 0)
            {
                logger.Info("Skill state changed, send required...");
                sendRequired = true;
            }
            hashedSkills = newSkills;
            skillState = state.skillLevels;
        }

        public bool isSendRequired()
        {
            return sendRequired;
        }

        public SSPacket getPacket(string channel, Character character)
        {
            return new SSPacket(channel, character, skillState);
        }

        public async Task<HttpResponseMessage> sendSnapshot(HttpClient client, string dest, string packet)
        {
            logger.Info("Sending skills snapshot to: " + dest);
            var content = new StringContent(packet, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(dest, content);
            return response;
        }
    }

    public class SSPacket
    {
        public Dictionary<string, object> data;

        public SSPacket(string channel, Character character, object payload)
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

    public class SSBackendService
    {
        static readonly ILogger logger = LogServiceLocator.Get(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISettingsService settingsService;
        readonly IGameService gameService;
        private static readonly HttpClient client = new HttpClient();
        private List<ISSEndpointHandler> handlers;
        private bool enabled;
        private string username;
        private string password;

        public SSBackendService(ISettingsService settingsService, IGameService gameService)
        {
            logger.Info("Creating SS backend service.");
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            settingsService.SettingsChanged += SettingsChanged;
            this.gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            RegisterServiceEventHandlers();

            handlers = new List<ISSEndpointHandler>
            {
                new EquippedEndpointHandler(logger),
                new SkillsEndpointHandler(logger),
                new GoldEndpointHandler(logger),
                new AttributesEndpointHandler(logger)
            };

            enabled = settingsService.CurrentSettings.SanctuaryStatsEnabled;
            username = settingsService.CurrentSettings.SanctuaryStatsUsername;
            password = settingsService.CurrentSettings.SanctuaryStatsKey;

            var bytes = Encoding.ASCII.GetBytes(username + ":" + password);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(bytes)
            );
        }

        void RegisterServiceEventHandlers()
        {
            gameService.DataRead += SSOnDataRead;
        }

        void SettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            username = e.Settings.SanctuaryStatsUsername;
            password = e.Settings.SanctuaryStatsKey;
            enabled = e.Settings.SanctuaryStatsEnabled;

            var bytes = Encoding.ASCII.GetBytes(username + ":" + password);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(bytes)
            );

            logger.Info("Updating settings for SSBackendHandler:");
            logger.Info(e.Settings.SanctuaryStatsEnabled);
            logger.Info(e.Settings.SanctuaryStatsUsername);
            logger.Info(e.Settings.SanctuaryStatsKey);
        }

        async void SSOnDataRead(object sender, DataReadEventArgs e)
        {
            Queue<Dictionary<string, object>> sendingHandlers = new Queue<Dictionary<string, object>>();
            // Perform all steps that are required to be synchronous
            foreach (var handler in handlers)
            {
                handler.updateURI("test_channel", e.Character.Name);
                handler.processGameState(e);
                if (handler.isSendRequired() && enabled)
                {
                    string uri = handler.getURI();
                    SSPacket packet = handler.getPacket("test_channel", e.Character);
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
                var response = await ((ISSEndpointHandler)currentHandler["handler"]).sendSnapshot(client, uri, packet);
                logger.Info(response.StatusCode.ToString());
            }
        }
    }
}
