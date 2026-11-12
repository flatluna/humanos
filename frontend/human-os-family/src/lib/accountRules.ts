/**
 * Reglas de negocio de HumanOSFlow para asignación de cuentas MSAL (usuario +
 * contraseña propios) a integrantes de la familia.
 *
 * - Menores de 12 años: NO tienen user id propio en MSAL. Su actividad se
 *   gestiona desde la cuenta del padre/madre/administrador(a) de la familia.
 * - 12 años o más: SÍ tienen su propio user id y password en MSAL (requiere
 *   invitación/registro con su propio correo electrónico).
 */
export const MIN_AGE_FOR_OWN_MSAL_ACCOUNT = 12;

export function requiresOwnMsalAccount(age: number | null | undefined): boolean {
  return typeof age === 'number' && age >= MIN_AGE_FOR_OWN_MSAL_ACCOUNT;
}

/**
 * Roles de autorización de plataforma (independientes del `FamilyRole` de
 * negocio como "father"/"mother"):
 *
 * - AdminEngrams: dueños/staff de EngramsIQ (nosotros). Único rol con acceso
 *   a los agentes de IA (recomendador de capabilities) y a HumanOS Studio.
 * - AdminFamily: administra los datos de su propia familia (futuro: gestión
 *   de miembros, presupuesto, etc.). Por ahora solo puede ver el catálogo.
 * - User: integrante regular de la familia. Solo puede ver el catálogo.
 */
export type PlatformRole = 'AdminEngrams' | 'AdminFamily' | 'User';

export function canUseAiAgentsAndStudio(role: PlatformRole | null | undefined): boolean {
  return role === 'AdminEngrams';
}
