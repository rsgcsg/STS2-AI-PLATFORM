import { servePolicyPort } from "./policy-port.js";

const modulePath = process.env.STS2_POLICY_MODULE;
if (!modulePath) {
  process.stderr.write("STS2_POLICY_MODULE is required\n");
  process.exitCode = 2;
} else {
  const loaded = await import(modulePath);
  if (typeof loaded.default !== "function" && typeof loaded.policy !== "function") {
    process.stderr.write("policy module must export default or policy function\n");
    process.exitCode = 2;
  } else {
    await servePolicyPort((loaded.default ?? loaded.policy) as Parameters<typeof servePolicyPort>[0]);
  }
}
