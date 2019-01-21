﻿using MixItUp.Base;
using MixItUp.Base.Commands;
using MixItUp.WPF.Util;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Command
{
    /// <summary>
    /// Interaction logic for TimerCommandDetailsControl.xaml
    /// </summary>
    public partial class TimerCommandDetailsControl : CommandDetailsControlBase
    {
        private TimerCommand command;

        public TimerCommandDetailsControl(TimerCommand command)
        {
            this.command = command;
            InitializeComponent();
        }

        public TimerCommandDetailsControl() : this(null) { }

        public override Task Initialize()
        {
            this.CommandGroupComboBox.ItemsSource = ChannelSession.Settings.CommandGroups.Keys;

            if (this.command != null)
            {
                this.NameTextBox.Text = this.command.Name;
                this.CommandGroupComboBox.Text = this.command.GroupName;
                this.UnlockedControl.Unlocked = this.command.Unlocked;
            }

            return Task.FromResult(0);
        }

        public override async Task<bool> Validate()
        {
            if (string.IsNullOrEmpty(this.NameTextBox.Text))
            {
                await MessageBoxHelper.ShowMessageDialog("Name is missing");
                return false;
            }

            return true;
        }

        public override CommandBase GetExistingCommand() { return this.command; }

        public override async Task<CommandBase> GetNewCommand()
        {
            if (await this.Validate())
            {
                if (this.command == null)
                {
                    this.command = new TimerCommand(this.NameTextBox.Text);
                    ChannelSession.Settings.TimerCommands.Add(this.command);
                }
                else
                {
                    this.command.Name = this.NameTextBox.Text;
                }
                this.command.Unlocked = this.UnlockedControl.Unlocked;

                this.command.GroupName = this.CommandGroupComboBox.Text;
                if (!string.IsNullOrEmpty(this.CommandGroupComboBox.Text) && !ChannelSession.Settings.CommandGroups.ContainsKey(this.CommandGroupComboBox.Text))
                {
                    ChannelSession.Settings.CommandGroups[this.CommandGroupComboBox.Text] = new CommandGroupSettings();
                }

                return this.command;
            }
            return null;
        }
    }
}
