# Security And Data Safety

Report vulnerabilities privately through GitHub security advisories when
available. Do not attach game binaries, saves, raw recordings, API keys, or
private machine paths to public issues.

The Mod runs in the game process and can observe player-visible state. The
current implementation is intentionally non-authorizing: it exposes no network
listener, no mutation API, no coordinates, and no serializable native objects.
Recordings may contain gameplay history and should be treated as private data.
