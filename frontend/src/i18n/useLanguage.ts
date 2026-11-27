import { createContext, useContext } from 'react'
import type { AppLanguage } from './config'

export type LanguageContextValue = {
  language: AppLanguage
  selectLanguage: (language: string) => Promise<void>
}

export const LanguageContext = createContext<LanguageContextValue | null>(null)

export function useLanguage(): LanguageContextValue {
  const context = useContext(LanguageContext)
  if (!context) {
    throw new Error('useLanguage must be used within LanguageProvider')
  }
  return context
}
