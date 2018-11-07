using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
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

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void DeviceFlowClick(object sender, RoutedEventArgs e)
        {
            var res = await _signIn.SignInAsync(true, OnDeviceCodeFlowAsync, OnStatusAsync);

            if (!string.IsNullOrEmpty(res.err))
            {
                DeviceFlowStatusMessage.Text = res.err;
            }
            else
            {
                DeviceFlowStatusMessage.Text += "\n" + res.res.AccessToken;
            }
        }

        private async Task OnStatusAsync(string msg)
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
                });
        }

        private async void SignInClick(object sender, RoutedEventArgs e)
        {
            await _signIn.SignInAsync(false);
        }
    }
}
