﻿using MixItUp.Base.ViewModel.Controls.Settings.Generic;
using MixItUp.Base.ViewModels;

namespace MixItUp.Base.ViewModel.Controls.Settings
{
    public class AlertsSettingsControlViewModel : UIViewModelBase
    {
        public GenericToggleSettingsOptionControlViewModel OnlyShowAlertsInDashboard { get; set; }

        public GenericColorComboBoxSettingsOptionControlViewModel UserJoinLeave { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Follow { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Host { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Raid { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Sub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel GiftedSub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel MassGiftedSub { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel BitsCheered { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel ChannelPoints { get; set; }
        public GenericColorComboBoxSettingsOptionControlViewModel Moderation { get; set; }

        public AlertsSettingsControlViewModel()
        {
            this.OnlyShowAlertsInDashboard = new GenericToggleSettingsOptionControlViewModel(MixItUp.Base.Resources.OnlyShowAlertsInDashboard, ChannelSession.Settings.OnlyShowAlertsInDashboard, (value) => { ChannelSession.Settings.OnlyShowAlertsInDashboard = value; });

            this.UserJoinLeave = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowUserJoinLeave, ChannelSession.Settings.AlertUserJoinLeaveColor, (value) => { ChannelSession.Settings.AlertUserJoinLeaveColor = value; });
            this.Follow = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowFollows, ChannelSession.Settings.AlertFollowColor, (value) => { ChannelSession.Settings.AlertFollowColor = value; });
            this.Host = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowHosts, ChannelSession.Settings.AlertHostColor, (value) => { ChannelSession.Settings.AlertHostColor = value; });
            this.Raid = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowRaids, ChannelSession.Settings.AlertRaidColor, (value) => { ChannelSession.Settings.AlertRaidColor = value; });
            this.Sub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowSubsResubs, ChannelSession.Settings.AlertSubColor, (value) => { ChannelSession.Settings.AlertSubColor = value; });
            this.GiftedSub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowGiftedSubs, ChannelSession.Settings.AlertGiftedSubColor, (value) => { ChannelSession.Settings.AlertGiftedSubColor = value; });
            this.MassGiftedSub = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowMassGiftedSubs, ChannelSession.Settings.AlertMassGiftedSubColor, (value) => { ChannelSession.Settings.AlertMassGiftedSubColor = value; });
            this.BitsCheered = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowBitsCheered, ChannelSession.Settings.AlertBitsCheeredColor, (value) => { ChannelSession.Settings.AlertBitsCheeredColor = value; });
            this.ChannelPoints = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowChannelPoints, ChannelSession.Settings.AlertChannelPointsColor, (value) => { ChannelSession.Settings.AlertChannelPointsColor = value; });
            this.Moderation = new GenericToggleColorComboBoxSettingsControlViewModel(MixItUp.Base.Resources.ShowModeration, ChannelSession.Settings.AlertModerationColor, (value) => { ChannelSession.Settings.AlertModerationColor = value; });
        }
    }
}
