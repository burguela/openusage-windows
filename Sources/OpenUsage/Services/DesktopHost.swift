import Foundation

/// The engine surface behind the Windows tray app (`windows/`). The tray app is a thin .NET shell: it
/// runs `openusage dashboard` and renders the display-ready rows this type produces, so every provider,
/// credential, pricing, formatting, and pacing rule stays in the shared Swift engine exactly as the
/// macOS dashboard uses it. Like `UsageReader`, it owns no timer or long-lived service; each call is
/// one pass over the shared snapshot cache.
///
/// Layout is the shipped default (`DefaultLayout`): its enabled metrics, its Always Visible / On Demand
/// split, and its menu-bar pins. Provider on/off is persisted per install and seeded on the first run
/// from the credentials found on the machine, the same way `FirstRunSeeder` seeds the Mac app.
@MainActor
public struct DesktopHost {
    public enum RefreshMode: Sendable {
        /// Only what's cached; never touches the network.
        case cached
        /// Refresh providers whose cache entry is missing or older than the refresh interval.
        case ifStale
        /// Refresh every enabled provider now (the manual "Refresh" action).
        case force
    }

    private let defaults: UserDefaults

    public init(userDefaults: UserDefaults) {
        self.defaults = userDefaults
    }

    // MARK: - Commands

    /// The full dashboard as `openusage.desktop.v1` JSON, after refreshing per `mode`.
    public func dashboard(refresh mode: RefreshMode) async -> Data {
        let session = await EngineSession.open(defaults: defaults)
        await session.seedEnablementIfNeeded()
        switch mode {
        case .cached:
            break
        case .ifStale, .force:
            await session.dataStore.refreshAll(force: mode == .force)
            await PersistentJSONLScanCaches.flushPendingWrites()
        }
        return Self.encode(DesktopDashboardBuilder.build(session: session, now: Date()))
    }

    /// Turn a provider on or off, then return the cached dashboard so the caller can re-render at once.
    public func setProviderEnabled(_ enabled: Bool, providerID: String) async throws -> Data {
        let session = await EngineSession.open(defaults: defaults)
        await session.seedEnablementIfNeeded()
        let token = providerID.lowercased()
        let matched = session.registry.providers.map(\.id).filter {
            $0 == token || ProviderAccountID.family(of: $0) == token
        }
        guard !matched.isEmpty else { throw UsageReaderError.unknownProvider(token) }
        for id in matched {
            session.enablement.setEnabled(enabled, for: id)
        }
        return Self.encode(DesktopDashboardBuilder.build(session: session, now: Date()))
    }

    /// Switch the global Used/Left meter style shared with the Mac app's setting of the same name.
    public func setMeterStyle(showRemaining: Bool) async -> Data {
        let session = await EngineSession.open(defaults: defaults)
        session.dataStore.meterStyle = showRemaining ? .remaining : .used
        return Self.encode(DesktopDashboardBuilder.build(session: session, now: Date()))
    }

    private static func encode(_ dashboard: DesktopDashboard) -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            try container.encode(OpenUsageISO8601.string(from: date))
        }
        do {
            return try encoder.encode(dashboard)
        } catch {
            // Every field is a plain value type; a failure here is a programming error. Fail loudly.
            AppLog.error(.config, "desktop dashboard encoding failed: \(error.localizedDescription)")
            return Data(#"{"schema":"openusage.desktop.v1","providers":[],"error":"encoding_failed"}"#.utf8)
        }
    }
}

/// The providers, stores, and cache for one pass — the same wiring `UsageReader` uses for the CLI.
@MainActor
struct EngineSession {
    let providers: [ProviderRuntime]
    let registry: WidgetRegistry
    let enablement: ProviderEnablementStore
    let dataStore: WidgetDataStore
    let orderedProviderIDs: [String]

    static func open(defaults: UserDefaults) async -> EngineSession {
        // Warm the login-shell capture off-main before the identity read (see `UsageReader.read`).
        await Task.detached(priority: .userInitiated) {
            _ = LoginShellEnvironment.shared.ensureCaptured()
        }.value
        let accountAssembly = await ProviderAccountAssembly.make(defaults: defaults, waitsForLoginShell: false)
        let providers = ProviderCatalog.make(
            defaults: defaults,
            claudeCards: accountAssembly.claudeCards,
            codexCards: accountAssembly.codexCards,
            claudeIdentityKeys: accountAssembly.identityKeysByCard
        )
        let registry = WidgetRegistry.from(providers)
        let enablement = ProviderEnablementStore(defaults: defaults)
        let cache = ProviderSnapshotCache(userDefaults: defaults, allowsPersistedFreshness: true)
        let dataStore = WidgetDataStore(
            registry: registry,
            providers: providers,
            cache: cache,
            defaults: defaults,
            isProviderEnabled: { enablement.isEnabled($0) },
            providerIdentityKeys: accountAssembly.identityKeysByCard
        )
        let savedOrder = LayoutPersistence(defaults: defaults, storageKey: "openusage.layout.v1")
            .loadProviderOrder() ?? []
        return EngineSession(
            providers: providers,
            registry: registry,
            enablement: enablement,
            dataStore: dataStore,
            orderedProviderIDs: registry.orderedProviderIDs(savedOrder: savedOrder)
        )
    }

    /// First run: enable exactly the providers whose credentials are on this machine (falling back to
    /// Claude, Codex, and Cursor when none are found). Later runs: probe only providers this install
    /// has never seen, like `NewProviderSeeder`.
    func seedEnablementIfNeeded() async {
        let allIDs = Set(providers.map(\.provider.id))
        if enablement.enabledIDs == nil {
            enablement.registerKnownProviders(allIDs)
            let detected = await FirstRunSeeder.detectLocalProviders(providers)
            let seeded = detected.isEmpty
                ? FirstRunSeeder.fallbackProviderIDs.intersection(allIDs)
                : detected
            AppLog.info(.config, "desktop first run: enabling \(seeded.sorted())")
            enablement.seedEnabledProviders(seeded)
        } else if let task = NewProviderSeeder.reconcileIfNeeded(providers: providers, enablement: enablement) {
            await task.value
        }
    }
}
