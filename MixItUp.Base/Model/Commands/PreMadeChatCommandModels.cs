﻿using MixItUp.Base.Commands;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class PreMadeChatCommandSettings
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public bool IsEnabled { get; set; }
        [DataMember]
        public UserRoleEnum Role { get; set; }
        [DataMember]
        public int Cooldown { get; set; }

        public PreMadeChatCommandSettings() { }

        public PreMadeChatCommandSettings(PreMadeChatCommandModelBase command)
        {
            this.Name = command.Name;
            this.IsEnabled = command.IsEnabled;
            this.Role = command.Requirements.Role.Role;
            this.Cooldown = command.Requirements.Cooldown.Amount;
        }
    }

    public abstract class PreMadeChatCommandModelBase : ChatCommandModel
    {
        public PreMadeChatCommandModelBase(string name, string trigger, int cooldown, UserRoleEnum role) : this(name, new HashSet<string>() { trigger }, cooldown, role) { }

        public PreMadeChatCommandModelBase(string name, HashSet<string> triggers, int cooldown, UserRoleEnum role)
            : base(name, CommandTypeEnum.PreMade, triggers, includeExclamation: true, wildcards: false)
        {
            this.Requirements.Requirements.Add(new RoleRequirementModel(role));
            this.Requirements.Requirements.Add(new CooldownRequirementModel(CooldownTypeEnum.Standard, cooldown));
        }

        public void UpdateFromSettings(PreMadeChatCommandSettings settings)
        {
            this.IsEnabled = settings.IsEnabled;
            this.Requirements.Role.Role = settings.Role;
            this.Requirements.Cooldown.Amount = settings.Cooldown;
        }

        protected override Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            return Task.FromResult(0);
        }

        public override bool DoesCommandHaveWork { get { return true; } }
    }

    public class MixItUpPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public MixItUpPreMadeChatCommandModel() : base("Mix It Up", "mixitup", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.Services.Chat.SendMessage("This channel uses the Mix It Up app to improve their stream. Check out http://mixitupapp.com for more information!");
        }
    }

    public class CommandsPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public CommandsPreMadeChatCommandModel() : base(MixItUp.Base.Resources.Commands, "commands", 0, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            List<string> commandTriggers = new List<string>();
            // TODO
            //foreach (ChatCommandModel command in ChannelSession.AllEnabledChatCommands)
            //{
            //    if (await command.Requirements.Validate(user))
            //    {
            //        if (command.IncludeExclamation)
            //        {
            //            commandTriggers.AddRange(command.Triggers.Select(c => $"!{c}"));
            //        }
            //        else
            //        {
            //            commandTriggers.AddRange(command.Triggers);
            //        }
            //    }
            //}

            if (commandTriggers.Count > 0)
            {
                string text = "Available Commands: " + string.Join(", ", commandTriggers.OrderBy(c => c));
                await ChannelSession.Services.Chat.SendMessage(text);
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("There are no commands available for you to use.");
            }
        }
    }

    public class GamesPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public GamesPreMadeChatCommandModel() : base(MixItUp.Base.Resources.Games, "games", 0, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            List<string> commandTriggers = new List<string>();
            foreach (GameCommandBase command in ChannelSession.Settings.GameCommands)
            {
                if (command.IsEnabled && await command.Requirements.DoesMeetUserRoleRequirement(user))
                {
                    commandTriggers.AddRange(command.Commands.Select(c => $"!{c}"));
                }
            }

            if (commandTriggers.Count > 0)
            {
                string text = "Available Games: " + string.Join(", ", commandTriggers.OrderBy(c => c));
                await ChannelSession.Services.Chat.SendMessage(text);
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("There are no games available for you to use.");
            }
        }
    }

    public class MixItUpCommandsPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public MixItUpCommandsPreMadeChatCommandModel() : base(MixItUp.Base.Resources.MixItUpCommands, "mixitupcommands", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.Services.Chat.SendMessage("All common, Mix It Up chat commands can be found here: https://github.com/SaviorXTanren/mixer-mixitup/wiki/Pre-Made-Chat-Commands. For commands specific to this stream, ask your streamer/moderator.");
        }
    }

    public class GamePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public GamePreMadeChatCommandModel() : base(MixItUp.Base.Resources.Game, "game", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.RefreshChannel();
            if (ChannelSession.TwitchChannelV5 != null)
            {
                GameInformation details = await XboxGamePreMadeChatCommandModel.GetXboxGameInfo(ChannelSession.TwitchChannelV5.game);
                if (details == null)
                {
                    details = await SteamGamePreMadeChatCommandModel.GetSteamGameInfo(ChannelSession.TwitchChannelV5.game);
                }

                if (details != null)
                {
                    await ChannelSession.Services.Chat.SendMessage(details.ToString());
                }
                else
                {
                    await ChannelSession.Services.Chat.SendMessage("Game: " + ChannelSession.TwitchChannelV5.game);
                }
            }
        }
    }

    public class TitlePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public TitlePreMadeChatCommandModel() : base(MixItUp.Base.Resources.Title, new HashSet<string>() { "title", "stream" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.RefreshChannel();
            await ChannelSession.Services.Chat.SendMessage("Stream Title: " + ChannelSession.TwitchChannelV5.status);
        }
    }

    public class UptimePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public static Task<DateTimeOffset> GetStartTime()
        {
            DateTimeOffset startTime = DateTimeOffset.MinValue;
            if (ChannelSession.TwitchStreamIsLive)
            {
                startTime = TwitchPlatformService.GetTwitchDateTime(ChannelSession.TwitchStreamV5.created_at).GetValueOrDefault();
            }
            return Task.FromResult(startTime);
        }

        public UptimePreMadeChatCommandModel() : base(MixItUp.Base.Resources.Uptime, "uptime", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            DateTimeOffset startTime = await UptimeChatCommand.GetStartTime();
            if (startTime > DateTimeOffset.MinValue)
            {
                TimeSpan duration = DateTimeOffset.Now.Subtract(startTime);
                await ChannelSession.Services.Chat.SendMessage("Start Time: " + startTime.ToString("MMMM dd, yyyy - h:mm tt") + ", Stream Length: " + (int)duration.TotalHours + duration.ToString("\\:mm"));
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Stream is currently offline");
            }
        }
    }

    public class FollowAgePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public FollowAgePreMadeChatCommandModel() : base(MixItUp.Base.Resources.FollowAge, "followage", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.Services.Chat.SendMessage(user.Username + "'s Follow Age: " + user.FollowAgeString);
        }
    }

    public class SubscribeAgePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public SubscribeAgePreMadeChatCommandModel() : base(MixItUp.Base.Resources.SubscribeAge, new HashSet<string>() { "subage", "subscribeage" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.Services.Chat.SendMessage(user.Username + "'s Subscribe Age: " + user.SubscribeAgeString);
        }
    }

    public class StreamerAgePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public StreamerAgePreMadeChatCommandModel() : base(MixItUp.Base.Resources.StreamerAge, new HashSet<string>() { "streamerage", "age" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            await ChannelSession.Services.Chat.SendMessage(user.Username + "'s Streamer Age: " + user.AccountAgeString);
        }
    }

    public class QuotePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public QuotePreMadeChatCommandModel() : base(MixItUp.Base.Resources.Quote, new HashSet<string>() { "quote", "quotes" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (ChannelSession.Settings.QuotesEnabled)
            {
                if (ChannelSession.Settings.Quotes.Count > 0)
                {
                    int quoteNumber = 0;
                    UserQuoteViewModel quote = null;

                    if (arguments.Count() == 1)
                    {
                        if (!int.TryParse(arguments.ElementAt(0), out quoteNumber))
                        {
                            await ChannelSession.Services.Chat.SendMessage("USAGE: !quote [QUOTE NUMBER]");
                            return;
                        }

                        quote = ChannelSession.Settings.Quotes.SingleOrDefault(q => q.ID == quoteNumber);
                        if (quote == null)
                        {
                            await ChannelSession.Services.Chat.SendMessage($"Unable to find quote number {quoteNumber}.");
                        }
                    }
                    else if (arguments.Count() == 0)
                    {
                        int quoteIndex = RandomHelper.GenerateRandomNumber(ChannelSession.Settings.Quotes.Count);
                        quote = ChannelSession.Settings.Quotes[quoteIndex];
                    }
                    else
                    {
                        await ChannelSession.Services.Chat.SendMessage("USAGE: !quote [QUOTE NUMBER]");
                        return;
                    }

                    if (quote != null)
                    {
                        await ChannelSession.Services.Chat.SendMessage(quote.ToString());
                    }
                }
                else
                {
                    await ChannelSession.Services.Chat.SendMessage("At least 1 quote must be added for this feature to work");
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Quotes must be enabled for this feature to work");
            }
        }
    }

    public class LastQuotePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public LastQuotePreMadeChatCommandModel() : base(MixItUp.Base.Resources.LastQuote, new HashSet<string>() { "lastquote" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (ChannelSession.Settings.QuotesEnabled)
            {
                if (ChannelSession.Settings.Quotes.Count > 0)
                {
                    UserQuoteViewModel quote = ChannelSession.Settings.Quotes.LastOrDefault();
                    if (quote != null)
                    {
                        await ChannelSession.Services.Chat.SendMessage(quote.ToString());
                        return;
                    }
                }
                await ChannelSession.Services.Chat.SendMessage("At least 1 quote must be added for this feature to work");
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Quotes must be enabled for this feature to work");
            }
        }
    }

    public class AddQuotePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public AddQuotePreMadeChatCommandModel() : base(MixItUp.Base.Resources.AddQuote, new HashSet<string>() { "addquote", "quoteadd" }, 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (ChannelSession.Settings.QuotesEnabled)
            {
                if (arguments.Count() > 0)
                {
                    StringBuilder quoteBuilder = new StringBuilder();
                    foreach (string arg in arguments)
                    {
                        quoteBuilder.Append(arg + " ");
                    }

                    string quoteText = quoteBuilder.ToString();
                    quoteText = quoteText.Trim(new char[] { ' ', '\'', '\"' });

                    UserQuoteViewModel quote = new UserQuoteViewModel(quoteText, DateTimeOffset.Now, ChannelSession.TwitchChannelV5?.game);
                    ChannelSession.Settings.Quotes.Add(quote);
                    await ChannelSession.SaveSettings();

                    GlobalEvents.QuoteAdded(quote);

                    if (ChannelSession.Services.Chat != null)
                    {
                        await ChannelSession.Services.Chat.SendMessage("Added " + quote.ToString());
                    }
                }
                else
                {
                    await ChannelSession.Services.Chat.SendMessage("Usage: !addquote <FULL QUOTE TEXT>");
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Quotes must be enabled with Mix It Up for this feature to work");
            }
        }
    }

    public class Magic8BallPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        private List<String> responses = new List<string>()
            {
                "It is certain",
                "It is decidedly so",
                "Without a doubt",
                "Yes definitely",
                "You may rely on it",
                "As I see it, yes",
                "Most likely",
                "Outlook good",
                "Yes",
                "Signs point to yes",
                "Reply hazy try again",
                "Ask again later",
                "Better not tell you now",
                "Cannot predict now",
                "Concentrate and ask again",
                "Don't count on it",
                "My reply is no",
                "My sources say no",
                "Outlook not so good",
                "Very doubtful",
                "Ask your mother",
                "Ask your father",
                "Come back later, I'm sleeping",
                "Yeah...sure, whatever",
                "Hahaha...no...",
                "I don't know, blame @SaviorXTanren..."
            };

        public Magic8BallPreMadeChatCommandModel() : base(MixItUp.Base.Resources.MagicEightBall, new HashSet<string>() { "magic8ball", "8ball" }, 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            int index = RandomHelper.GenerateRandomNumber(this.responses.Count);
            await ChannelSession.Services.Chat.SendMessage(string.Format("The Magic 8-Ball says: \"{0}\"", this.responses[index]));
        }
    }

    public class GameInformation
    {
        public string Name { get; set; }
        public double Price { get; set; }
        public string Uri { get; set; }

        public override string ToString()
        {
            return $"Game: {Name} - {Price} - {Uri}";
        }
    }

    public class XboxGamePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public static async Task<GameInformation> GetXboxGameInfo(string gameName)
        {
            gameName = gameName.ToLower();

            string cv = Convert.ToBase64String(Guid.NewGuid().ToByteArray(), 0, 12);

            using (AdvancedHttpClient client = new AdvancedHttpClient("https://displaycatalog.mp.microsoft.com"))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MixItUp");
                client.DefaultRequestHeaders.Add("MS-CV", cv);

                HttpResponseMessage response = await client.GetAsync($"v7.0/productFamilies/Games/products?query={HttpUtility.UrlEncode(gameName)}&$top=1&market=US&languages=en-US&fieldsTemplate=StoreSDK&isAddon=False&isDemo=False&actionFilter=Browse");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    JObject jobj = JObject.Parse(result);
                    JArray products = jobj["Products"] as JArray;
                    if (products?.FirstOrDefault() is JObject product)
                    {
                        string productId = product["ProductId"]?.Value<string>();
                        string name = product["LocalizedProperties"]?.First()?["ProductTitle"]?.Value<string>();
                        double price = product["DisplaySkuAvailabilities"]?.First()?["Availabilities"]?.First()?["OrderManagementData"]?["Price"]?["ListPrice"]?.Value<double>() ?? 0.0;
                        string uri = $"https://www.microsoft.com/store/apps/{productId}";

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(productId) && name.ToLower().Contains(gameName))
                        {
                            return new GameInformation { Name = name, Price = price, Uri = uri };
                        }
                    }
                }
            }

            return null;
        }

        public XboxGamePreMadeChatCommandModel() : base(MixItUp.Base.Resources.XboxGame, "xboxgame", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            string gameName = null;
            if (arguments.Count() > 0)
            {
                gameName = string.Join(" ", arguments);
            }
            else
            {
                await ChannelSession.RefreshChannel();
                if (ChannelSession.TwitchChannelV5 != null)
                {
                    gameName = ChannelSession.TwitchChannelV5.game;
                }
            }

            GameInformation details = await XboxGamePreMadeChatCommandModel.GetXboxGameInfo(gameName);
            if (details != null)
            {
                await ChannelSession.Services.Chat.SendMessage(details.ToString());
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage(string.Format("Could not find the game \"{0}\" on Xbox", gameName));
            }
        }
    }

    public class SteamGamePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        private static Dictionary<string, int> steamGameList = new Dictionary<string, int>();

        public static async Task<GameInformation> GetSteamGameInfo(string gameName)
        {
            gameName = gameName.ToLower();

            if (steamGameList.Count == 0)
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient("http://api.steampowered.com/"))
                {
                    HttpResponseMessage response = await client.GetAsync("ISteamApps/GetAppList/v0002");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject jobj = JObject.Parse(result);
                        JToken list = jobj["applist"]["apps"];
                        JArray games = (JArray)list;
                        foreach (JToken game in games)
                        {
                            SteamGamePreMadeChatCommandModel.steamGameList[game["name"].ToString().ToLower()] = (int)game["appid"];
                        }
                    }
                }
            }

            int gameID = -1;
            if (SteamGamePreMadeChatCommandModel.steamGameList.ContainsKey(gameName))
            {
                gameID = SteamGamePreMadeChatCommandModel.steamGameList[gameName];
            }
            else
            {
                string foundGame = SteamGamePreMadeChatCommandModel.steamGameList.Keys.FirstOrDefault(g => g.Contains(gameName));
                if (foundGame != null)
                {
                    gameID = SteamGamePreMadeChatCommandModel.steamGameList[foundGame];
                }
            }

            if (gameID > 0)
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient("http://store.steampowered.com/"))
                {
                    HttpResponseMessage response = await client.GetAsync("api/appdetails?appids=" + gameID);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        JObject jobj = JObject.Parse(result);
                        if (jobj[gameID.ToString()] != null && jobj[gameID.ToString()]["data"] != null)
                        {
                            jobj = (JObject)jobj[gameID.ToString()]["data"];

                            double price = 0.0;
                            if (jobj["price_overview"] != null && jobj["price_overview"]["final"] != null)
                            {
                                price = (int)jobj["price_overview"]["final"];
                                price = price / 100.0;
                            }

                            string url = string.Format("http://store.steampowered.com/app/{0}", gameID);

                            return new GameInformation { Name = jobj["name"].Value<string>(), Price = price, Uri = url };
                        }
                    }
                }
            }
            return null;
        }

        public SteamGamePreMadeChatCommandModel() : base(MixItUp.Base.Resources.SteamGame, "steamgame", 5, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            string gameName = null;
            if (arguments.Count() > 0)
            {
                gameName = string.Join(" ", arguments);
            }
            else
            {
                await ChannelSession.RefreshChannel();
                if (ChannelSession.TwitchChannelV5 != null)
                {
                    gameName = ChannelSession.TwitchChannelV5.game;
                }
            }

            GameInformation details = await SteamGamePreMadeChatCommandModel.GetSteamGameInfo(gameName);
            if (details != null)
            {
                await ChannelSession.Services.Chat.SendMessage(details.ToString());
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage(string.Format("Could not find the game \"{0}\" on Steam", gameName));
            }
        }
    }

    public class SetTitlePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public SetTitlePreMadeChatCommandModel() : base(MixItUp.Base.Resources.SetTitle, "settitle", 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() > 0)
            {
                string name = string.Join(" ", arguments);
                await ChannelSession.TwitchUserConnection.UpdateV5Channel(ChannelSession.TwitchChannelV5, status: name);
                await ChannelSession.RefreshChannel();
                await ChannelSession.Services.Chat.SendMessage("Title Updated: " + name);
                return;
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !settitle <TITLE NAME>");
            }
        }
    }

    public class SetGamePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public SetGamePreMadeChatCommandModel() : base(MixItUp.Base.Resources.SetGame, "setgame", 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() > 0)
            {
                string name = string.Join(" ", arguments).ToLower();
                IEnumerable<Twitch.Base.Models.NewAPI.Games.GameModel> games = await ChannelSession.TwitchUserConnection.GetNewAPIGamesByName(name);
                if (games != null && games.Count() > 0)
                {
                    Twitch.Base.Models.NewAPI.Games.GameModel game = games.FirstOrDefault(g => g.name.ToLower().Equals(name));
                    if (game == null)
                    {
                        game = games.First();
                    }
                    await ChannelSession.TwitchUserConnection.UpdateV5Channel(ChannelSession.TwitchChannelV5, game: game);
                    await ChannelSession.RefreshChannel();
                    await ChannelSession.Services.Chat.SendMessage("Game Updated: " + game.name);
                    return;
                }
                await ChannelSession.Services.Chat.SendMessage("We could not find a game with that name");
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !setgame <GAME NAME>");
            }
        }
    }

    public class SetUserTitlePreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public SetUserTitlePreMadeChatCommandModel() : base(MixItUp.Base.Resources.SetUserTitle, "setusertitle", 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() > 1)
            {
                string username = arguments.ElementAt(0);
                if (username.StartsWith("@"))
                {
                    username = username.Substring(1);
                }

                UserViewModel targetUser = ChannelSession.Services.User.GetUserByUsername(username, platform);
                if (targetUser != null)
                {
                    targetUser.Title = string.Join(" ", arguments.Skip(1));
                }
                else
                {
                    await ChannelSession.Services.Chat.SendMessage(username + " could not be found in chat");
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !settitle <USERNAME> <TITLE NAME>");
            }
        }
    }

    public class AddCommandPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public AddCommandPreMadeChatCommandModel() : base(MixItUp.Base.Resources.AddCommand, "addcommand", 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() >= 3)
            {
                string commandTrigger = arguments.ElementAt(0).ToLower();

                if (!CommandBase.IsValidCommandString(commandTrigger))
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: Command trigger contain an invalid character");
                    return;
                }

                foreach (PermissionsCommandBase command in ChannelSession.AllEnabledChatCommands)
                {
                    if (command.IsEnabled)
                    {
                        if (command.Commands.Contains(commandTrigger, StringComparer.InvariantCultureIgnoreCase))
                        {
                            await ChannelSession.Services.Chat.SendMessage("ERROR: There already exists an enabled, chat command that uses the command trigger you have specified");
                            return;
                        }
                    }
                }

                if (!int.TryParse(arguments.ElementAt(1), out int cooldown) || cooldown < 0)
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: Cooldown must be 0 or greater");
                    return;
                }

                StringBuilder commandTextBuilder = new StringBuilder();
                foreach (string arg in arguments.Skip(2))
                {
                    commandTextBuilder.Append(arg + " ");
                }

                string commandText = commandTextBuilder.ToString();
                commandText = commandText.Trim(new char[] { ' ', '\'', '\"' });

                // TODO
                //ChatCommand newCommand = new ChatCommand(commandTrigger, commandTrigger, new RequirementViewModel());
                //newCommand.Requirements.Cooldown.Amount = cooldown;
                //newCommand.Actions.Add(new ChatAction(commandText));
                //ChannelSession.Settings.ChatCommands.Add(newCommand);

                if (ChannelSession.Services.Chat != null)
                {
                    await ChannelSession.Services.Chat.SendMessage("Added New Command: !" + commandTrigger);

                    ChannelSession.Services.Chat.RebuildCommandTriggers();
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !addcommand <COMMAND TRIGGER, NO !> <COOLDOWN> <FULL COMMAND MESSAGE TEXT>");
            }
        }
    }

    public class UpdateCommandPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public UpdateCommandPreMadeChatCommandModel() : base(MixItUp.Base.Resources.UpdateCommand, new HashSet<string>() { "updatecommand", "editcommand" }, 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() >= 2)
            {
                string commandTrigger = arguments.ElementAt(0).ToLower();

                PermissionsCommandBase command = ChannelSession.AllEnabledChatCommands.FirstOrDefault(c => c.Commands.Contains(commandTrigger, StringComparer.InvariantCultureIgnoreCase));
                if (command == null)
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: Could not find any command with that trigger");
                    return;
                }

                if (!int.TryParse(arguments.ElementAt(1), out int cooldown) || cooldown < 0)
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: Cooldown must be 0 or greater");
                    return;
                }

                command.Requirements.Cooldown.Amount = cooldown;

                if (arguments.Count() > 2)
                {
                    StringBuilder commandTextBuilder = new StringBuilder();
                    foreach (string arg in arguments.Skip(2))
                    {
                        commandTextBuilder.Append(arg + " ");
                    }

                    string commandText = commandTextBuilder.ToString();
                    commandText = commandText.Trim(new char[] { ' ', '\'', '\"' });

                    command.Actions.Clear();
                    // TODO
                    //command.Actions.Add(new ChatAction(commandText));
                }

                if (ChannelSession.Services.Chat != null)
                {
                    await ChannelSession.Services.Chat.SendMessage("Updated Command: !" + commandTrigger);

                    ChannelSession.Services.Chat.RebuildCommandTriggers();
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !updatecommand <COMMAND TRIGGER, NO !> <COOLDOWN> [OPTIONAL FULL COMMAND MESSAGE TEXT]");
            }
        }
    }

    public class DisableCommandPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public DisableCommandPreMadeChatCommandModel() : base(MixItUp.Base.Resources.DisableCommand, "disablecommand", 5, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() == 1)
            {
                string commandTrigger = arguments.ElementAt(0).ToLower();

                PermissionsCommandBase command = ChannelSession.AllEnabledChatCommands.FirstOrDefault(c => c.Commands.Contains(commandTrigger, StringComparer.InvariantCultureIgnoreCase));
                if (command == null)
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: Could not find any command with that trigger");
                    return;
                }

                command.IsEnabled = false;

                if (ChannelSession.Services.Chat != null)
                {
                    await ChannelSession.Services.Chat.SendMessage("Disabled Command: !" + commandTrigger);

                    ChannelSession.Services.Chat.RebuildCommandTriggers();
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !disablecommand <COMMAND TRIGGER, NO !>");
            }
        }
    }

    public class StartGiveawayPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public StartGiveawayPreMadeChatCommandModel() : base(MixItUp.Base.Resources.StartGiveaway, "startgiveaway", 5, UserRoleEnum.Streamer) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments.Count() > 0)
            {
                string result = await ChannelSession.Services.GiveawayService.Start(string.Join(" ", arguments));
                if (!string.IsNullOrEmpty(result))
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: " + result);
                }
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !startgiveaway <GIVEAWAY ITEM>");
            }
        }
    }

    public class LinkMixerAccountPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public static Dictionary<Guid, Guid> LinkedAccounts = new Dictionary<Guid, Guid>();

        public LinkMixerAccountPreMadeChatCommandModel() : base("Link Mixer Account", "linkmixeraccount", 0, UserRoleEnum.User) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments != null && arguments.Count() == 1)
            {
                string mixerUsername = arguments.First().Replace("@", "");
#pragma warning disable CS0612 // Type or member is obsolete
                UserDataModel mixerUserData = ChannelSession.Settings.GetUserDataByUsername(StreamingPlatformTypeEnum.Mixer, mixerUsername);
#pragma warning restore CS0612 // Type or member is obsolete
                if (mixerUserData != null)
                {
                    LinkedAccounts[user.ID] = mixerUserData.ID;
                    await ChannelSession.Services.Chat.SendMessage($"@{user.Username} is attempting to link the Mixer account {mixerUserData.MixerUsername} to their {user.Platform} account. Mods can type \"!approvemixeraccount @<TWITCH USERNAME>\" in chat to approve this linking.");
                    return;
                }
                await ChannelSession.Services.Chat.SendMessage("There is no Mixer user data for that username");
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !linkmixeraccount <MIXER USERNAME>");
            }
        }
    }

    public class ApproveMixerAccountPreMadeChatCommandModel : PreMadeChatCommandModelBase
    {
        public ApproveMixerAccountPreMadeChatCommandModel() : base("Approve Mixer Account", "approvemixeraccount", 0, UserRoleEnum.Mod) { }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (arguments != null && arguments.Count() == 1)
            {
                UserViewModel targetUser = ChannelSession.Services.User.GetUserByUsername(arguments.First().Replace("@", ""), user.Platform);
                if (targetUser != null && LinkMixerAccountChatCommand.LinkedAccounts.ContainsKey(targetUser.ID))
                {
                    UserDataModel mixerUserData = ChannelSession.Settings.GetUserData(LinkMixerAccountChatCommand.LinkedAccounts[targetUser.ID]);
                    if (mixerUserData != null)
                    {
                        LinkMixerAccountChatCommand.LinkedAccounts.Remove(targetUser.ID);
                        targetUser.Data.MergeData(mixerUserData);

                        ChannelSession.Settings.UserData.Remove(mixerUserData.ID);

                        await ChannelSession.Services.Chat.SendMessage($"The user data from the account {mixerUserData.MixerUsername} on Mixer has been deleted and merged into @{targetUser.Username}.");
                        return;
                    }
                    await ChannelSession.Services.Chat.SendMessage("There is no Mixer user data for that username");
                    return;
                }
                await ChannelSession.Services.Chat.SendMessage("The specified Twitch user has not run the !linkmixeraccount command");
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage("Usage: !approvemixeraccount <TWITCH USERNAME>");
            }
        }
    }
}