﻿using MixItUp.Base;
using MixItUp.Base.Util;
using MixItUp.WPF.Windows;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MixItUp.WPF.Controls.MainControls
{
    public abstract class MainControlBase : UserControl
    {
        private readonly static string[] IgnoredPages = new string[]
        {
            "MixItUp.WPF.Controls.MainControls.MainMenuControl",
            "MixItUp.WPF.Controls.Settings.MainSettingsContainerControl",
            "MixItUp.WPF.Controls.Settings.GeneralSettingsControl",
        };

        public LoadingWindowBase Window { get; private set; }

        public MainControlBase() { this.IsVisibleChanged += MainControlBase_IsVisibleChanged; }

        public async Task Initialize(LoadingWindowBase window)
        {
            this.Window = window;
            await this.Window.RunAsyncOperation(async () =>
            {
                await this.InitializeInternal();
                await this.OnVisibilityChanged();
            });
        }

        public void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessHelper.LaunchLink(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        protected virtual Task InitializeInternal() { return Task.FromResult(0); }

        protected virtual Task OnVisibilityChanged() { return Task.FromResult(0); }

        private async void MainControlBase_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }

            if ((bool)e.NewValue)
            {
                string typeName = this.GetType().FullName;
                if (!IgnoredPages.Contains(typeName) && ChannelSession.Services?.Telemetry != null)
                {
                    ChannelSession.Services.Telemetry.TrackPageView(typeName);
                }
            }

            if (this.Window != null)
            {
                await this.Window.RunAsyncOperation(async () =>
                {
                    await this.OnVisibilityChanged();
                });
            }
        }
    }
}
