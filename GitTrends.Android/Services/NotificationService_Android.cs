﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Autofac;
using Firebase.Messaging;
using GitTrends.Droid;
using GitTrends.Shared;
using Microsoft.Azure.NotificationHubs;
using Xamarin.Forms;

[assembly: Dependency(typeof(NotificationService_Android))]
namespace GitTrends.Droid
{
    public class NotificationService_Android : INotificationService
    {
        Context CurrentContext => Xamarin.Essentials.Platform.AppContext;

        public void Initialize()
        {

        }

        public Task<bool?> AreNotificationEnabled()
        {
            bool isPushNotificationEnabled;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O
                && CurrentContext.GetSystemService(Context.NotificationService) is NotificationManager notificationManager)
            {
                isPushNotificationEnabled = notificationManager.AreNotificationsEnabled();
            }
            else
            {
                isPushNotificationEnabled = NotificationManagerCompat.From(CurrentContext).AreNotificationsEnabled();
            }

            return Task.FromResult<bool?>(isPushNotificationEnabled);
        }

        public Task SetiOSBadgeCount(int count) => throw new NotSupportedException();
    }

    [Service, IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class FirebaseService : FirebaseMessagingService
    {
        readonly static TaskCompletionSource<NotificationHubInformation> _notificationHubInformationTCS = new TaskCompletionSource<NotificationHubInformation>();

        public override async void OnNewToken(string token)
        {
            using var scope = ContainerService.Container.BeginLifetimeScope();
            var notificationService = scope.Resolve<NotificationService>();

            notificationService.InitializationCompleted += HandleInitializationCompleted;

            var notificationHubInformation = await notificationService.GetNotificationHubInformation().ConfigureAwait(false);

            if (notificationHubInformation.IsEmpty())
                notificationHubInformation = await _notificationHubInformationTCS.Task.ConfigureAwait(false);

            notificationService.InitializationCompleted -= HandleInitializationCompleted;
            await RegisterWithNotificationHub(notificationHubInformation, token).ConfigureAwait(false);
        }

        public override async void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);

            using var scope = ContainerService.Container.BeginLifetimeScope();
            var backgroundFetchService = scope.Resolve<BackgroundFetchService>();

            await Task.WhenAll(backgroundFetchService.CleanUpDatabase(), backgroundFetchService.NotifyTrendingRepositories(CancellationToken.None));
        }

        Task RegisterWithNotificationHub(in NotificationHubInformation notificationHubInformation, in string token)
        {
#if AppStore
            var hubClient = NotificationHubClient.CreateClientFromConnectionString(notificationHubInformation.ConnectionString, notificationHubInformation.Name);
#else
            if (notificationHubInformation.IsEmpty())
                return Task.CompletedTask;

            var hubClient = NotificationHubClient.CreateClientFromConnectionString(notificationHubInformation.ConnectionString_Debug, notificationHubInformation.Name_Debug);
#endif
            return hubClient.CreateFcmNativeRegistrationAsync(token);
        }

        static void HandleInitializationCompleted(object sender, NotificationHubInformation e) => _notificationHubInformationTCS.SetResult(e);
    }
}