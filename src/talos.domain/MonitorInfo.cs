namespace talos.domain;

public record MonitorInfo(
    string NameHost,
    string OSDescription,
    string Category,
    List<string> IPAddresses,
    List<string> MacAddresses,
    List<string> Programs
);
