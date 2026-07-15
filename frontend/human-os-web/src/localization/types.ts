/** A piece of user/domain content authored in both supported languages.
 *  Used for names and labels that come from user or organization data
 *  (capability names, goals, aspirations) as opposed to fixed UI chrome,
 *  which is translated via i18next translation keys instead.
 */
export interface LocalizedText {
  en: string;
  es: string;
}
