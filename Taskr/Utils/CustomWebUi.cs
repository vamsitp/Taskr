﻿namespace Taskr
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    using Microsoft.IdentityModel.Clients.ActiveDirectory.Extensibility;
    using Microsoft.Toolkit.Wpf.UI.Controls;

    // Credit: https://techcommunity.microsoft.com/t5/windows-dev-appconsult/how-to-use-active-directory-authentication-library-adal-for-net/ba-p/400623#
    internal class CustomWebUi : ICustomWebUi
    {
        public async Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri)
        {
            var tcs = new TaskCompletionSource<Uri>();
            var thread = new Thread(() => this.AcquireAuthorizationCode(authorizationUri, tcs));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return await tcs.Task;
        }

        private void AcquireAuthorizationCode(Uri authorizationUri, TaskCompletionSource<Uri> tcs)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // WebView2 not available yet
            var webView = new WebView();
#pragma warning restore CS0618 // Type or member is obsolete
            var window = new Window
            {
                Title = "Sign in to your account",
                WindowStyle = WindowStyle.ToolWindow,
                Content = webView,
                Width = 600,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };

            window.Loaded += (_, __) => webView.Navigate(authorizationUri);
            webView.NavigationCompleted += (_, e) =>
            {
                System.Diagnostics.Debug.WriteLine(e.Uri);
                if (e.Uri.Query.ContainsIgnoreCase("code="))
                {
                    tcs.SetResult(e.Uri);
                    window.DialogResult = true;
                    window.Close();
                }

                if (e.Uri.Query.ContainsIgnoreCase("error="))
                {
                    tcs.SetException(new Exception(e.Uri.Query));
                    window.DialogResult = false;
                    window.Close();
                }
            };
            webView.UnsupportedUriSchemeIdentified += (_, e) =>
            {
                if (e.Uri.Query.Contains("code=", StringComparison.OrdinalIgnoreCase))
                {
                    tcs.SetResult(e.Uri);
                    window.DialogResult = true;
                    window.Close();
                }
                else
                {
                    tcs.SetException(new Exception($"Unknown error: {e.Uri}"));
                    window.DialogResult = false;
                    window.Close();
                }
            };

            window.Activate();
            window.BringIntoView();
            window.Topmost = true;
            window.Focus();
            if (window.ShowDialog() != true && !tcs.Task.IsCompleted)
            {
                tcs.SetException(new Exception("canceled"));
            }
        }
    }
}
