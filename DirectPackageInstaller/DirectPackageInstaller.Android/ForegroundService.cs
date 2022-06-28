using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Exception = Java.Lang.Exception;

namespace DirectPackageInstaller.Android;

[Service]
public class ForegroundService : Service
{
    private static Dictionary<string, Action> IntentActionMap = new();
    
    public static int IDCount = 1;
    
    public int NotificationID = IDCount++;
        
    private NotificationChannel _channel;
    
    public override void OnCreate()
    {
        
        BindChannel();
        BindForeground();
        
        base.OnCreate();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        BindChannel();
        BindForeground();

        var ID = intent?.GetStringExtra("ID");
        if (ID != null && IntentActionMap.ContainsKey(ID))
        {
            var Action = IntentActionMap[ID];
            Action?.Invoke();
        }

        return StartCommandResult.Sticky;
    }
    
    public static void StartService(Context context, Action? Action)
    {
        var intent = new Intent(context, typeof(ForegroundService));
        intent.PutExtra("ID",IntentActionMap.Count.ToString());

        if (Action != null)
        {
            IntentActionMap[IntentActionMap.Count.ToString()] = Action;
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }


    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    private void BindChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            try
            {
                _channel = new NotificationChannel("ServiceChannel", "Service", NotificationImportance.Default);
                _channel.SetSound(null, null);
                _channel.SetShowBadge(false);
                NotificationManager.FromContext(this)?.CreateNotificationChannel(_channel);
            }
            catch (Exception ex)
            {
                MainActivity.LogFatalError(ex);
            }
        }
    }

    private void BindForeground()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            try
            {
                var Intent = new Intent(this, typeof(NotificationReceiver));
                PendingIntentFlags Flags = Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0;
                var pendingIntent = PendingIntent.GetBroadcast(this, 0, Intent, Flags);
                
                var Notification = new Notification.Builder(this, "ServiceChannel");
                Notification.SetContentIntent(pendingIntent);
                Notification.SetContentText("A thread is running.");

                StartForeground(NotificationID, Notification.Build());
            }
            catch (Exception ex)
            {
                MainActivity.LogFatalError(ex);
            }
        }
    }

    private void UnbindForeground()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            NotificationManager.FromContext(this)
                .Notify(NotificationID, new Notification.Builder(this, "ServiceChannel")
                    .SetContentIntent(PendingIntent.GetBroadcast(this, 0,
                        new Intent(this, typeof(NotificationReceiver)), 0))
                    .SetContentText("A thread is stopped.")
                    .Build());
        }
    }
    
}   
[BroadcastReceiver]
public class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        context?.StartActivity(context.PackageManager.GetLaunchIntentForPackage(context.PackageName)
            .SetFlags(ActivityFlags.BroughtToFront | ActivityFlags.ReorderToFront | ActivityFlags.NewTask));
    }
}