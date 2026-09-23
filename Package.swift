// swift-tools-version: 6.2
import PackageDescription

// OpenUsage ships as a macOS menu-bar app, and its provider engine (auth stores, usage clients,
// mappers, pricing, snapshot cache) also builds on Windows (and Linux, which CI uses as a quick
// portability check). Off macOS the package builds the shared engine plus the `openusage` CLI; the
// Windows tray app in `windows/` drives that CLI. The AppKit/SwiftUI layer and the Mac-only
// dependencies (global hotkey recorder, Sparkle updates, PostHog's Apple SDK) are left out there.
#if os(macOS)
let isMac = true
#else
let isMac = false
#endif

/// Files in `Sources/OpenUsage` that only make sense on macOS: the AppKit/SwiftUI UI layer and the
/// services that wrap Apple-only frameworks. Everything else is the portable engine.
let macOnlySources: [String] = [
    "App/AppContainer.swift",
    "App/LegacyLaunchAgentCleanup.swift",
    "App/OpenUsageApp.swift",
    "App/PanelHeightController.swift",
    "App/PanelOutsideClickMonitor.swift",
    "App/PopoverBackdropView.swift",
    "App/SingleInstanceGuard.swift",
    "App/SingleInstanceLock.swift",
    "App/StatusItemController.swift",
    "App/StatusItemImageUpdater.swift",
    "App/UpdaterController.swift",
    "Views",
    "Providers/Codex/CodexResetClaimService.swift",
    "Services/CommandLineToolInstaller.swift",
    "Services/LocalUsageServer.swift",
    "Services/ScreenCaptureProbe.swift",
    "Services/Telemetry.swift",
    "Stores/AppearanceSetting.swift",
    "Stores/DensitySetting.swift",
    "Stores/ICloudUsageSyncStore.swift",
    "Stores/LaunchAtLoginSetting.swift",
    "Stores/LayoutStore.swift",
    "Stores/LayoutStore+Customization.swift",
    "Stores/MenuBarPrivacyStore.swift",
    "Stores/PopoverTransparencyStore.swift",
    "Stores/PopoverTransparencyStyle.swift",
    "Stores/ReduceAnimationsSetting.swift",
    "Support/AboutPanel.swift",
    "Support/Animations.swift",
    "Support/AppNotifications.swift",
    "Support/AppShortcuts.swift",
    "Support/Haptics.swift",
    "Support/InvisibleOverlayScroller.swift",
    "Support/LiquidGlassFallbacks.swift",
    "Support/MenuBarIcon.swift",
    "Support/MenuBarStripRenderer.swift",
    "Support/PartyMode.swift",
    "Support/PopoverDismissReader.swift",
    "Support/PopoverSurfaceTreatment.swift",
    "Support/ProviderIconShape.swift",
    "Support/ShareCardRenderer.swift",
    "Support/Theme.swift",
    "Support/TooMuchTransparencyEffect.swift",
    "Support/TooMuchTransparencyKeyReader.swift",
]

/// Tests in `Tests/OpenUsageTests` that only build or run on macOS: they exercise the AppKit/SwiftUI
/// layer or Apple frameworks, or mix `@MainActor` and nonisolated test methods in one class, which
/// XCTest's test discovery off Apple platforms can't enumerate. Everything else runs everywhere.
let macOnlyTests: [String] = [
    "AntigravityLayoutTests.swift",
    "AppNotificationsTests.swift",
    "ClaudeDesktopAuthStoreTests+Fixtures.swift",
    "ClaudeDesktopAuthStoreTests.swift",
    "ClaudeResetGrantsTests.swift",
    "ClaudeSwapAccountTests.swift",
    "ClaudeSwapOverlapTests.swift",
    "CodexResetClaimTests.swift",
    "CodexSwapAccountTests.swift",
    "CodexSwapHistoryTests.swift",
    "CodexSwapMaintainerReviewTests.swift",
    "CodexSwapReviewRegressionTests.swift",
    "CommandLineToolInstallerTests.swift",
    "CursorOptionalEndpointTests.swift",
    "CursorProviderTests.swift",
    "DensitySettingTests.swift",
    "ICloudUsageSyncStoreTests.swift",
    "KeychainAccessorTests.swift",
    "LaunchAtLoginSettingTests.swift",
    "LayoutBootstrapTests.swift",
    "LayoutPersistenceTests.swift",
    "LayoutStoreTests.swift",
    "LegacyLaunchAgentCleanupTests.swift",
    "LocalUsageAPITests.swift",
    "LoginShellEnvironmentTests.swift",
    "MenuBarBarsTests.swift",
    "MenuBarContentTests.swift",
    "MenuBarPinTests.swift",
    "MenuBarPrivacyStoreTests.swift",
    "MenuBarStripMemoTests.swift",
    "MenuBarStripTrimTests.swift",
    "ModelUsageHoverTests.swift",
    "NotificationSettingsStoreTests.swift",
    "OllamaMonthlyUsageTests.swift",
    "PanelGeometryTests.swift",
    "PanelHeightBridgeTests.swift",
    "PanelHeightCoordinatorTests.swift",
    "PanelOutsideClickPolicyTests.swift",
    "PopoverKeyReaderTests.swift",
    "PopoverScreenTests.swift",
    "PopoverSurfaceOpacityTests.swift",
    "PopoverTransparencyStoreTests.swift",
    "PopoverTransparencyStyleTests.swift",
    "ProviderAccountsStoreTests.swift",
    "ProviderEnablementEnforcementTests.swift",
    "ProviderEnablementStoreTests.swift",
    "ProviderMarksTests.swift",
    "ReduceAnimationsSettingTests.swift",
    "ReorderGeometryTests.swift",
    "ResetDisplayTests.swift",
    "ShareCardRendererTests.swift",
    "ShellEnvironmentSnapshotTests.swift",
    "SingleInstanceGuardTests.swift",
    "SingleInstanceLockTests.swift",
    "TelemetryRecorderTests.swift",
    "TelemetrySinkTests.swift",
    "UpdaterControllerTests.swift",
    "UsageHistoryClassificationTests.swift",
    "UsageTrendPopoverTests.swift",
    "UsageTrendTests.swift",
    "WidgetUsagePeriodTests.swift",
]

