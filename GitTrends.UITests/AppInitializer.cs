﻿using Xamarin.UITest;

namespace GitTrends.UITests
{
    static class AppInitializer
    {
        public static IApp StartApp(Platform platform) => platform switch
        {
            Platform.Android => ConfigureApp.Android.StartApp(Xamarin.UITest.Configuration.AppDataMode.Clear),
            Platform.iOS => ConfigureApp.iOS.StartApp(Xamarin.UITest.Configuration.AppDataMode.Clear),
            _ => throw new System.NotSupportedException(),
        };
    }
}

