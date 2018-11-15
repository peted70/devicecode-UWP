using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MSALUWPApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        SignInScript _signIn = new SignInScript();

        public ObservableCollection<RemoteSystem> Remotes { get; set; } = new ObservableCollection<RemoteSystem>();

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void DeviceFlowClick(object sender, RoutedEventArgs e)
        {
            var res = await _signIn.SignInWithDeviceCodeAsync(OnDeviceCodeFlowAsync, OnDeviceCodeStatusAsync);

            if (!string.IsNullOrEmpty(res.err))
            {
                DeviceFlowStatusMessage.Text = res.err;
            }
            else
            {
                DeviceFlowStatusMessage.Text += "\n" + res.res.AccessToken;
            }
        }

        private async Task OnDeviceCodeStatusAsync(string msg)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    DeviceFlowStatusMessage.Text += "\n" + msg;
                });
        }

        async Task StartRemoteSystemDetectionAsync()
        {
            var result = await RemoteSystem.RequestAccessAsync();

            if (result == RemoteSystemAccessStatus.Allowed)
            {
                var filters = new List<IRemoteSystemFilter>() { };
                //filters.Add()
                var remoteWatcher = RemoteSystem.CreateWatcher(filters);
                remoteWatcher.RemoteSystemAdded += async (s, e) =>
                {
                    //if (!e.RemoteSystem.IsAvailableByProximity && !e.RemoteSystem.IsAvailableBySpatialProximity)
                    //    return;

                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                    () =>
                                    {
                                        Remotes.Add(e.RemoteSystem);
                                    });
                };
                remoteWatcher.Start();
            }
        }

        public async Task AddDeviceCodeFlowStatusMessage(string msg)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    DeviceFlowStatusMessage.Text += "\n" + msg;
                });
        }

        private async Task OnDeviceCodeFlowAsync(DeviceCodeResult deviceCode)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {

                    await StartRemoteSystemDetectionAsync();
                    ///
                    /// Launch a URI locally and copy the device code to the clipboard
                    //await LaunchDeviceCodeUri(deviceCode);
                });
        }

        private async Task LaunchDeviceCodeUri(DeviceCodeResult deviceCode)
        {
            DeviceFlowStatusMessage.Text = deviceCode.Message;
            var uri = new Uri(deviceCode.VerificationUrl);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);

            if (success)
            {
                // URI launched
                var dataPackage = new DataPackage();
                dataPackage.SetText(deviceCode.UserCode);
                Clipboard.SetContent(dataPackage);
            }
            else
            {
                // URI launch failed
            }
        }

        private async void SignInClick(object sender, RoutedEventArgs e)
        {
            await _signIn.SignInUserFlowAsync(OnTokenUserFlow, OnSignInStatusAsync);
        }

        private async Task OnTokenUserFlow(AuthenticationResult arg)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    DelegatedFlowStatusMessage.Text += "\n" + arg.AccessToken;
                });
            return;
        }

        private async Task OnSignInStatusAsync(string msg)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    DelegatedFlowStatusMessage.Text += "\n" + msg;
                });
        }

        private async void RemoteActivated(object sender, RoutedEventArgs e)
        {
            var fw = sender as FrameworkElement;
            if (fw == null)
                return;
            var remote = fw.DataContext as RemoteSystem;
            if (remote == null)
                return;


            var res = await RemoteSystem.RequestAccessAsync();
            if (res != RemoteSystemAccessStatus.Allowed)
                return;

            bool isRemoteSystemLaunchUriCapable = await remote.GetCapabilitySupportedAsync(KnownRemoteSystemCapabilities.LaunchUri);
            bool isRemoteSystemAppServiceCapable = await remote.GetCapabilitySupportedAsync(KnownRemoteSystemCapabilities.AppService);
            bool isRemoteSystemRemoteSessionCapable = await remote.GetCapabilitySupportedAsync(KnownRemoteSystemCapabilities.RemoteSession);
            bool isRemoteSystemSpatialEntityCapable = await remote.GetCapabilitySupportedAsync(KnownRemoteSystemCapabilities.SpatialEntity);

            var rscr = new RemoteSystemConnectionRequest(remote);
            var uri = new Uri("http://www.google.co.uk");
            var status = await RemoteLauncher.LaunchUriAsync(rscr, uri);
        }
    }
}
