using Prism.Config;

namespace Prism.Lib.Ingress;

/// <summary> Typed representation of HostRules.json. Controls which URL schemes, hosts, and network ranges are permitted during remote fetch operations. Grouped into nested sections mirroring the JSON structure (redirects, networkRanges, timeouts, weTransferPolling, testing).</summary>
internal sealed record HostRules_Config {
    public required string[] AllowedSchemes { get; init; }
    public required string[] BlockedSchemes { get; init; }
    public required string[] BlockedHostPatterns { get; init; }
    public required RedirectsSection Redirects { get; init; }
    public required NetworkRangesSection NetworkRanges { get; init; }
    public required TimeoutsSection Timeouts { get; init; }
    public required WeTransferPollingSection WeTransferPolling { get; init; }
    public required TestingSection Testing { get; init; }

    /// <summary> Loads and parses HostRules.json. The <paramref name="configDirectory"/> parameter is accepted for call-site compatibility but unused — <see cref="ConfigLoader"/> resolves HostRules.json via its own standard search locations, which always include the same directory. Throws <see cref="PrismConfigurationException"/> if the file is absent or malformed. </summary>
    internal static HostRules_Config Load(string configDirectory) => ConfigLoader.Root<HostRules_Config>("HostRules.json");

    internal sealed record RedirectsSection {
        public required bool AllowGenericDirectFileRedirects { get; init; }
        public required bool AllowFetcherOwnedRedirects { get; init; }
    }

    internal sealed record NetworkRangesSection {
        public required bool AllowPrivate { get; init; }
        public required bool AllowLinkLocal { get; init; }
        public required bool AllowLoopback { get; init; }
        public required bool RejectAnyLoopbackDnsResult { get; init; }
    }

    internal sealed record TimeoutsSection {
        public required int ConnectSeconds { get; init; }
        public required int ResponseHeaderSeconds { get; init; }
        public required int IdleReadSeconds { get; init; }
        public required int TotalFetchSeconds { get; init; }
    }

    internal sealed record WeTransferPollingSection {
        public required int ConsentClickTimeoutMs { get; init; }
        public required int ConsentHiddenWaitTimeoutMs { get; init; }
        public required int ConsentSettleDelayMs { get; init; }
        public required int DownloadButtonClickTimeoutMs { get; init; }
        public required int DownloadWaitTimeoutMs { get; init; }
        public required int StreamBufferSizeBytes { get; init; }
        public required int MaxDownloadGb { get; init; }
        public required int ConsentBannerPasses { get; init; }
    }

    internal sealed record TestingSection {
        public required bool AllowLocalhost { get; init; }
    }
}
