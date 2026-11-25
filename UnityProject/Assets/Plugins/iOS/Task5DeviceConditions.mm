#import <Foundation/Foundation.h>

// Read-only Task5 QA condition sample. C# calls this only in the opted-in iOS development probe.
extern "C" __attribute__((visibility("default"))) int HalfTask5ReadDeviceConditions()
{
    NSProcessInfo *info = [NSProcessInfo processInfo];
    return ((int)[info thermalState]) | ([info isLowPowerModeEnabled] ? 256 : 0);
}