var packageDependencies: [Package.Dependency] = []
var coreDependencies: [Target.Dependency] = []
if isMac {
    packageDependencies += [
        // The de-facto standard recorder + global hotkey for Mac apps (System Settings-style field).
        .package(url: "https://github.com/sindresorhus/KeyboardShortcuts", from: "3.0.1"),
        // In-app auto-updates (appcast + EdDSA-signed downloads). 2.9.4 fixes the update window opening
        // behind other apps for menu-bar (dockless) apps (sparkle-project/Sparkle#2889).
        .package(url: "https://github.com/sparkle-project/Sparkle", from: "2.9.4"),
        // Anonymous usage analytics and mandatory crash reporting (official first-party Swift SDK).
        .package(url: "https://github.com/PostHog/posthog-ios.git", from: "3.62.0")
    ]
    coreDependencies += [
        .product(name: "KeyboardShortcuts", package: "KeyboardShortcuts"),
        .product(name: "Sparkle", package: "Sparkle"),
        .product(name: "PostHog", package: "posthog-ios")
    ]
} else {
    // CryptoKit's API (SHA256, AES-GCM, Curve25519) for platforms without CryptoKit.
    packageDependencies.append(.package(url: "https://github.com/apple/swift-crypto.git", "3.0.0"..<"5.0.0"))
    coreDependencies.append(.product(name: "Crypto", package: "swift-crypto"))
}

var products: [Product] = [
    .executable(name: "openusage-cli", targets: ["OpenUsageCLI"])
]
var targets: [Target] = [
    .target(
        name: "OpenUsage",
        dependencies: coreDependencies,
        path: "Sources/OpenUsage",
        exclude: isMac ? [] : macOnlySources,
        resources: [
            .copy("Resources/ProviderIcons"),
            .copy("Resources/pricing_supplement.json"),
            .copy("Resources/pricing_litellm_snapshot.json"),
            .copy("Resources/pricing_models_dev_snapshot.json")
        ],
        swiftSettings: [
            .swiftLanguageMode(.v6)
        ],
        linkerSettings: [
            // Windows Credential Manager (`CredReadW`) and DPAPI (`CryptUnprotectData`).
            .linkedLibrary("Advapi32", .when(platforms: [.windows])),
            .linkedLibrary("Crypt32", .when(platforms: [.windows]))
        ]
    ),
    .executableTarget(
        name: "OpenUsageCLI",
        dependencies: ["OpenUsage"],
        path: "Sources/OpenUsageCLI",
        swiftSettings: [
            .swiftLanguageMode(.v6)
        ]
    ),
    .testTarget(
        name: "OpenUsageTests",
        dependencies: ["OpenUsage"] + (isMac ? [] : [.product(name: "Crypto", package: "swift-crypto")]),
        path: "Tests/OpenUsageTests",
        exclude: isMac ? [] : macOnlyTests,
        swiftSettings: [
            .swiftLanguageMode(.v6)
        ]
    ),
    .testTarget(
        name: "OpenUsageCLITests",
        dependencies: ["OpenUsageCLI"],
        path: "Tests/OpenUsageCLITests",
        swiftSettings: [
            .swiftLanguageMode(.v6)
        ]
    )
]
if isMac {
    products.insert(.executable(name: "OpenUsage", targets: ["OpenUsageApp"]), at: 0)
    targets += [
        .executableTarget(
            name: "OpenUsageApp",
            dependencies: ["OpenUsage"],
            path: "Sources/OpenUsageApp",
            swiftSettings: [
                .swiftLanguageMode(.v6)
            ]
        )
    ]
}

let package = Package(
    name: "OpenUsage",
    platforms: [
        .macOS(.v15)
    ],
    products: products,
    dependencies: packageDependencies,
    targets: targets
)
