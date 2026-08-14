import { randomUUID } from "node:crypto";
import type { JsonObject } from "./json.js";

interface ControlClientRecord {
  readonly client_session_id: string;
  readonly client_instance_id: string;
}

interface ControlLease {
  readonly controller_lease_id: string;
  readonly controller_generation: number;
  readonly client_session_id: string;
  readonly expires_at: string;
}

interface ControlRegistration {
  readonly runtime_instance_id: string;
  readonly client: ControlClientRecord;
  readonly controller?: ControlLease | null;
}

interface ControlLeaseResponse {
  readonly runtime_instance_id: string;
  readonly controller?: ControlLease | null;
}

interface DecodedControlPayload<T> {
  readonly raw: JsonObject;
  readonly data: T;
}

export interface EnvironmentControllerCredentials {
  readonly clientSessionId: string;
  readonly clientInstanceId: string;
  readonly controllerLeaseId: string;
  readonly controllerGeneration: number;
}

export interface EnvironmentControllerClientIdentity {
  readonly productId: string;
  readonly productName: string;
  readonly productVersion: string;
  readonly clientInstanceId?: string;
}

export interface EnvironmentControlClient {
  registerClient(input: {
    clientInstanceId: string;
    productId: string;
    productName: string;
    productVersion: string;
  }): Promise<DecodedControlPayload<ControlRegistration>>;
  acquireController(
    clientSessionId: string
  ): Promise<DecodedControlPayload<ControlLeaseResponse>>;
  renewController(input: {
    clientSessionId: string;
    controllerLeaseId: string;
    controllerGeneration: number;
  }): Promise<DecodedControlPayload<ControlLeaseResponse>>;
  releaseController(input: {
    clientSessionId: string;
    controllerLeaseId: string;
    controllerGeneration: number;
  }): Promise<DecodedControlPayload<ControlLeaseResponse>>;
}

export class EnvironmentControllerSession {
  private readonly clientInstanceId: string;
  private registration?: ControlRegistration;
  private lease?: ControlLease;
  private renewalTimer?: ReturnType<typeof setTimeout>;
  private operation?: Promise<void>;
  private closed = false;
  private recommendedRenewalMs = 10_000;

  constructor(
    private readonly environment: EnvironmentControlClient,
    private readonly identity: EnvironmentControllerClientIdentity
  ) {
    if (!identity.productId.trim()
        || !identity.productName.trim()
        || !identity.productVersion.trim()) {
      throw new Error("Player Environment controller identity fields must be non-empty");
    }
    this.clientInstanceId = identity.clientInstanceId?.trim()
      || `${identity.productId}-${randomUUID()}`;
  }

  async register(
    runtime: { runtime_instance_id: string },
    coordination: { recommended_renewal_ms: number }
  ): Promise<void> {
    if (this.registration) return;
    const registration = await this.environment.registerClient({
      clientInstanceId: this.clientInstanceId,
      productId: this.identity.productId,
      productName: this.identity.productName,
      productVersion: this.identity.productVersion
    });
    if (registration.data.runtime_instance_id
          !== runtime.runtime_instance_id
        || registration.data.client.client_instance_id
          !== this.clientInstanceId) {
      throw new Error("Player Environment client registration does not match negotiated capabilities");
    }
    this.registration = registration.data;
    this.recommendedRenewalMs = coordination.recommended_renewal_ms;
  }

  async credentials(): Promise<EnvironmentControllerCredentials> {
    if (this.closed) throw new Error("Player Environment controller session is closed");
    await this.serialize(async () => {
      if (!this.registration) {
        throw new Error("Player Environment controller session was not registered");
      }
      if (this.lease && !this.shouldRenew(this.lease)) return;

      if (this.lease) {
        try {
          const renewed = await this.environment.renewController({
            clientSessionId: this.registration.client.client_session_id,
            controllerLeaseId: this.lease.controller_lease_id,
            controllerGeneration: this.lease.controller_generation
          });
          this.acceptLease(renewed.data);
          return;
        } catch {
          this.lease = undefined;
        }
      }

      const acquired = await this.environment.acquireController(
        this.registration.client.client_session_id
      );
      this.acceptLease(acquired.data);
    });

    if (!this.registration || !this.lease) {
      throw new Error("Player Environment did not provide an active controller lease");
    }
    return {
      clientSessionId: this.registration.client.client_session_id,
      clientInstanceId: this.clientInstanceId,
      controllerLeaseId: this.lease.controller_lease_id,
      controllerGeneration: this.lease.controller_generation
    };
  }

  snapshot(): JsonObject {
    return {
      client_instance_id: this.clientInstanceId,
      registered: Boolean(this.registration),
      client_session_id: this.registration?.client.client_session_id ?? null,
      controller_lease_id: this.lease?.controller_lease_id ?? null,
      controller_generation: this.lease?.controller_generation ?? null,
      controller_expires_at: this.lease?.expires_at ?? null
    };
  }

  async close(): Promise<void> {
    this.closed = true;
    if (this.renewalTimer) {
      clearTimeout(this.renewalTimer);
      this.renewalTimer = undefined;
    }
    const registration = this.registration;
    const lease = this.lease;
    this.lease = undefined;
    if (!registration || !lease) return;
    try {
      await this.environment.releaseController({
        clientSessionId: registration.client.client_session_id,
        controllerLeaseId: lease.controller_lease_id,
        controllerGeneration: lease.controller_generation
      });
    } catch {
      // TTL is the crash-safe release path; shutdown must not mask the caller's result.
    }
  }

  private acceptLease(response: ControlLeaseResponse): void {
    if (!this.registration
        || response.runtime_instance_id !== this.registration.runtime_instance_id
        || !response.controller
        || response.controller.client_session_id
          !== this.registration.client.client_session_id) {
      throw new Error("Player Environment controller response does not match this registered client");
    }
    this.lease = response.controller;
    this.scheduleRenewal();
  }

  private scheduleRenewal(): void {
    if (this.renewalTimer) clearTimeout(this.renewalTimer);
    if (!this.lease || this.closed) return;
    const expiresInMs = Date.parse(this.lease.expires_at) - Date.now();
    const delayMs = Math.max(
      100,
      expiresInMs - this.recommendedRenewalMs
    );
    this.renewalTimer = setTimeout(() => {
      void this.credentials().catch(() => {
        this.lease = undefined;
      });
    }, delayMs);
    this.renewalTimer.unref?.();
  }

  private shouldRenew(lease: ControlLease): boolean {
    return Date.parse(lease.expires_at) - Date.now()
      <= this.recommendedRenewalMs;
  }

  private async serialize(operation: () => Promise<void>): Promise<void> {
    const previous = this.operation;
    const current = (async () => {
      if (previous) {
        try {
          await previous;
        } catch {
          // The new operation gets one independent attempt.
        }
      }
      await operation();
    })();
    this.operation = current;
    try {
      await current;
    } finally {
      if (this.operation === current) this.operation = undefined;
    }
  }
}
