using System.Collections.Generic;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Storage;
using System.Threading;
using Newtonsoft.Json;

#if !UNITY_EDITOR && UNITY_WSA
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Storage;
#endif

public class SignInScript
{
    public SignInScript()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        //Debug.Break();
        _userId = localSettings.Values["UserId"] as string;
        _scopes = new List<string>() { "User.Read", "Mail.Read" };
        _client = new PublicClientApplication("e90a5e05-a177-468a-9f6e-eee32b946f86");
    }

    public class AuthResult
    {
        public AuthenticationResult res;
        public string err;
    }

    IEnumerable<string> _scopes;
    PublicClientApplication _client;
    string _userId;

    public async void Start()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        _userId = localSettings.Values["UserId"] as string;
        _scopes = new List<string>() { "User.Read", "Mail.Read" };
        _client = new PublicClientApplication("e90a5e05-a177-468a-9f6e-eee32b946f86");

        await SignInUserFlowAsync();
    }

    public async Task<AuthResult> AcquireTokenAsync(IPublicClientApplication app,
                                                     IEnumerable<string> scopes,
                                                     string usrId,
                                                     Func<AuthenticationResult, Task> callback)
    {
        var usr = !string.IsNullOrEmpty(usrId) ? await app.GetAccountAsync(usrId) : null;
        var userStr = usr != null ? usr.Username : "null";
        //Debug.Log($"Found User {userStr}");
        AuthResult res = new AuthResult();
        try
        {
            //Debug.Log($"Calling AcquireTokenSilentAsync");
            res.res = await app.AcquireTokenSilentAsync(scopes, usr).ConfigureAwait(false);
            //Debug.Log($"app.AcquireTokenSilentAsync called {res.res}");
        }
        catch (MsalUiRequiredException)
        {
            //Debug.Log($"Needs UI for Login");
            try
            {
                res.res = await app.AcquireTokenAsync(scopes).ConfigureAwait(false);
                if (callback != null)
                {
                    await callback(res.res);
                }
                //Debug.Log($"app.AcquireTokenAsync called {res.res}");
            }
            catch (MsalException msalex)
            {
                res.err = $"Error Acquiring Token:{Environment.NewLine}{msalex}";
                //Debug.Log($"{res.err}");
                return res;
            }
        }
        catch (Exception ex)
        {
            res.err = $"Error Acquiring Token Silently:{Environment.NewLine}{ex}";
            //Debug.Log($"{res.err}");
            return res;
        }

#if !UNITY_EDITOR && UNITY_WSA
        Debug.Log($"Access Token - {res.res.AccessToken}");
        ApplicationData.Current.LocalSettings.Values["UserId"] = res.res.User.Identifier;
#endif
        return res;
    }

    private async Task ListEmailAsync(string accessToken, Action<Value> success, Action<string> error)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.GetAsync("https://graph.microsoft.com/v1.0/me/messages?$top=5");
        if (!response.IsSuccessStatusCode)
        {
            error(response.ReasonPhrase);
            return;
        }

        var respStr = await response.Content.ReadAsStringAsync();
        //Debug.Log(respStr);

        Rootobject email = null;
        try
        {
            // Parse the Json...
            email = JsonConvert.DeserializeObject<Rootobject>(respStr);
            //email = JsonUtility.FromJson<Rootobject>(respStr);
        }
        catch (Exception ex)
        {
            //Debug.Log($"Error = {ex.Message}");
            return;
        }
        //Debug.Log($"msg count = {email.value.Length}");
        foreach (var msg in email.value)
        {
            success(msg);
        }
    }

    private async Task<AuthResult> AcquireTokenDeviceFlowAsync(PublicClientApplication app, IEnumerable<string> scopes, string userId,
        Func<DeviceCodeResult, Task> callback)
    {
        AuthResult res = new AuthResult();

        try
        {
            res.res = await app.AcquireTokenWithDeviceCodeAsync(scopes,
                string.Empty, async deviceCodeCallback =>
                {
                    // This will print the message on the console which tells the user where to go sign-in using 
                    // a separate browser and the code to enter once they sign in.
                    // The AcquireTokenWithDeviceCodeAsync() method will poll the server after firing this
                    // device code callback to look for the successful login of the user via that browser.
                    // This background polling (whose interval and timeout data is also provided as fields in the 
                    // deviceCodeCallback class) will occur until:
                    // * The user has successfully logged in via browser and entered the proper code
                    // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
                    // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
                    //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
                    if (callback != null)
                    {
                        await callback(deviceCodeCallback);
                    }

                    return;
                }, CancellationToken.None);

            //Console.WriteLine(result.Account.Username);
        }
        catch (MsalServiceException ex)
        {
            // Kind of errors you could have (in ex.Message)

            // AADSTS50059: No tenant-identifying information found in either the request or implied by any provided credentials.
            // Mitigation: as explained in the message from Azure AD, the authoriy needs to be tenanted. you have probably created
            // your public client application with the following authorities:
            // https://login.microsoftonline.com/common or https://login.microsoftonline.com/organizations

            // AADSTS90133: Device Code flow is not supported under /common or /consumers endpoint.
            // Mitigation: as explained in the message from Azure AD, the authority needs to be tenanted

            // AADSTS90002: Tenant <tenantId or domain you used in the authority> not found. This may happen if there are 
            // no active subscriptions for the tenant. Check with your subscription administrator.
            // Mitigation: if you have an active subscription for the tenant this might be that you have a typo in the 
            // tenantId (GUID) or tenant domain name.
        }
        catch (OperationCanceledException ex)
        {
            // If you use a CancellationToken, and call the Cancel() method on it, then this may be triggered
            // to indicate that the operation was cancelled. 
            // See https://docs.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads 
            // for more detailed information on how C# supports cancellation in managed threads.
        }
        catch (MsalClientException ex)
        {
            // Verification code expired before contacting the server
            // This exception will occur if the user does not manage to sign-in before a time out (15 mins) and the
            // call to `AcquireTokenWithDeviceCodeAsync` is not cancelled in between
        }

        return res;
    }


    public async Task<AuthResult> SignInWithDeviceCodeAsync(Func<DeviceCodeResult, Task> callback = null,
                                                            Func<string, Task> statusCb = null)
    {
        var res = await AcquireTokenDeviceFlowAsync(_client, _scopes, _userId, callback);

        if (string.IsNullOrEmpty(res.err) && statusCb != null)
        {
            await statusCb($"Signed in as {res.res.Account.Username}");

            await ListEmailAsync(res.res.AccessToken, t =>
            {
                statusCb($"\nFrom: {t.from.emailAddress.address}\nSubject:{t.subject}");
            },
            t =>
            {
                statusCb($"{t}");
            });
        }
        else if (res.err != null)
        {
            await statusCb($"Error - {res.err}");
        }
        return res;
    }

    public async Task<AuthResult> SignInUserFlowAsync(Func<AuthenticationResult, Task> callback = null,
                                                      Func<string, Task> statusCb = null)
    {
        var res = await AcquireTokenAsync(_client, _scopes, _userId, callback);

        if (string.IsNullOrEmpty(res.err) && statusCb != null)
        {
            await statusCb($"Signed in as {res.res.Account.Username}");

            await ListEmailAsync(res.res.AccessToken, t =>
            {
                statusCb($"\nFrom: {t.from.emailAddress.address}\nSubject:{t.subject}");
            },
            t =>
            {
                statusCb($"{t}");
            });
        }
        else if (res.err != null)
        {
            await statusCb($"Error - {res.err}");
        }
        return res;
    }

    private void SignOut()
    {
#if !UNITY_EDITOR && UNITY_WSA
        ApplicationData.Current.LocalSettings.Values["UserId"] = _userId = null;
        var usr = _client.GetUser(_userId);
        if (usr != null)
        {
            _client.Remove(usr);
        }
#endif
    }

    //public async void OnSpeechKeywordRecognized(SpeechEventData eventData)
    //{
    //    if (eventData.RecognizedText == "sign in")
    //    {
    //        _statusText.text = "Signing In...";
    //        _welcomeText.text = "";

    //        await SignInAsync();
    //    }

    //    if (eventData.RecognizedText == "sign out")
    //    {
    //        SignOut();
    //        _statusText.text = "--- Not Signed In ---";
    //    }
    //}


    [Serializable]
    public class Rootobject
    {
        public string odatacontext;
        public string odatanextLink;
        public Value[] value;
    }

    [Serializable]
    public class Value
    {
        public string odataetag;
        public string id;
        public DateTime createdDateTime;
        public DateTime lastModifiedDateTime;
        public string changeKey;
        public object[] categories;
        public DateTime receivedDateTime;
        public DateTime sentDateTime;
        public bool hasAttachments;
        public string internetMessageId;
        public string subject;
        public string bodyPreview;
        public string importance;
        public string parentFolderId;
        public string conversationId;
        public object isDeliveryReceiptRequested;
        public bool isReadReceiptRequested;
        public bool isRead;
        public bool isDraft;
        public string webLink;
        public string inferenceClassification;
        public Body body;
        public Sender sender;
        public From from;
        public Torecipient[] toRecipients;
        public object[] ccRecipients;
        public object[] bccRecipients;
        public Replyto[] replyTo;
    }

    [Serializable]
    public class Body
    {
        public string contentType;
        public string content;
    }

    [Serializable]
    public class Sender
    {
        public Emailaddress emailAddress;
    }

    [Serializable]
    public class Emailaddress
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class From
    {
        public Emailaddress1 emailAddress;
    }

    [Serializable]
    public class Emailaddress1
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class Torecipient
    {
        public Emailaddress2 emailAddress;
    }

    [Serializable]
    public class Emailaddress2
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class Replyto
    {
        public Emailaddress3 emailAddress;
    }

    [Serializable]
    public class Emailaddress3
    {
        public string name;
        public string address;
    }
}
