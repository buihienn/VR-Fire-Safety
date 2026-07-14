# Meta Avatar Unity Editor Crash Fix

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