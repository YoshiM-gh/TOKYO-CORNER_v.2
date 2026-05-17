#import <AppKit/AppKit.h>

static NSWindow* GetUnityWindow() {
    NSWindow* win = [[NSApplication sharedApplication] mainWindow];
    if (win != nil) return win;
    NSArray* windows = [[NSApplication sharedApplication] windows];
    if ([windows count] > 0) return [windows objectAtIndex:0];
    return nil;
}

extern "C" {
    void SetWindowAlwaysOnTop(bool onTop) {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSWindow* window = GetUnityWindow();
            if (window != nil) {
                [window setLevel:(onTop ? NSFloatingWindowLevel : NSNormalWindowLevel)];
            }
        });
    }
}
