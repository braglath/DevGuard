#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>

static void (*g_activate)(const char *document, const char *file) = NULL;

typedef enum {
    NotifyNone = 0,
    NotifyUser = 1,
    NotifyLegacy = 2,
    NotifyScript = 3
} NotifyMode;

static NotifyMode g_mode = NotifyNone;

@interface SuperDevNotifier : NSObject <UNUserNotificationCenterDelegate>
@end

static SuperDevNotifier *g_delegate = nil;

@interface SuperDevLegacyNotifier : NSObject
@end

static SuperDevLegacyNotifier *g_legacy = nil;

static void DeliverClick(NSString *document, NSString *file)
{
    if (g_activate != NULL && document != nil)
        g_activate(document.UTF8String, file != nil ? file.UTF8String : NULL);
}

@implementation SuperDevNotifier

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler
{
    (void)center;
    UNNotificationPresentationOptions options =
        UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionList;
    if (notification.request.content.sound != nil)
        options |= UNNotificationPresentationOptionSound;
    completionHandler(options);
}

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
didReceiveNotificationResponse:(UNNotificationResponse *)response
         withCompletionHandler:(void (^)(void))completionHandler
{
    (void)center;
    NSString *document = response.notification.request.content.userInfo[@"document"];
    NSString *file = response.notification.request.content.userInfo[@"file"];
    DeliverClick(document, file);
    completionHandler();
}

@end

@implementation SuperDevLegacyNotifier

- (BOOL)userNotificationCenter:(NSUserNotificationCenter *)center
     shouldPresentNotification:(NSUserNotification *)notification
{
    (void)center;
    (void)notification;
    return YES;
}

- (void)userNotificationCenter:(NSUserNotificationCenter *)center
       didActivateNotification:(NSUserNotification *)notification
{
    (void)center;
    DeliverClick(notification.userInfo[@"document"], notification.userInfo[@"file"]);
}

@end

static BOOL RunningInAppBundle(void)
{
    NSString *path = [NSBundle mainBundle].bundlePath;
    if (path == nil)
        return NO;

    return [path hasSuffix:@".app"] || [path containsString:@".app/"];
}

static NSString *Text(const char *value)
{
    if (value == NULL || value[0] == '\0')
        return nil;

    return [NSString stringWithUTF8String:value];
}

static int StartUserNotifications(void)
{
    if (!RunningInAppBundle() || NSClassFromString(@"UNUserNotificationCenter") == Nil)
        return 0;

    @try {
        if (g_delegate == nil)
            g_delegate = [SuperDevNotifier new];

        UNUserNotificationCenter *center = UNUserNotificationCenter.currentNotificationCenter;
        center.delegate = g_delegate;
        [center requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
                              completionHandler:^(__unused BOOL granted, __unused NSError *error) {}];
        return 1;
    } @catch (NSException *exception) {
        (void)exception;
        return 0;
    }
}

static int StartLegacyNotifications(void)
{
    if (NSClassFromString(@"NSUserNotificationCenter") == Nil)
        return 0;

    @try {
        if (g_legacy == nil)
            g_legacy = [SuperDevLegacyNotifier new];

        NSUserNotificationCenter *center = [NSUserNotificationCenter defaultUserNotificationCenter];
        center.delegate = (id<NSUserNotificationCenterDelegate>)g_legacy;
        return 1;
    } @catch (NSException *exception) {
        (void)exception;
        return 0;
    }
}

