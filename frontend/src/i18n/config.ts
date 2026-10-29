import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { messages } from './messages'

export const appLanguages = ['nb', 'nn', 'en'] as const
export type AppLanguage = (typeof appLanguages)[number]

export function isAppLanguage(value: string): value is AppLanguage {
  return (appLanguages as readonly string[]).includes(value)
}

/** Locale used for dates and numbers, following the active language. */
export function intlLocale(language = i18n.resolvedLanguage ?? i18n.language): string {
  if (language?.startsWith('en')) {
    return 'en-GB'
  }
  if (language?.startsWith('nn')) {
    return 'nn-NO'
  }
  return 'nb-NO'
}

void i18n.use(initReactI18next).init({
  resources: {
    nb: { translation: messages.nb },
    nn: { translation: messages.nn },
    en: { translation: messages.en },
  },
  lng: 'nb',
  fallbackLng: 'nb',
  supportedLngs: [...appLanguages],
  interpolation: { escapeValue: false },
})

function applyDocumentLanguage(language: string) {
  document.documentElement.lang = language
  if (i18n.isInitialized) {
    document.title = `${i18n.t('menu.file_transfers')} – Altinn Broker`
  }
}

i18n.on('languageChanged', applyDocumentLanguage)
applyDocumentLanguage(i18n.language)

export { i18n }
