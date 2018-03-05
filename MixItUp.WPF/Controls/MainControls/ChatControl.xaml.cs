﻿using MixItUp.Base;
using MixItUp.Base.MixerAPI;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using MixItUp.WPF.Controls.Chat;
using MixItUp.WPF.Util;
using MixItUp.WPF.Windows.PopOut;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for ChatControl.xaml
    /// </summary>
    public partial class ChatControl : MainControlBase
    {
        public static BitmapImage SubscriberBadgeBitmap { get; private set; }

        public ObservableCollection<ChatUserControl> UserControls = new ObservableCollection<ChatUserControl>();
        public ObservableCollection<ChatMessageControl> MessageControls = new ObservableCollection<ChatMessageControl>();

        private CancellationTokenSource backgroundThreadCancellationTokenSource = new CancellationTokenSource();

        private SemaphoreSlim userUpdateLock = new SemaphoreSlim(1);
        private SemaphoreSlim messageUpdateLock = new SemaphoreSlim(1);

        private int totalMessages = 0;
        private ScrollViewer chatListScrollViewer;
        private bool lockChatList = true;

        private Dictionary<string, int> fontSizes = new Dictionary<string, int>() { { "Normal", 13 }, { "Large", 16 }, { "X-Large", 24 }, };

        public ChatControl(bool isPopOut = false)
        {
            InitializeComponent();

            if (isPopOut)
            {
                this.PopOutChatButton.Visibility = Visibility.Collapsed;
            }

            if (!ChannelSession.Settings.IsStreamer)
            {
                this.DisableChatButton.Visibility = Visibility.Collapsed;
            }
        }

        protected override Task InitializeInternal()
        {
            this.Window.Closing += Window_Closing;
            GlobalEvents.OnChatFontSizeChanged += GlobalEvents_OnChatFontSizeChanged;

            ChannelSession.Constellation.OnFollowOccurred += Constellation_OnFollowOccurred;
            ChannelSession.Constellation.OnUnfollowOccurred += Constellation_OnUnfollowOccurred;
            ChannelSession.Constellation.OnHostedOccurred += Constellation_OnHostedOccurred;
            ChannelSession.Constellation.OnSubscribedOccurred += Constellation_OnSubscribedOccurred;
            ChannelSession.Constellation.OnResubscribedOccurred += Constellation_OnResubscribedOccurred;

            ChannelSession.Interactive.OnInteractiveControlUsed += Interactive_OnInteractiveControlUsed;

            if (ChannelSession.Settings.ChatFontSize == 0)
            {
                ChannelSession.Settings.ChatFontSize = this.fontSizes["Normal"];
            }

            this.ChatList.ItemsSource = this.MessageControls;
            this.UserList.ItemsSource = this.UserControls;

            this.FontSizeComboBox.ItemsSource = this.fontSizes.Keys;
            this.FontSizeComboBox.SelectedItem = this.fontSizes.FirstOrDefault(f => f.Value == ChannelSession.Settings.ChatFontSize).Key;
            this.ShowUserJoinLeaveToggleButton.IsChecked = ChannelSession.Settings.ChatShowUserJoinLeave;
            this.ShowEventAlertsToggleButton.IsChecked = ChannelSession.Settings.ChatShowEventAlerts;
            this.ShowInteractiveAlertsToggleButton.IsChecked = ChannelSession.Settings.ChatShowInteractiveAlerts;

            ChannelSession.Chat.OnMessageOccurred += ChatClient_OnMessageOccurred;
            ChannelSession.Chat.OnDeleteMessageOccurred += ChatClient_OnDeleteMessageOccurred;
            ChannelSession.Chat.OnPurgeMessageOccurred += ChatClient_OnPurgeMessageOccurred;
            ChannelSession.Chat.OnClearMessagesOccurred += Chat_OnClearMessagesOccurred;
            ChannelSession.Chat.OnUserJoinOccurred += ChatClient_OnUserJoinOccurred;
            ChannelSession.Chat.OnUserLeaveOccurred += ChatClient_OnUserLeaveOccurred;
            ChannelSession.Chat.OnUserUpdateOccurred += ChatClient_OnUserUpdateOccurred;

            if (ChannelSession.Channel.badge != null && ChannelSession.Channel.badge != null && !string.IsNullOrEmpty(ChannelSession.Channel.badge.url))
            {
                ChatControl.SubscriberBadgeBitmap = new BitmapImage();
                ChatControl.SubscriberBadgeBitmap.BeginInit();
                ChatControl.SubscriberBadgeBitmap.UriSource = new Uri(ChannelSession.Channel.badge.url, UriKind.Absolute);
                ChatControl.SubscriberBadgeBitmap.EndInit();
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => { await this.ChatRefreshBackground(); }, this.backgroundThreadCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            return Task.FromResult(0);
        }

        protected override Task OnVisibilityChanged()
        {
            if (ChannelSession.Chat.BotClient != null)
            {
                this.SendChatAsComboBox.ItemsSource = new List<string>() { "Streamer", "Bot" };
                this.SendChatAsComboBox.SelectedIndex = 1;
            }
            else
            {
                this.SendChatAsComboBox.ItemsSource = new List<string>() { "Streamer" };
                this.SendChatAsComboBox.SelectedIndex = 0;
            }

            return Task.FromResult(0);
        }

        private async Task ChatRefreshBackground()
        {
            await BackgroundTaskWrapper.RunBackgroundTask(this.backgroundThreadCancellationTokenSource, async (tokenSource) =>
            {
                tokenSource.Token.ThrowIfCancellationRequested();

                await this.Dispatcher.InvokeAsync<Task>(async () =>
                {
                    await this.RefreshUserList();

                    this.ViewersCountTextBlock.Text = ChannelSession.Channel.viewersCurrent.ToString();
                    this.ChatCountTextBlock.Text = ChannelSession.Chat.ChatUsers.Count.ToString();
                });

                tokenSource.Token.ThrowIfCancellationRequested();

                await Task.Delay(1000 * 30);
            });
        }

        #region Chat Update Methods

        private async Task RefreshUserList()
        {
            await userUpdateLock.WaitAsync();

            this.UserControls.Clear();
            var orderedUsers = ChannelSession.Chat.ChatUsers.Values.ToList().OrderByDescending(u => u.PrimarySortableRole).ThenBy(u => u.UserName).ToList();
            foreach (UserViewModel user in orderedUsers)
            {
                this.UserControls.Add(new ChatUserControl(user));
            }

            userUpdateLock.Release();
        }

        private async Task AddMessage(ChatMessageViewModel message)
        {
            await messageUpdateLock.WaitAsync();

            ChatMessageControl messageControl = new ChatMessageControl(message);
            this.MessageControls.Add(messageControl);
            this.totalMessages++;

            while (this.MessageControls.Count > ChannelSession.Settings.MaxMessagesInChat)
            {
                this.MessageControls.RemoveAt(0);
            }

            messageUpdateLock.Release();
        }

        private async Task ShowUserDialog(UserViewModel user)
        {
            UserDialogResult result = await MessageBoxHelper.ShowUserDialog(user);

            if (result == UserDialogResult.Purge)
            {
                await ChannelSession.Chat.PurgeUser(user.UserName);
            }
            else if (result == UserDialogResult.Timeout1)
            {
                await ChannelSession.Chat.TimeoutUser(user.UserName, 60);
            }
            else if (result == UserDialogResult.Timeout5)
            {
                await ChannelSession.Chat.TimeoutUser(user.UserName, 300);
            }
            else if (result == UserDialogResult.Ban)
            {
                if (await MessageBoxHelper.ShowConfirmationDialog(string.Format("This will ban the user {0} from this channel. Are you sure?", user.UserName)))
                {
                    await ChannelSession.Connection.AddUserRoles(ChannelSession.Channel, user.GetModel(), new List<UserRole>() { UserRole.Banned });
                }
            }
            else if (result == UserDialogResult.Unban)
            {
                await ChannelSession.Connection.RemoveUserRoles(ChannelSession.Channel, user.GetModel(), new List<UserRole>() { UserRole.Banned });
            }
        }

        #endregion Chat Update Methods

        #region UI Events

        private void PopOutChatButton_Click(object sender, RoutedEventArgs e)
        {
            PopOutWindow window = new PopOutWindow("Chat", new ChatControl(isPopOut: true));
            window.Show();
        }

        private async void ChatClearMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (await MessageBoxHelper.ShowConfirmationDialog("This will clear all Chat for the stream. Are you sure?"))
            {
                await ChannelSession.Chat.ClearMessages();
                this.MessageControls.Clear();
            }
        }

        private async void DisableChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (await MessageBoxHelper.ShowConfirmationDialog("This will disable chat for all users. Are you sure?"))
            {
                ChannelSession.Chat.DisableChat = true;
                this.DisableChatButton.Visibility = Visibility.Collapsed;
                this.EnableChatButton.Visibility = Visibility.Visible;
            }
        }

        private void EnableChatButton_Click(object sender, RoutedEventArgs e)
        {
            ChannelSession.Chat.DisableChat = false;
            this.DisableChatButton.Visibility = Visibility.Visible;
            this.EnableChatButton.Visibility = Visibility.Collapsed;
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.FontSizeComboBox.SelectedIndex >= 0)
            {
                string name = (string)this.FontSizeComboBox.SelectedItem;
                ChannelSession.Settings.ChatFontSize = this.fontSizes[name];
                GlobalEvents.ChatFontSizeChanged();
            }
        }

        private void ShowUserJoinLeaveToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ChannelSession.Settings.ChatShowUserJoinLeave = this.ShowUserJoinLeaveToggleButton.IsChecked.GetValueOrDefault();
        }

        private void ShowEventAlertsToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ChannelSession.Settings.ChatShowEventAlerts = this.ShowEventAlertsToggleButton.IsChecked.GetValueOrDefault();
        }

        private void ShowInteractiveAlertsToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ChannelSession.Settings.ChatShowInteractiveAlerts = this.ShowInteractiveAlertsToggleButton.IsChecked.GetValueOrDefault();
        }

        private void GlobalEvents_OnChatFontSizeChanged(object sender, EventArgs e)
        {
            this.FontSizeComboBox.SelectedItem = this.fontSizes.FirstOrDefault(f => f.Value == ChannelSession.Settings.ChatFontSize).Key;
            foreach (ChatMessageControl control in this.MessageControls)
            {
                control.UpdateSizing();
            }
        }

        private void ChatList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (this.chatListScrollViewer == null)
            {
                this.chatListScrollViewer = (ScrollViewer)e.OriginalSource;
                this.chatListScrollViewer.IsEnabled = !this.lockChatList;
            }
        }

        private void ChatLockButton_Click(object sender, RoutedEventArgs e)
        {
            this.lockChatList = !this.lockChatList;
            this.chatListScrollViewer.IsEnabled = !this.lockChatList;
            this.ChatLockButtonIcon.Kind = (this.lockChatList) ? MaterialDesignThemes.Wpf.PackIconKind.LockOutline : MaterialDesignThemes.Wpf.PackIconKind.LockOpenOutline;
            if (this.lockChatList)
            {
                this.chatListScrollViewer.ScrollToBottom();
            }
        }

        private void ChatList_LayoutUpdated(object sender, EventArgs e)
        {
            if (this.lockChatList && this.chatListScrollViewer != null)
            {
                this.chatListScrollViewer.ScrollToBottom();
            }
        }

        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.SendChatMessageButton_Click(this, new RoutedEventArgs());
                this.ChatMessageTextBox.Focus();
            }
        }

        private async void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(this.ChatMessageTextBox.Text))
            {
                string message = this.ChatMessageTextBox.Text;
                this.ChatMessageTextBox.Text = string.Empty;

                Match whisperRegexMatch = ChatMessageViewModel.WhisperRegex.Match(message);
                if (whisperRegexMatch != null && whisperRegexMatch.Success)
                {
                    message = message.Substring(whisperRegexMatch.Value.Length);

                    Match usernNameMatch = ChatMessageViewModel.UserNameTagRegex.Match(whisperRegexMatch.Value);
                    string username = usernNameMatch.Value;
                    username = username.Trim();
                    username = username.Replace("@", "");

                    await this.Window.RunAsyncOperation((Func<Task>)(async () =>
                    {
                        await ChannelSession.Chat.Whisper(username, message, (this.SendChatAsComboBox.SelectedIndex == 0));
                    }));
                }
                else
                {
                    await this.Window.RunAsyncOperation((Func<Task>)(async () =>
                    {
                        await ChannelSession.Chat.SendMessage(message, (this.SendChatAsComboBox.SelectedIndex == 0));
                    }));
                }
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await ChannelSession.Chat.Disconnect();
        }

        private async void UserList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.UserList.SelectedIndex >= 0)
            {
                ChatUserControl userControl = (ChatUserControl)this.UserList.SelectedItem;
                this.UserList.SelectedIndex = -1;
                await this.ShowUserDialog(userControl.User);
            }
        }

        private void MessageCopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                Clipboard.SetText(control.Message.Message);
            }
        }

        private async void MessageDeleteMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                if (!control.Message.IsWhisper)
                {
                    await ChannelSession.Chat.DeleteMessage(control.Message.ID);
                }
            }
        }

        private async void MessageUserInformationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                await this.ShowUserDialog(control.Message.User);
            }
        }

        #endregion UI Events

        #region Chat Event Handlers

        private async void ChatClient_OnMessageOccurred(object sender, ChatMessageViewModel message)
        {
            await this.AddMessage(message);
        }

        private void ChatClient_OnDeleteMessageOccurred(object sender, Guid messageID)
        {
            ChatMessageControl message = this.MessageControls.FirstOrDefault(msg => msg.Message.ID.Equals(messageID));
            if (message != null)
            {
                message.DeleteMessage();
            }
        }

        private void ChatClient_OnPurgeMessageOccurred(object sender, uint userID)
        {
            IEnumerable<ChatMessageControl> userMessages = this.MessageControls.Where(msg => msg.Message.User != null && msg.Message.User.ID.Equals(userID));
            if (userMessages != null)
            {
                foreach (ChatMessageControl message in userMessages)
                {
                    message.DeleteMessage();
                }
            }
        }

        private void Chat_OnClearMessagesOccurred(object sender, EventArgs e)
        {
            this.MessageControls.Clear();
        }

        private async void ChatClient_OnUserJoinOccurred(object sender, UserViewModel user)
        {
            await this.RefreshUserList();
            if (ChannelSession.Settings.ChatShowUserJoinLeave)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Joined Chat  ---", user.UserName)));
            }
        }

        private async void ChatClient_OnUserUpdateOccurred(object sender, UserViewModel user)
        {
            await this.RefreshUserList();
        }

        private async void ChatClient_OnUserLeaveOccurred(object sender, UserViewModel user)
        {
            await this.RefreshUserList();
            if (ChannelSession.Settings.ChatShowUserJoinLeave)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Left Chat  ---", user.UserName)));
            }
        }

        private async void Constellation_OnFollowOccurred(object sender, UserViewModel e)
        {
            if (ChannelSession.Settings.ChatShowEventAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Followed  ---", e.UserName)));
            }
        }

        private async void Constellation_OnUnfollowOccurred(object sender, UserViewModel e)
        {
            if (ChannelSession.Settings.ChatShowEventAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Unfollowed  ---", e.UserName)));
            }
        }

        private async void Constellation_OnHostedOccurred(object sender, Tuple<UserViewModel, int> e)
        {
            if (ChannelSession.Settings.ChatShowEventAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Hosted With {1} Viewers  ---", e.Item1.UserName, e.Item2)));
            }
        }

        private async void Constellation_OnSubscribedOccurred(object sender, UserViewModel e)
        {
            if (ChannelSession.Settings.ChatShowEventAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Subscribed  ---", e.UserName)));
            }
        }

        private async void Constellation_OnResubscribedOccurred(object sender, Tuple<UserViewModel, int> e)
        {
            if (ChannelSession.Settings.ChatShowEventAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Re-Subscribed For {1} Months  ---", e.Item1.UserName, e.Item2)));
            }
        }

        private async void Interactive_OnInteractiveControlUsed(object sender, Tuple<UserViewModel, InteractiveConnectedControlCommand> e)
        {
            if (ChannelSession.Settings.ChatShowInteractiveAlerts)
            {
                await this.AddMessage(new ChatMessageViewModel(string.Format("---  {0} Used The \"{1}\" Interactive Control  ---", e.Item1.UserName, e.Item2.Name)));
            }
        }

        #endregion Chat Event Handlers
    }
}
