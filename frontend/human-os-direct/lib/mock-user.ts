/**
 * Placeholder profile data for local development only.
 *
 * This consumer-facing app doesn't have real MSAL/Entra sign-in wired up
 * yet (unlike the enterprise `human-os-web` app). Once student/consumer
 * auth is integrated, replace this with the real signed-in user's claims
 * (name, email, oid) from the auth context.
 */
export const mockUser = {
  name: "Ana García",
  universityName: "Tec de Monterrey",
  email: "ana.garcia@estudiante.tecmty.mx",
  oid: "3f2a9c1e-7b6d-4e2a-9c31-8a1f5d6b2e40",
};
