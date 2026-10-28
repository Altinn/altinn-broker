import { RootProvider } from '@altinn/altinn-components'
import type { ReactNode } from 'react'
import { useLanguage } from './useLanguage'

/** Passes the active language into Altinn components. RootProvider only reads it from props. */
export function LocalizedRoot({ children }: { children: ReactNode }) {
  const { language } = useLanguage()
  return <RootProvider languageCode={language}>{children}</RootProvider>
}
