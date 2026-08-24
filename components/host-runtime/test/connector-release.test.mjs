import assert from "node:assert/strict";
import test from "node:test";
import {
  assertConnectorReleaseIdentity,
  CONNECTOR_RELEASE
} from "../src/connector-release.mjs";

test("Platform Connector release pins archive and native artifact identity", () => {
  assert.match(CONNECTOR_RELEASE.baseUrl, /rsgcsg\/STS2-AI-PLATFORM\/releases\/download\/connector\//u);
  assert.equal(CONNECTOR_RELEASE.version, "1.2.0-rc.5");
  assert.doesNotThrow(() => assertConnectorReleaseIdentity({
    source_revision: CONNECTOR_RELEASE.sourceRevision,
    source_protocol: CONNECTOR_RELEASE.protocol,
    artifact_sha256: CONNECTOR_RELEASE.artifactSha256,
    artifact_mvid: CONNECTOR_RELEASE.artifactMvid
  }));
});

test("Connector release identity drift fails closed", () => {
  assert.throws(
    () => assertConnectorReleaseIdentity({
      source_revision: CONNECTOR_RELEASE.sourceRevision,
      source_protocol: CONNECTOR_RELEASE.protocol,
      artifact_sha256: "0".repeat(64),
      artifact_mvid: CONNECTOR_RELEASE.artifactMvid
    }),
    /artifact_sha256/u
  );
});
