﻿using MixItUp.Base.Actions;
using MixItUp.Base.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.WPF.Controls.Command;
using MixItUp.WPF.Windows.Command;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Games
{
    public abstract class GameEditorControlBase : LoadingControlBase
    {
        public abstract Task<bool> Validate();

        public abstract void SaveGameCommand();

        protected void SubCommandButtonsControl_EditClicked(object sender, RoutedEventArgs e)
        {
            CommandButtonsControl commandButtonsControl = (CommandButtonsControl)sender;
            CustomCommand command = commandButtonsControl.GetCommandFromCommandButtons<CustomCommand>(sender);
            if (command != null)
            {
                CommandWindow window = new CommandWindow(new CustomCommandDetailsControl(command));
                window.Show();
            }
        }

        protected CustomCommand CreateBasicChatCommand(string message)
        {
            CustomCommand command = new CustomCommand("Game Sub-Command");
            command.Actions.Add(new ChatAction(message, sendAsStreamer: false));
            return command;
        }

        protected CustomCommand CreateBasic2ChatCommand(string message1, string message2)
        {
            CustomCommand command = new CustomCommand("Game Sub-Command");
            command.Actions.Add(new ChatAction(message1));
            command.Actions.Add(new ChatAction(message2));
            return command;
        }

        protected CustomCommand CreateBasicCurrencyCommand(string message, CurrencyModel currency, int amount)
        {
            CustomCommand command = this.CreateBasicChatCommand(message);
            command.Actions.Add(new CurrencyAction(currency, CurrencyActionTypeEnum.AddToUser, amount.ToString()));
            return command;
        }
    }
}
