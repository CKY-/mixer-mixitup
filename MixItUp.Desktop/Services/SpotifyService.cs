﻿using Mixer.Base;
using Mixer.Base.Model.OAuth;
using Mixer.Base.Web;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Desktop.Services
{
    public class SpotifyService : OAuthServiceBase, ISpotifyService
    {
        private const string BaseAddress = "https://api.spotify.com/v1/";

        private const string ClientID = "94c9f9c67c864ae9a0f9f8f5bdf3e000";
        private const string ClientSecret = "42d76a67bdfe4dd598ec4a0e9b524e7e";
        private const string StateKey = "V21C2J2RWE51CYSM";
        private const string AuthorizationUrl = "https://accounts.spotify.com/authorize?client_id={0}&redirect_uri=http://localhost:8919/&response_type=code&scope=playlist-read-private+playlist-modify-public+playlist-read-collaborative+user-top-read+user-read-recently-played+user-library-read+user-read-currently-playing+user-modify-playback-state+user-read-playback-state+streaming&state={1}";

        private const string MixItUpPlaylistName = "Mix It Up Request Playlist";
        private const string MixItUpPlaylistDescription = "This playlist contains songs that are requested by users through Mix It Up";

        public SpotifyUserProfile Profile { get; private set; }
        public SpotifyPlaylist Playlist { get; private set; }

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public SpotifyService() : base(SpotifyService.BaseAddress) { }

        public SpotifyService(OAuthTokenModel token) : base(SpotifyService.BaseAddress, token) { }

        public async Task<bool> Connect()
        {
            if (this.token != null)
            {
                try
                {
                    await this.RefreshOAuthToken();

                    await this.InitializeInternal();

                    return true;
                }
                catch (Exception ex) { Logger.Log(ex); }
            }

            string authorizationCode = await this.ConnectViaOAuthRedirect(string.Format(SpotifyService.AuthorizationUrl, SpotifyService.ClientID, SpotifyService.StateKey));
            if (!string.IsNullOrEmpty(authorizationCode))
            {
                var body = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("redirect_uri", MixerConnection.DEFAULT_OAUTH_LOCALHOST_URL),
                    new KeyValuePair<string, string>("code", authorizationCode),
                };

                string authorizationValue = string.Format("{0}:{1}", SpotifyService.ClientID, SpotifyService.ClientSecret);
                byte[] authorizationBytes = System.Text.Encoding.UTF8.GetBytes(authorizationValue);
                authorizationValue = Convert.ToBase64String(authorizationBytes);

                using (HttpClientWrapper client = await this.GetHttpClient())
                {
                    client.BaseAddress = new Uri("https://accounts.spotify.com/api/");
                    client.DefaultRequestHeaders.Add("Authorization", "Basic " + authorizationValue);
                    using (var content = new FormUrlEncodedContent(body))
                    {
                        content.Headers.Clear();
                        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                        HttpResponseMessage response = await client.PostAsync("token", content);
                        this.token = await this.ProcessResponse<OAuthTokenModel>(response);
                    }
                }

                if (this.token != null)
                {
                    this.token.authorizationCode = authorizationCode;

                    await this.InitializeInternal();

                    return true;
                }
            }

            return false;
        }

        public Task Disconnect()
        {
            this.token = null;
            this.cancellationTokenSource.Cancel();
            return Task.FromResult(0);
        }

        public async Task<SpotifyUserProfile> GetCurrentProfile()
        {
            try
            {
                JObject result = await this.GetJObjectAsync("me");
                return new SpotifyUserProfile(result);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task<SpotifyUserProfile> GetProfile(string username)
        {
            try
            {
                JObject result = await this.GetJObjectAsync("users/" + username);
                return new SpotifyUserProfile(result);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task<IEnumerable<SpotifySong>> SearchSongs(string songName)
        {
            List<SpotifySong> songs = new List<SpotifySong>();
            try
            {
                JObject result = await this.GetJObjectAsync(string.Format("search?q={0}&type=track", this.EncodeString(songName)));
                if (result != null)
                {
                    JArray results = (JArray)result["tracks"]["items"];
                    foreach (JToken songResult in results)
                    {
                        songs.Add(new SpotifySong((JObject)songResult));
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return songs;
        }


        public async Task<SpotifySong> GetSong(string songID)
        {
            try
            {
                JObject result = await this.GetJObjectAsync(string.Format("tracks/" + songID));
                return new SpotifySong(result);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task<IEnumerable<SpotifyPlaylist>> GetCurrentPlaylists()
        {
            List<SpotifyPlaylist> playlists = new List<SpotifyPlaylist>();
            try
            {
                foreach (JObject playlist in await this.GetPagedResult("me/playlists"))
                {
                    playlists.Add(new SpotifyPlaylist(playlist));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return playlists;
        }

        public async Task<SpotifyPlaylist> GetPlaylist(string playlistID)
        {
            try
            {
                JObject result = await this.GetJObjectAsync(string.Format("users/{0}/playlists/{1}", this.Profile.ID, playlistID));
                return new SpotifyPlaylist(result);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task<IEnumerable<SpotifySong>> GetPlaylistSongs(string playlistID)
        {
            List<SpotifySong> results = new List<SpotifySong>();
            try
            {
                foreach (JObject song in await this.GetPagedResult(string.Format("users/{0}/playlists/{1}/tracks", this.Profile.ID, playlistID)))
                {
                    results.Add(new SpotifySong(song));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return results;
        }

        public async Task<SpotifyPlaylist> CreatePlaylist(string name, string description)
        {
            try
            {
                JObject payload = new JObject();
                payload["name"] = name;
                payload["description"] = description;
                payload["public"] = "true";

                HttpResponseMessage response = await this.PostAsync(string.Format("users/{0}/playlists", this.Profile.ID), this.CreateContentFromObject(payload));
                string responseString = await response.Content.ReadAsStringAsync();
                JObject jobj = JObject.Parse(responseString);
                return new SpotifyPlaylist(jobj);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task AddSongToPlaylist(string songID)
        {
            try
            {
                HttpResponseMessage response = await this.PostAsync(string.Format("users/{0}/playlists/{1}/tracks?uris=spotify:track:" + songID, this.Profile.ID, this.Playlist.ID), null);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public async Task RemoveSongToPlaylist(string songID)
        {
            await this.RemoveSongToPlaylist(new List<string>() { songID });
        }

        public async Task RemoveSongToPlaylist(IEnumerable<string> songID)
        {
            try
            {
                for (int i = 0; i < songID.Count(); i += 50)
                {
                    JArray songsToDeleteArray = new JArray();
                    foreach (string songToDelete in songID.Skip(i).Take(50))
                    {
                        JObject songPayload = new JObject();
                        songPayload["uri"] = "spotify:track:" + songToDelete;
                        songsToDeleteArray.Add(songPayload);
                    }

                    JObject payload = new JObject();
                    payload["tracks"] = songsToDeleteArray;

                    using (HttpClientWrapper client = await this.GetHttpClient())
                    {
                        HttpMethod method = new HttpMethod("DELETE");
                        HttpRequestMessage request = new HttpRequestMessage(method, string.Format("users/{0}/playlists/{1}/tracks", this.Profile.ID, this.Playlist.ID))
                            { Content = this.CreateContentFromObject(payload) };
                        HttpResponseMessage response = await client.SendAsync(request);
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public async Task<SpotifyCurrentlyPlaying> GetCurrentlyPlaying()
        {
            try
            {
                JObject result = await this.GetJObjectAsync("me/player/currently-playing");
                return new SpotifyCurrentlyPlaying(result);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        public async Task PlayCurrentlyPlaying()
        {
            try
            {
                await this.PutAsync("me/player/play", null);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public async Task PauseCurrentlyPlaying()
        {
            try
            {
                await this.PutAsync("me/player/pause", null);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public async Task NextCurrentlyPlaying()
        {
            try
            {
                await this.PutAsync("me/player/next", null);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public async Task PreviousCurrentlyPlaying()
        {
            try
            {
                await this.PostAsync("me/player/seek", null);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        protected override async Task RefreshOAuthToken()
        {
            if (this.token != null)
            {
                JObject payload = new JObject();
                payload["grant_type"] = "refresh_token";
                payload["client_id"] = SpotifyService.ClientID;
                payload["client_secret"] = SpotifyService.ClientSecret;
                payload["refresh_token"] = this.token.refreshToken;
                payload["redirect_uri"] = MixerConnection.DEFAULT_OAUTH_LOCALHOST_URL;

                this.token = await this.PostAsync<OAuthTokenModel>("token", this.CreateContentFromObject(payload));
            }
        }

        private async Task InitializeInternal()
        {
            this.Profile = await this.GetCurrentProfile();

            foreach (SpotifyPlaylist playlist in await this.GetCurrentPlaylists())
            {
                if (playlist.Name.Equals(SpotifyService.MixItUpPlaylistName))
                {
                    this.Playlist = playlist;
                    break;
                }
            }

            if (this.Playlist == null)
            {
                this.Playlist = await this.CreatePlaylist(SpotifyService.MixItUpPlaylistName, SpotifyService.MixItUpPlaylistDescription);
            }
        }

        private async Task<IEnumerable<JObject>> GetPagedResult(string endpointURL)
        {
            List<JObject> results = new List<JObject>();

            int offset = 0;
            int total = 1;
            while (offset < total)
            {
                JObject result = await this.GetJObjectAsync(endpointURL + "?offset=" + offset);
                if (result != null)
                {
                    offset += 20;
                    total = int.Parse(result["total"].ToString());

                    JArray arrayResults = (JArray)result["items"];
                    foreach (JToken arrayResult in arrayResults)
                    {
                        results.Add((JObject)arrayResult);
                    }
                }
            }

            return results;
        }
    }
}
