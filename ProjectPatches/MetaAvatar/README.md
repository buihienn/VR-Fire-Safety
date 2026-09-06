# Meta Avatar Unity Editor Crash Workaround

This is an optional local workaround for a Unity Editor crash observed with the package versions below. Apply it only if Unity crashes when entering Play Mode repeatedly.

## Environment

- Unity: 6000.1.14f1
- Meta XR SDK: v81
- Meta Avatars SDK: 40.0.1

## Problem

Unity may crash when entering Play Mode repeatedly after installing Meta Avatars SDK.

The crash occurs during assembly reload and `InitializeOnLoad` processing in:

`AvatarAssetsPackageCheckTrigger`

## File to Modify

Open:

`Library/PackageCache/com.meta.xr.sdk.avatars@*/Editor/Scripts/PackageAssetsPostProcessor.cs`

Find:

`AvatarAssetsPackageCheckTrigger`

Replace the class with:

```csharp
[InitializeOnLoad]
public class AvatarAssetsPackageCheckTrigger
{
    private const string SessionKey =
        "AvatarAssetsPackageCheckRanOnce";

    static AvatarAssetsPackageCheckTrigger()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);

        if (PresetHelper.CheckIfPresetsPackaged())
        {
            Debug.Log("Avatar Preset are already packaged.");
        }
        else
        {
            Debug.Log("Detected missing packaged presets, repackaging.");
            PresetHelper.PackagePresetsDefaultSelection();
        }

        CoreAssetsMover.CopyAssets();
    }
}
```

## What the Workaround Changes

The `SessionState` guard allows the package-asset check to run only once during each Unity Editor session. This avoids repeating the preset packaging and asset-copy operation after an assembly reload.

## Important

- Close Unity before editing the package file.
- `Library/PackageCache` is generated locally and is intentionally not committed to Git.
- Unity may overwrite this change after a package refresh, reinstallation, or upgrade. Reapply the workaround if the crash returns.
- Remove the workaround when upgrading to a Meta Avatars SDK release that resolves the underlying issue.