static NSString *AppleQuoted(NSString *value)
{
    NSString *text = value ?: @"";
    text = [text stringByReplacingOccurrencesOfString:@"\\" withString:@"\\\\"];
    text = [text stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
    return [NSString stringWithFormat:@"\"%@\"", text];
}

static void PostScript(NSString *subtitle, NSString *body, int silent)
{
    NSString *sound = silent ? @"" : @" sound name \"Glass\"";
    NSString *script = [NSString stringWithFormat:@"display notification %@ with title \"Super Dev\" subtitle %@%@",
                                                  AppleQuoted(body),
                                                  AppleQuoted(subtitle),
                                                  sound];
    @try {
        NSTask *task = [[NSTask alloc] init];
        task.executableURL = [NSURL fileURLWithPath:@"/usr/bin/osascript"];
        task.arguments = @[@"-e", script];
        [task launchAndReturnError:nil];
    } @catch (NSException *exception) {
        (void)exception;
    }
}

int sd_notify_start(void (*activate)(const char *document, const char *file), int background)
{
    @autoreleasepool {
        g_activate = activate;

        @try {
            if (background) {
                [NSApplication sharedApplication];
                [NSApp setActivationPolicy:NSApplicationActivationPolicyProhibited];
            }
        } @catch (NSException *exception) {
            (void)exception;
        }

        if (StartUserNotifications()) {
            g_mode = NotifyUser;
            return 1;
        }

        if (StartLegacyNotifications()) {
            g_mode = NotifyLegacy;
            return 1;
        }

        g_mode = NotifyScript;
        return 1;
    }
}

void sd_notify_post(const char *title, const char *subtitle, const char *body, int silent, const char *document, const char *file)
{
    (void)title;
    @autoreleasepool {
        NSString *subtitleText = Text(subtitle);
        NSString *bodyText = Text(body);
        NSString *documentText = Text(document);
        NSString *fileText = Text(file);

        @try {
            if (g_mode == NotifyUser) {
                UNMutableNotificationContent *content = [UNMutableNotificationContent new];
                content.title = @"Super Dev";
                if (subtitleText != nil)
                    content.subtitle = subtitleText;
                if (bodyText != nil)
                    content.body = bodyText;
                if (!silent)
                    content.sound = [UNNotificationSound defaultSound];

                NSMutableDictionary *info = [NSMutableDictionary dictionary];
                if (documentText != nil)
                    info[@"document"] = documentText;
                if (fileText != nil)
                    info[@"file"] = fileText;
                content.userInfo = info;

                UNNotificationRequest *request =
                    [UNNotificationRequest requestWithIdentifier:[[NSUUID UUID] UUIDString]
                                                         content:content
                                                         trigger:nil];
                [UNUserNotificationCenter.currentNotificationCenter addNotificationRequest:request
                                                                     withCompletionHandler:nil];
                return;
            }

            if (g_mode == NotifyLegacy) {
                NSUserNotification *note = [NSUserNotification new];
                note.title = @"Super Dev";
                if (subtitleText != nil)
                    note.subtitle = subtitleText;
                if (bodyText != nil)
                    note.informativeText = bodyText;
                if (!silent)
                    note.soundName = NSUserNotificationDefaultSoundName;

                NSMutableDictionary *info = [NSMutableDictionary dictionary];
                if (documentText != nil)
                    info[@"document"] = documentText;
                if (fileText != nil)
                    info[@"file"] = fileText;
                note.userInfo = info;
                [[NSUserNotificationCenter defaultUserNotificationCenter] deliverNotification:note];
                return;
            }
        } @catch (NSException *exception) {
            (void)exception;
        }

        PostScript(subtitleText ?: @"Checklist", bodyText ?: @"", silent);
    }
}

void sd_notify_bind(void)
{
    @try {
        if (g_mode == NotifyUser && g_delegate != nil)
            UNUserNotificationCenter.currentNotificationCenter.delegate = g_delegate;
        else if (g_mode == NotifyLegacy && g_legacy != nil)
            [NSUserNotificationCenter defaultUserNotificationCenter].delegate =
                (id<NSUserNotificationCenterDelegate>)g_legacy;
    } @catch (NSException *exception) {
        (void)exception;
    }
}

void sd_notify_pump(double seconds)
{
    CFRunLoopRunInMode(kCFRunLoopDefaultMode, seconds, false);
}

void sd_notify_activate_app(void)
{
    @autoreleasepool {
        @try {
            [NSApp activateIgnoringOtherApps:YES];
        } @catch (NSException *exception) {
            (void)exception;
        }
    }
}
