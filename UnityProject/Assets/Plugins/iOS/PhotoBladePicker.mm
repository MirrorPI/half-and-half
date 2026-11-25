#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>
#import <ImageIO/ImageIO.h>

extern "C" UIViewController *UnityGetGLViewController(void);

@interface HHBladePhotoSelection : NSObject <PHPickerViewControllerDelegate>
@property(nonatomic) int token;
@property(nonatomic) int side;
@property(nonatomic) int result;
@property(nonatomic, strong) NSData *png;
@property(nonatomic, weak) PHPickerViewController *picker;
@property(nonatomic, strong) NSProgress *progress;
@end

static HHBladePhotoSelection *selection;
static int lastToken;

// All shared picker state lives on the main queue. Decode only the selected file on the
// provider's background completion queue, while its temporary URL is still valid.
@implementation HHBladePhotoSelection
- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results
{
    [picker dismissViewControllerAnimated:YES completion:nil];
    if (self != selection) return;
    NSItemProvider *provider = results.firstObject.itemProvider;
    if (!provider) { self.result = -2; return; }
    int side = self.side;
    self.progress = [provider loadFileRepresentationForTypeIdentifier:@"public.image"
        completionHandler:^(NSURL *url, NSError *error) {
        @autoreleasepool {
            NSData *png = nil;
            if (url && !error) {
                NSDictionary *options = @{ (__bridge NSString *)kCGImageSourceShouldCache: @NO };
                CGImageSourceRef source = CGImageSourceCreateWithURL((__bridge CFURLRef)url, (__bridge CFDictionaryRef)options);
                if (source) {
                    NSDictionary *resize = @{
                        (__bridge NSString *)kCGImageSourceCreateThumbnailFromImageAlways: @YES,
                        (__bridge NSString *)kCGImageSourceCreateThumbnailWithTransform: @YES,
                        (__bridge NSString *)kCGImageSourceThumbnailMaxPixelSize: @(side),
                        (__bridge NSString *)kCGImageSourceShouldCacheImmediately: @YES
                    };
                    CGImageRef image = CGImageSourceCreateThumbnailAtIndex(source, 0, (__bridge CFDictionaryRef)resize);
                    if (image) {
                        png = UIImagePNGRepresentation([UIImage imageWithCGImage:image]);
                        CGImageRelease(image);
                    }
                    CFRelease(source);
                }
            }
            dispatch_async(dispatch_get_main_queue(), ^{
                if (selection != self) return;
                self.progress = nil;
                self.png = png;
                self.result = png.length > 0 ? (int)png.length : -1;
            });
        }
    }];
}
@end

extern "C" int HHPhotoBegin(int maximumSide)
{
    HHBladePhotoSelection *session = [HHBladePhotoSelection new];
    session.token = ++lastToken;
    session.side = maximumSide;
    selection = session;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (session != selection) return;
        UIViewController *host = UnityGetGLViewController();
        // Respect a Game Center sheet, a store sheet, or a backgrounded player.
        if (!host.viewIfLoaded.window || host.presentedViewController ||
            UIApplication.sharedApplication.applicationState != UIApplicationStateActive) {
            session.result = -1;
            return;
        }
        PHPickerConfiguration *config = [PHPickerConfiguration new];
        config.filter = PHPickerFilter.imagesFilter;
        config.selectionLimit = 1;
        config.preferredAssetRepresentationMode = PHPickerConfigurationAssetRepresentationModeCurrent;
        PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:config];
        picker.delegate = session;
        picker.modalInPresentation = YES;
        session.picker = picker;
        [host presentViewController:picker animated:YES completion:nil];
    });
    return session.token;
}

extern "C" int HHPhotoSize(int token)
{
    return selection.token == token ? selection.result : -1;
}

extern "C" bool HHPhotoCopy(int token, void *bytes, int length)
{
    if (!bytes || length <= 0 || selection.token != token || selection.png.length != (NSUInteger)length) return false;
    [selection.png getBytes:bytes length:(NSUInteger)length];
    return true;
}

extern "C" void HHPhotoEnd(int token)
{
    if (selection.token != token) return;
    HHBladePhotoSelection *finished = selection;
    selection = nil;
    [finished.progress cancel];
    dispatch_async(dispatch_get_main_queue(), ^{
        PHPickerViewController *picker = finished.picker;
        picker.delegate = nil;
        if (picker.presentingViewController && !picker.isBeingDismissed) [picker dismissViewControllerAnimated:NO completion:nil];
    });
}

extern "C" const char *HHPhotoDirectory(void)
{
    static NSString *path;
    if (!path) {
        NSFileManager *files = NSFileManager.defaultManager;
        NSURL *support = [files URLsForDirectory:NSApplicationSupportDirectory inDomains:NSUserDomainMask].firstObject;
        NSURL *dir = [support URLByAppendingPathComponent:@"PhotoBlade" isDirectory:YES];
        if (!dir || ![files createDirectoryAtURL:dir withIntermediateDirectories:YES attributes:nil error:nil] ||
            ![dir setResourceValue:@YES forKey:NSURLIsExcludedFromBackupKey error:nil]) return nullptr;
        path = dir.path;
    }
    return path.UTF8String;
}
