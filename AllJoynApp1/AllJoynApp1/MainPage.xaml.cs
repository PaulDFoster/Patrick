using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.AllJoyn;
using com.microsoft.ZWaveBridge.SwitchBinary.Switch;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AllJoynApp1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        SwitchConsumer switchConsumer;
        bool lightStatus = false;

        public MainPage()
        {
            this.InitializeComponent();
            AllJoynBusAttachment switchBusAttachment = new AllJoynBusAttachment();
            SwitchWatcher switchWatcher = new SwitchWatcher(switchBusAttachment);
            switchWatcher.Added += SwitchWatcher_Added;
            switchWatcher.Start();
        }

        private async void SwitchWatcher_Added(SwitchWatcher sender, AllJoynServiceInfo args)
        {
            
            SwitchJoinSessionResult joinSessionResult = await SwitchConsumer.JoinSessionAsync(args, sender);
            if (joinSessionResult.Status == AllJoynStatus.Ok)
            {
                switchConsumer = joinSessionResult.Consumer;
                btnLightSwitch.IsEnabled = true;
            }
        }

        private async void btnLightSwitch_Click(object sender, RoutedEventArgs e)
        {
            lightStatus = !lightStatus;
            await switchConsumer.SetValueAsync(lightStatus);
        }
    }
}
