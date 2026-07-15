/** TODO: Replace with the authenticated person's identity, derived from
 *  the validated Microsoft Entra ID token once real authentication is
 *  wired up (see src/auth/index.ts and the matching backend TODO:
 *  "// TODO: Derive PersonId from the validated Microsoft Entra token."
 *  on every person-scoped Azure Function). These placeholder GUIDs let
 *  the Work Context screen call the real GetTenant/GetPerson/
 *  GetPersonProfile/GetHumanProfile Azure Functions during development.
 */
export const DEMO_PERSON_ID = '00000000-0000-0000-0000-000000000001';
export const DEMO_TENANT_ID = '00000000-0000-0000-0000-000000000002';
